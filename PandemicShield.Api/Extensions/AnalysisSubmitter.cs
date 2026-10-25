using Google.Protobuf;
using PandemicShield.Contracts;
using PandemicShield.Contracts.Grpc;
using PandemicShield.DataAccess.Data;
using PandemicShield.DataAccess.Entities;

namespace PandemicShield.Api.Extensions
{
    /// <summary>
    /// Запускає аналіз одного FASTA: стрімить його в Parser (вікна → Worker → мотиви ORF), паралельно зберігає копію
    /// у спільний том і ставить завдання для Nextclade (віруси) або AMRFinderPlus (бактерії).
    /// Спільний для ручного завантаження й імпорту з NCBI.
    /// </summary>
    public sealed class AnalysisSubmitter
    {
        public sealed record Request(
            string FileName,
            ThreatCategory Category,
            string? NextcladeDataset,
            string? AmrOrganism,
            Guid? ImportId = null,
            string? SourceAccession = null);

        private readonly DnaParserService.DnaParserServiceClient _grpc;
        private readonly AnalysisOptions _options;
        private readonly JobPublisher _jobs;

        public AnalysisSubmitter(DnaParserService.DnaParserServiceClient grpc, AnalysisOptions options, JobPublisher jobs)
        {
            _grpc = grpc;
            _options = options;
            _jobs = jobs;
        }

        /// <summary>Перевіряє параметри; повертає текст помилки або null.</summary>
        public string? Validate(ThreatCategory category, string? nextcladeDataset, string? amrOrganism)
        {
            if (!string.IsNullOrWhiteSpace(nextcladeDataset))
            {
                if (category != ThreatCategory.Virus)
                    return "Nextclade аналізує лише віруси: оберіть категорію Virus.";
                if (!_options.IsKnownDataset(nextcladeDataset))
                    return $"Невідомий набір даних Nextclade: {nextcladeDataset}. Доступні: {string.Join(", ", _options.Datasets)}";
            }

            if (!string.IsNullOrWhiteSpace(amrOrganism) && !_options.IsKnownOrganism(amrOrganism.Trim()))
                return $"AMRFinderPlus не знає вид «{amrOrganism}». Доступні: {string.Join(", ", _options.Organisms)}";

            return null;
        }

        public async Task<(AnalysisRunEntity Run, DnaProcessResponse Response)> SubmitAsync(
            PandemicDbContext db, Stream input, Request request, CancellationToken cancellationToken = default)
        {
            // Nextclade працює з вірусними геномами й потребує цілого файлу, а не вікон.
            bool runNextclade = !string.IsNullOrWhiteSpace(request.NextcladeDataset);
            // Бактеріальні збірки завжди аналізує AMRFinderPlus; вид (необов'язковий) вмикає пошук точкових мутацій.
            bool runAmr = request.Category == ThreatCategory.Bacteria;
            string? organism = string.IsNullOrWhiteSpace(request.AmrOrganism) ? null : request.AmrOrganism.Trim();
            bool keepFile = runNextclade || runAmr;

            var run = new AnalysisRunEntity(request.FileName, request.Category, request.ImportId, request.SourceAccession);
            db.Runs.Add(run);
            await db.SaveChangesAsync(cancellationToken);

            string inputPath = _options.RunInputPath(run.Id);

            try
            {
                using var call = _grpc.StreamDnaForParsing(cancellationToken: cancellationToken);

                // Для Nextclade і AMRFinderPlus файл паралельно зберігається у спільний том, звідки його прочитає воркер.
                if (keepFile) Directory.CreateDirectory(Path.GetDirectoryName(inputPath)!);
                await using FileStream? copy = keepFile ? File.Create(inputPath) : null;

                byte[] buffer = new byte[32 * 1024];
                int bytesRead;

                while ((bytesRead = await input.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await call.RequestStream.WriteAsync(new DnaChunkRequest
                    {
                        ChunkData = ByteString.CopyFrom(buffer, 0, bytesRead),
                        CategoryId = (int)request.Category,
                        RunId = run.Id.ToString()
                    }, cancellationToken);
                    if (copy != null) await copy.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                }

                if (copy != null) await copy.FlushAsync(cancellationToken);

                await call.RequestStream.CompleteAsync();

                var response = await call.ResponseAsync;

                run.MarkStreamed(response.RecordCount, response.BaseCount, response.InvalidSymbolCount,
                    response.ChunkProcessed, response.Message);
                if (runNextclade) run.RequestNextclade(request.NextcladeDataset!);
                if (runAmr)
                {
                    run.RequestAmr(organism);
                    // Тип штаму (MLST) рахується для тієї самої збірки паралельно з AMRFinderPlus.
                    run.RequestMlst();
                }
                await db.SaveChangesAsync(cancellationToken);

                if (runNextclade)
                {
                    await _jobs.PublishAsync(JobPublisher.NextcladeQueue, new NextcladeJobMessage
                    {
                        RunId = run.Id,
                        FilePath = inputPath,
                        Dataset = request.NextcladeDataset!
                    });
                }

                if (runAmr)
                {
                    await _jobs.PublishAsync(JobPublisher.AmrFinderQueue, new AmrJobMessage
                    {
                        RunId = run.Id,
                        FilePath = inputPath,
                        Organism = organism
                    });
                    await _jobs.PublishAsync(JobPublisher.MlstQueue, new MlstJobMessage
                    {
                        RunId = run.Id,
                        FilePath = inputPath,
                        Organism = organism
                    });
                }

                return (run, response);
            }
            catch (Exception ex)
            {
                run.MarkFailed(ex.Message);
                await db.SaveChangesAsync(CancellationToken.None);
                throw;
            }
        }
    }
}
