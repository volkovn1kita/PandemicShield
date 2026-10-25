using Microsoft.EntityFrameworkCore;
using PandemicShield.Api.Extensions;
using PandemicShield.Contracts;
using PandemicShield.DataAccess.Data;
using PandemicShield.DataAccess.Entities;

namespace PandemicShield.Api.Endpoints
{
    public static class AmrEndpoints
    {
        public static void MapAmrEndpoints(this IEndpointRouteBuilder app)
        {
            app.MapGet("/api/amr/organisms", (AnalysisOptions options) => Results.Ok(options.Organisms));

            app.MapGet("/api/runs/{id}/amr", async (Guid id, PandemicDbContext db) =>
            {
                if (!await db.Runs.AnyAsync(r => r.Id == id)) return Results.NotFound();

                var hits = await db.AmrHits
                    .Where(h => h.RunId == id)
                    .OrderBy(h => h.Type).ThenBy(h => h.Class).ThenBy(h => h.ElementSymbol)
                    .ToListAsync();
                return Results.Ok(hits);
            });

            // Повторне визначення типу штаму (наприклад, після оновлення схем PubMLST в образі).
            app.MapPost("/api/runs/{id}/mlst", async (Guid id, PandemicDbContext db, AnalysisOptions options, JobPublisher jobs) =>
            {
                var run = await db.Runs.FindAsync(id);
                if (run == null) return Results.NotFound();
                if (run.Category != ThreatCategory.Bacteria) return Results.BadRequest("MLST визначає тип штаму лише для бактерій.");

                string inputPath = options.RunInputPath(run.Id);
                if (!File.Exists(inputPath)) return Results.Conflict("Файл цього запуску не збережено.");

                await db.Threats.Where(t => t.RunId == id && t.Source == ThreatAlertEntity.MlstSource).ExecuteDeleteAsync();
                run.RequestMlst();
                await db.SaveChangesAsync();

                await jobs.PublishAsync(JobPublisher.MlstQueue, new MlstJobMessage { RunId = run.Id, FilePath = inputPath, Organism = run.AmrOrganism });
                return Results.Accepted($"/api/runs");
            });

            // Чи той самий штам в інших ізолятах? ST147 у кількох відділеннях — ознака спалаху.
            app.MapGet("/api/mlst/search", async (string st, PandemicDbContext db) =>
            {
                // "ST147" і "147" — одне й те саме.
                string value = st.Trim();
                if (value.StartsWith("ST", StringComparison.OrdinalIgnoreCase)) value = value[2..];
                var runs = await db.Runs
                    .Where(r => r.MlstSequenceType == value)
                    .OrderByDescending(r => r.StartedAt)
                    .Select(r => new { RunId = r.Id, r.FileName, r.AmrOrganism, r.MlstScheme, r.MlstSequenceType, r.StartedAt })
                    .Take(1000)
                    .ToListAsync();
                return Results.Ok(runs);
            });

            // Порівняння між ізолятами: у яких запусках є ген (символ або його початок, наприклад blaNDM)?
            app.MapGet("/api/amr/search", async (string gene, PandemicDbContext db) =>
            {
                var hits = await (
                    from h in db.AmrHits
                    where h.ElementSymbol.StartsWith(gene)
                    join r in db.Runs on h.RunId equals r.Id
                    orderby r.StartedAt descending
                    select new
                    {
                        RunId = r.Id,
                        r.FileName,
                        r.AmrOrganism,
                        h.ContigId,
                        h.ElementSymbol,
                        h.Class,
                        h.Subclass,
                        h.IdentityPercent
                    }).Take(1000).ToListAsync();
                return Results.Ok(hits);
            });
        }
    }
}
