using Microsoft.AspNetCore.Mvc;
using PandemicShield.Api.Extensions;
using PandemicShield.Contracts;
using PandemicShield.DataAccess.Data;
using PandemicShield.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace PandemicShield.Api.Endpoints
{
    public static class AnalysisEndpoints
    {

        public static void MapAnalysisEndpoints(this IEndpointRouteBuilder app)
        {
            app.MapPost("/api/analyze", async (
                [FromForm(Name = "file")] IFormFile file,
                [FromForm] ThreatCategory category,
                [FromForm] string? nextcladeDataset,
                [FromForm] string? amrOrganism,
                PandemicDbContext db,
                AnalysisSubmitter submitter) =>
            {
                if (file == null || file.Length == 0)
                    return Results.BadRequest("Файл порожній або не вибрано");

                var allowedExtensions = new[] { ".fasta", ".fa", ".fna", ".txt" };
                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

                if (!allowedExtensions.Contains(extension))
                    return Results.BadRequest("Неправильний формат файлу. Дозволені лише FASTA файли (.fasta, .fa, .txt).");

                using var peekStream = new StreamReader(file.OpenReadStream());
                var firstLine = await peekStream.ReadLineAsync();

                if (firstLine == null || !firstLine.StartsWith('>'))
                    return Results.BadRequest("\"Файл пошкоджений або не є валідним FASTA-форматом (має починатися з '>').");

                peekStream.BaseStream.Position = 0;

                string? error = submitter.Validate(category, nextcladeDataset, amrOrganism);
                if (error != null) return Results.BadRequest(error);

                using var stream = file.OpenReadStream();
                var (run, response) = await submitter.SubmitAsync(db, stream,
                    new AnalysisSubmitter.Request(file.FileName, category, nextcladeDataset, amrOrganism));

                return Results.Ok(new
                {
                    RunId = run.Id,
                    Message = response.Message,
                    ChunkProcessed = response.ChunkProcessed,
                    RecordCount = response.RecordCount,
                    BaseCount = response.BaseCount,
                    InvalidSymbolCount = response.InvalidSymbolCount,
                    NextcladeDataset = run.NextcladeDataset,
                    AmrOrganism = run.AmrOrganism
                });
            })
            .DisableAntiforgery();

            // Повторний аналіз уже завантаженого файлу (наприклад, з новішою версією набору даних).
            app.MapPost("/api/runs/{id}/nextclade", async (
                Guid id,
                NextcladeRerunRequest request,
                PandemicDbContext db,
                AnalysisOptions options,
                JobPublisher jobs) =>
            {
                var run = await db.Runs.FindAsync(id);
                if (run == null) return Results.NotFound();
                if (!options.IsKnownDataset(request.Dataset))
                    return Results.BadRequest($"Невідомий набір даних Nextclade: {request.Dataset}");

                string inputPath = options.RunInputPath(run.Id);
                if (!File.Exists(inputPath))
                    return Results.Conflict("Файл цього запуску не збережено: Nextclade для нього не запитували.");

                await db.Mutations.Where(m => m.RunId == id).ExecuteDeleteAsync();
                await db.SequenceResults.Where(s => s.RunId == id).ExecuteDeleteAsync();
                await db.Threats.Where(t => t.RunId == id && t.Source == ThreatAlertEntity.NextcladeSource).ExecuteDeleteAsync();

                run.RequestNextclade(request.Dataset);
                await db.SaveChangesAsync();

                await jobs.PublishAsync(JobPublisher.NextcladeQueue,
                    new NextcladeJobMessage { RunId = run.Id, FilePath = inputPath, Dataset = request.Dataset });
                return Results.Accepted($"/api/runs/{id}/sequences");
            });

            // Повторний аналіз AMRFinderPlus (наприклад, з іншим видом або після оновлення бази).
            app.MapPost("/api/runs/{id}/amrfinder", async (
                Guid id,
                AmrRerunRequest request,
                PandemicDbContext db,
                AnalysisOptions options,
                JobPublisher jobs) =>
            {
                var run = await db.Runs.FindAsync(id);
                if (run == null) return Results.NotFound();

                string? organism = string.IsNullOrWhiteSpace(request.Organism) ? null : request.Organism.Trim();
                if (organism != null && !options.IsKnownOrganism(organism))
                    return Results.BadRequest($"AMRFinderPlus не знає вид «{organism}».");

                string inputPath = options.RunInputPath(run.Id);
                if (!File.Exists(inputPath))
                    return Results.Conflict("Файл цього запуску не збережено.");

                await db.AmrHits.Where(h => h.RunId == id).ExecuteDeleteAsync();
                await db.Threats.Where(t => t.RunId == id && t.Source == ThreatAlertEntity.AmrFinderSource).ExecuteDeleteAsync();

                run.RequestAmr(organism);
                await db.SaveChangesAsync();

                await jobs.PublishAsync(JobPublisher.AmrFinderQueue, new AmrJobMessage { RunId = run.Id, FilePath = inputPath, Organism = organism });
                return Results.Accepted($"/api/runs/{id}/amr");
            });
        }

        public sealed record NextcladeRerunRequest(string Dataset);

        public sealed record AmrRerunRequest(string? Organism);
    }
}
