using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PandemicShield.Api.Extensions;
using PandemicShield.Api.Imports;
using PandemicShield.Contracts;
using PandemicShield.DataAccess.Data;
using PandemicShield.DataAccess.Entities;

namespace PandemicShield.Api.Endpoints
{
    /// <summary>Масовий імпорт відкритих даних NCBI: список номерів або архів NCBI Datasets.</summary>
    public static class ImportEndpoints
    {
        public sealed record NcbiImportRequest(string Accessions, ThreatCategory Category, string? NextcladeDataset, string? AmrOrganism);

        public static void MapImportEndpoints(this IEndpointRouteBuilder app)
        {
            app.MapGet("/api/imports", async (PandemicDbContext db) =>
                Results.Ok(await db.Imports.OrderByDescending(b => b.CreatedAt).Take(20).ToListAsync()));

            app.MapPost("/api/imports/ncbi", async (NcbiImportRequest request, PandemicDbContext db, AnalysisSubmitter submitter, JobPublisher jobs) =>
            {
                string? error = ValidateCategory(request.Category) ?? submitter.Validate(request.Category, request.NextcladeDataset, request.AmrOrganism);
                if (error != null) return Results.BadRequest(error);

                var parsed = NcbiAccessions.Parse(request.Accessions);
                if (parsed.Invalid.Count > 0)
                    return Results.BadRequest($"Не схоже на номери NCBI: {string.Join(", ", parsed.Invalid.Take(10))}. " +
                                              "Приклади: OQ291490.1 (послідовність), GCA_009887825.1 (збірка).");
                if (parsed.Total == 0)
                    return Results.BadRequest("Вкажіть хоча б один номер NCBI.");
                if (parsed.Total > NcbiAccessions.MaxPerImport)
                    return Results.BadRequest($"За один імпорт — не більше {NcbiAccessions.MaxPerImport} номерів, а вказано {parsed.Total}.");
                if (request.Category == ThreatCategory.Virus && parsed.Assemblies.Count > 0)
                    return Results.BadRequest($"{parsed.Assemblies[0]} — це збірка геному (бактерії). Для вірусів вкажіть номери послідовностей, " +
                                              "наприклад OQ291490.1, або оберіть тип «Бактерія».");

                var batch = new ImportBatchEntity(request.Category, Normalize(request.NextcladeDataset), Normalize(request.AmrOrganism),
                    string.Join(' ', parsed.All), null, parsed.Total);
                db.Imports.Add(batch);
                await db.SaveChangesAsync();

                await jobs.PublishAsync(JobPublisher.ImportQueue, new ImportJobMessage { ImportId = batch.Id });
                return Results.Accepted("/api/imports", batch);
            });

            // Архів, завантажений з сайту NCBI (Datasets / NCBI Virus) або через `datasets download`.
            app.MapPost("/api/imports/ncbi-archive", async (
                [FromForm(Name = "file")] IFormFile file,
                [FromForm] ThreatCategory category,
                [FromForm] string? nextcladeDataset,
                [FromForm] string? amrOrganism,
                PandemicDbContext db,
                AnalysisSubmitter submitter,
                AnalysisOptions options,
                JobPublisher jobs) =>
            {
                if (file == null || file.Length == 0)
                    return Results.BadRequest("Файл порожній або не вибрано.");
                if (!file.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    return Results.BadRequest("Очікується ZIP-архів NCBI Datasets (.zip).");

                string? error = ValidateCategory(category) ?? submitter.Validate(category, nextcladeDataset, amrOrganism);
                if (error != null) return Results.BadRequest(error);

                var batch = new ImportBatchEntity(category, Normalize(nextcladeDataset), Normalize(amrOrganism), string.Empty, file.FileName, 0);

                // Архів може бути на сотні мегабайт: пишемо його потоком у спільний том, а розбираємо у фоні.
                string archivePath = Path.Combine(options.ImportDir(batch.Id), "archive.zip");
                Directory.CreateDirectory(Path.GetDirectoryName(archivePath)!);
                await using (var output = File.Create(archivePath))
                    await file.CopyToAsync(output);

                db.Imports.Add(batch);
                await db.SaveChangesAsync();

                await jobs.PublishAsync(JobPublisher.ImportQueue, new ImportJobMessage { ImportId = batch.Id, ArchivePath = archivePath });
                return Results.Accepted("/api/imports", batch);
            })
            .DisableAntiforgery();
        }

        private static string? ValidateCategory(ThreatCategory category) =>
            category is ThreatCategory.Virus or ThreatCategory.Bacteria ? null : "Імпорт з NCBI підтримує віруси й бактерії.";

        private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
