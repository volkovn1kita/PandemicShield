using Microsoft.EntityFrameworkCore;
using PandemicShield.DataAccess.Data;

namespace PandemicShield.Api.Endpoints
{
    public static class ThreatEndpoints
    {
        public static void MapThreatEndpoints(this IEndpointRouteBuilder app)
        {
            app.MapGet("/api/threats", async (PandemicDbContext db) =>
            {
                var threats = await db.Threats.OrderByDescending(t => t.DetectedAt).ToListAsync();
                return Results.Ok(threats);
            });

            app.MapGet("/api/runs", async (PandemicDbContext db) =>
            {
                var runs = await db.Runs.OrderByDescending(r => r.StartedAt).ToListAsync();
                return Results.Ok(runs);
            });

            app.MapGet("/api/runs/{id}/threats", async (Guid id, PandemicDbContext db) =>
            {
                if (!await db.Runs.AnyAsync(r => r.Id == id)) return Results.NotFound();

                var threats = await db.Threats
                    .Where(t => t.RunId == id)
                    .OrderBy(t => t.RecordId).ThenBy(t => t.NtStart)
                    .ToListAsync();
                return Results.Ok(threats);
            });

            app.MapDelete("/api/threats/{id}", async (Guid id, PandemicDbContext db) =>
            {
                var threat = await db.Threats.FindAsync(id);
                if (threat == null) return Results.NotFound();

                db.Threats.Remove(threat);
                await db.SaveChangesAsync();
                return Results.Ok();
            });

        }
    }
}
