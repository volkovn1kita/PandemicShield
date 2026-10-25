using Microsoft.EntityFrameworkCore;
using PandemicShield.Api.Extensions;
using PandemicShield.DataAccess.Data;

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
