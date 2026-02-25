using Microsoft.EntityFrameworkCore;
using PandemicShield.DataAccess.Data;
using PandemicShield.DataAccess.Entities;
using System.Security.Cryptography.Xml;

namespace PandemicShield.Api.Endpoints
{
    public static class DictionaryEndpoints
    {
        public static void MapDictionaryEndpoints(this IEndpointRouteBuilder app)
        {
            app.MapGet("/api/dictionary", async (PandemicDbContext db) =>
            {
                var dictionary = await db.Mutation.ToListAsync();
                return Results.Ok(dictionary);
            });

            app.MapPost("/api/dictionary", async (ReferenceMutationEntity mutation, PandemicDbContext db) =>
            {
                db.Mutation.Add(mutation);
                await db.SaveChangesAsync();
                return Results.Ok();
            });
        }

      
    }
}
