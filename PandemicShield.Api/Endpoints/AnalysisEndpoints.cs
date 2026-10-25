using Google.Protobuf;
using Microsoft.AspNetCore.Mvc;
using PandemicShield.Api.Extensions;
using PandemicShield.Contracts;
using PandemicShield.Contracts.Grpc;
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
                DnaParserService.DnaParserServiceClient grpcClient,
                PandemicDbContext db,
                NextcladeOptions nextclade,
                NextcladeJobPublisher jobs) =>
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

                // Nextclade працює з вірусними геномами й потребує цілого файлу, а не вікон.
                bool runNextclade = !string.IsNullOrWhiteSpace(nextcladeDataset);
                if (runNextclade && category != ThreatCategory.Virus)
                    return Results.BadRequest("Nextclade аналізує лише віруси: оберіть категорію Virus.");
                if (runNextclade && !nextclade.IsKnownDataset(nextcladeDataset!))
                    return Results.BadRequest($"Невідомий набір даних Nextclade: {nextcladeDataset}. Доступні: {string.Join(", ", nextclade.Datasets)}");

                var run = new AnalysisRunEntity(file.FileName, category);
                db.Runs.Add(run);
                await db.SaveChangesAsync();

                string inputPath = nextclade.RunInputPath(run.Id);

                try
                {
                    using var call = grpcClient.StreamDnaForParsing();
                    using var stream = file.OpenReadStream();

                    // Для Nextclade файл паралельно зберігається у спільний том, звідки його прочитає воркер.
                    if (runNextclade) Directory.CreateDirectory(Path.GetDirectoryName(inputPath)!);
                    await using FileStream? copy = runNextclade ? File.Create(inputPath) : null;

                    byte[] buffer = new byte[32 * 1024];
                    int bytesRead;

                    while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        var byteString = ByteString.CopyFrom(buffer, 0, bytesRead);

                        var request = new DnaChunkRequest
                        {
                            ChunkData = byteString,
                            CategoryId = (int)category,
                            RunId = run.Id.ToString()
                        };

                        await call.RequestStream.WriteAsync(request);
                        if (copy != null) await copy.WriteAsync(buffer.AsMemory(0, bytesRead));
                    }

                    if (copy != null) await copy.FlushAsync();

                    await call.RequestStream.CompleteAsync();

                    var response = await call.ResponseAsync;

                    run.MarkStreamed(response.RecordCount, response.BaseCount, response.InvalidSymbolCount,
                        response.ChunkProcessed, response.Message);
                    if (runNextclade) run.RequestNextclade(nextcladeDataset!);
                    await db.SaveChangesAsync();

                    if (runNextclade)
                    {
                        await jobs.PublishAsync(new NextcladeJobMessage
                        {
                            RunId = run.Id,
                            FilePath = inputPath,
                            Dataset = nextcladeDataset!
                        });
                    }

                    return Results.Ok(new
                    {
                        RunId = run.Id,
                        Message = response.Message,
                        ChunkProcessed = response.ChunkProcessed,
                        RecordCount = response.RecordCount,
                        BaseCount = response.BaseCount,
                        InvalidSymbolCount = response.InvalidSymbolCount,
                        NextcladeDataset = runNextclade ? nextcladeDataset : null
                    });
                }
                catch (Exception ex)
                {
                    run.MarkFailed(ex.Message);
                    await db.SaveChangesAsync();
                    throw;
                }
            })
            .DisableAntiforgery();

            // Повторний аналіз уже завантаженого файлу (наприклад, з новішою версією набору даних).
            app.MapPost("/api/runs/{id}/nextclade", async (
                Guid id,
                NextcladeRerunRequest request,
                PandemicDbContext db,
                NextcladeOptions nextclade,
                NextcladeJobPublisher jobs) =>
            {
                var run = await db.Runs.FindAsync(id);
                if (run == null) return Results.NotFound();
                if (!nextclade.IsKnownDataset(request.Dataset))
                    return Results.BadRequest($"Невідомий набір даних Nextclade: {request.Dataset}");

                string inputPath = nextclade.RunInputPath(run.Id);
                if (!File.Exists(inputPath))
                    return Results.Conflict("Файл цього запуску не збережено: Nextclade для нього не запитували.");

                await db.Mutations.Where(m => m.RunId == id).ExecuteDeleteAsync();
                await db.SequenceResults.Where(s => s.RunId == id).ExecuteDeleteAsync();
                await db.Threats.Where(t => t.RunId == id && t.Source == ThreatAlertEntity.NextcladeSource).ExecuteDeleteAsync();

                run.RequestNextclade(request.Dataset);
                await db.SaveChangesAsync();

                await jobs.PublishAsync(new NextcladeJobMessage { RunId = run.Id, FilePath = inputPath, Dataset = request.Dataset });
                return Results.Accepted($"/api/runs/{id}/sequences");
            });
        }

        public sealed record NextcladeRerunRequest(string Dataset);
    }
}
