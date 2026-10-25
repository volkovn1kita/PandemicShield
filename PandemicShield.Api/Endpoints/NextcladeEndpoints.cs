using Microsoft.EntityFrameworkCore;
using PandemicShield.Api.Extensions;
using PandemicShield.Bio.Variants;
using PandemicShield.DataAccess.Data;
using PandemicShield.DataAccess.Entities;

namespace PandemicShield.Api.Endpoints
{
    public static class NextcladeEndpoints
    {
        public sealed record VariantMarkerRequest(string Name, string Notation, string? Dataset, string? Description, string? Source);

        public static void MapNextcladeEndpoints(this IEndpointRouteBuilder app)
        {
            app.MapGet("/api/nextclade/datasets", (AnalysisOptions nextclade) => Results.Ok(nextclade.Datasets));

            app.MapGet("/api/runs/{id}/sequences", async (Guid id, PandemicDbContext db) =>
            {
                if (!await db.Runs.AnyAsync(r => r.Id == id)) return Results.NotFound();

                var sequences = await db.SequenceResults
                    .Where(s => s.RunId == id)
                    .OrderBy(s => s.SeqName)
                    .ToListAsync();
                return Results.Ok(sequences);
            });

            // Компактні позиції замін і делецій для міні-карти геному в кожному рядку таблиці (як «Sequence view» у Nextclade Web).
            app.MapGet("/api/runs/{id}/genome-marks", async (Guid id, PandemicDbContext db) =>
            {
                var rows = await db.Mutations
                    .Where(m => m.RunId == id && (m.Kind == "NucleotideSubstitution" || m.Kind == "NucleotideDeletion"))
                    .Select(m => new { m.SequenceResultId, m.Kind, m.Position, m.EndPosition })
                    .ToListAsync();

                var marks = rows
                    .GroupBy(r => r.SequenceResultId)
                    .Select(g => new
                    {
                        SequenceResultId = g.Key,
                        Substitutions = g.Where(r => r.Kind == "NucleotideSubstitution").Select(r => r.Position).OrderBy(p => p).ToList(),
                        Deletions = g.Where(r => r.Kind == "NucleotideDeletion").Select(r => new[] { r.Position, r.EndPosition ?? r.Position }).ToList()
                    });
                return Results.Ok(marks);
            });

            app.MapGet("/api/sequences/{id}/mutations", async (Guid id, PandemicDbContext db) =>
            {
                var mutations = await db.Mutations
                    .Where(m => m.SequenceResultId == id)
                    .OrderBy(m => m.Gene).ThenBy(m => m.Position)
                    .ToListAsync();
                return Results.Ok(mutations);
            });

            // Порівняння між зразками: у яких послідовностях (з усіх запусків) є ця мутація?
            app.MapGet("/api/mutations", async (string label, PandemicDbContext db) =>
            {
                var hits = await (
                    from m in db.Mutations
                    where m.Label == label
                    join s in db.SequenceResults on m.SequenceResultId equals s.Id
                    join r in db.Runs on s.RunId equals r.Id
                    orderby s.AnalyzedAt descending
                    select new
                    {
                        SequenceId = s.Id,
                        s.SeqName,
                        RunId = r.Id,
                        r.FileName,
                        s.Clade,
                        s.CladeDisplay,
                        s.Lineage,
                        s.QcStatus,
                        s.AnalyzedAt
                    }).Take(1000).ToListAsync();
                return Results.Ok(hits);
            });

            app.MapGet("/api/variant-markers", async (PandemicDbContext db) =>
                Results.Ok(await db.VariantMarkers.OrderBy(m => m.Name).ToListAsync()));

            app.MapPost("/api/variant-markers", async (VariantMarkerRequest request, PandemicDbContext db, AnalysisOptions nextclade) =>
            {
                if (string.IsNullOrWhiteSpace(request.Name))
                    return Results.BadRequest("Вкажіть назву маркера.");
                if (!VariantPattern.TryParse(request.Notation, out var pattern, out string error))
                    return Results.BadRequest(error);

                string? dataset = string.IsNullOrWhiteSpace(request.Dataset) ? null : request.Dataset.Trim();
                if (dataset != null && pattern.IsAmr)
                    return Results.BadRequest("Правила для генів резистентності (gene:, class:, subclass:) не прив'язуються до набору даних Nextclade.");
                if (dataset != null && !nextclade.IsKnownDataset(dataset))
                    return Results.BadRequest($"Невідомий набір даних Nextclade: {dataset}");

                // Зберігаємо канонічне позначення: "s:d614g" → "S:D614G".
                var marker = new VariantMarkerEntity(request.Name.Trim(), pattern.ToString(), dataset,
                    request.Description, request.Source);
                db.VariantMarkers.Add(marker);
                await db.SaveChangesAsync();
                return Results.Ok(marker);
            });

            app.MapDelete("/api/variant-markers/{id}", async (Guid id, PandemicDbContext db) =>
            {
                var marker = await db.VariantMarkers.FindAsync(id);
                if (marker == null) return Results.NotFound();

                db.VariantMarkers.Remove(marker);
                await db.SaveChangesAsync();
                return Results.Ok();
            });
        }
    }
}
