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
                var threats = await db.Threats.ToListAsync();
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
