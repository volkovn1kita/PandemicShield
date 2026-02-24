using Microsoft.EntityFrameworkCore;
using PandemicShield.DataAccess.Data;
using PandemicShield.DataAccess.Entities;
namespace PandemicShield.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddDbContext<PandemicDbContext>();

            builder.Services.AddOpenApi();

            var app = builder.Build();

            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
            }

            app.MapGet("/api/threats", async (PandemicDbContext db) =>
            {
                var threats = await db.Threats.ToListAsync();
                return Results.Ok(threats);
            });

            app.MapPost("/api/dictionary", async (ReferenceMutationEntity mutation, PandemicDbContext db) =>
            {
                db.Mutation.Add(mutation);
                await db.SaveChangesAsync();
                return Results.Ok();
            });

            app.MapGet("/api/dictionary", async (PandemicDbContext db) =>
            {
                var mutations = await db.Mutation.ToListAsync();
                return Results.Ok(mutations);
            });
             

            app.Run();
        }
    }
}
