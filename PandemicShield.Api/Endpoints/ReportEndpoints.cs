using PandemicShield.Api.Reports;
using PandemicShield.DataAccess.Data;

namespace PandemicShield.Api.Endpoints
{
    /// <summary>Звіт по зразку: PDF — щоб передати результат далі, CSV — для подальшої обробки.</summary>
    public static class ReportEndpoints
    {
        public static void MapReportEndpoints(this IEndpointRouteBuilder app)
        {
            app.MapGet("/api/runs/{id}/report.pdf", async (Guid id, string? lang, PandemicDbContext db) =>
            {
                var report = await RunReport.LoadAsync(db, id);
                if (report == null) return Results.NotFound();
                var text = ReportText.For(lang);
                return Results.File(PdfReport.Build(report, text), "application/pdf", report.FileName("pdf"));
            });

            app.MapGet("/api/runs/{id}/report.csv", async (Guid id, PandemicDbContext db) =>
            {
                var report = await RunReport.LoadAsync(db, id);
                if (report == null) return Results.NotFound();
                return Results.File(CsvReport.ToBytes(CsvReport.Build(report)), "text/csv; charset=utf-8", report.FileName("csv"));
            });
        }
    }
}
