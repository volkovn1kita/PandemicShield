using System.IO.Compression;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PandemicShield.Api.Extensions;
using PandemicShield.Contracts;
using PandemicShield.DataAccess.Data;
using PandemicShield.DataAccess.Entities;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace PandemicShield.Api.Imports
{
    /// <summary>
    /// Виконує масові імпорти з NCBI у фоні: бере завдання з черги import_jobs, завантажує послідовності або збірки
    /// й передає їх у той самий конвеєр, що й ручне завантаження (<see cref="AnalysisSubmitter"/>).
    /// <list type="bullet">
    /// <item>віруси: усі послідовності — один запуск (так Nextclade порівнює їх у одній таблиці);</item>
    /// <item>бактерії: кожна збірка — окремий запуск, бо це окремий ізолят.</item>
    /// </list>
    /// </summary>
    public sealed class ImportWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly AnalysisOptions _options;
        private readonly ILogger<ImportWorker> _logger;

        public ImportWorker(IServiceScopeFactory scopes, AnalysisOptions options, ILogger<ImportWorker> logger)
        {
            _scopes = scopes;
            _options = options;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await MarkInterruptedAsync(stoppingToken);

            string rabbitHost = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost";
            var factory = new ConnectionFactory { HostName = rabbitHost };

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await using var connection = await factory.CreateConnectionAsync(stoppingToken);
                    await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
                    await channel.QueueDeclareAsync(JobPublisher.ImportQueue, durable: true, exclusive: false, autoDelete: false, cancellationToken: stoppingToken);
                    await channel.BasicQosAsync(0, 1, false, stoppingToken);

                    var consumer = new AsyncEventingBasicConsumer(channel);
                    consumer.ReceivedAsync += async (_, ea) =>
                    {
                        // Підтверджуємо одразу: імпорт може тривати довго, а перерваний пакет при перезапуску
                        // позначається як Failed (MarkInterruptedAsync), щоб не завантажувати ті самі дані двічі.
                        await channel.BasicAckAsync(ea.DeliveryTag, false);
                        var job = JsonSerializer.Deserialize<ImportJobMessage>(ea.Body.Span);
                        if (job != null) await ProcessAsync(job, stoppingToken);
                    };

                    await channel.BasicConsumeAsync(JobPublisher.ImportQueue, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);
                    _logger.LogInformation("Імпорт NCBI чекає на завдання...");

                    while (connection.IsOpen && !stoppingToken.IsCancellationRequested)
                        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("RabbitMQ недоступний для імпорту, повтор за 5 с: {Message}", ex.Message);
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }
        }

        private async Task MarkInterruptedAsync(CancellationToken cancellationToken)
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PandemicDbContext>();
            foreach (var batch in await db.Imports.Where(b => b.Status == ImportStatus.Running).ToListAsync(cancellationToken))
                batch.MarkFailed("Перервано: API перезапустився під час імпорту. Запустіть імпорт ще раз.");
            await db.SaveChangesAsync(cancellationToken);
        }

        private async Task ProcessAsync(ImportJobMessage job, CancellationToken cancellationToken)
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PandemicDbContext>();
            var batch = await db.Imports.FindAsync([job.ImportId], cancellationToken);
            if (batch == null || batch.Status != ImportStatus.Queued) return;

            var context = new ImportContext(
                db,
                batch,
                scope.ServiceProvider.GetRequiredService<AnalysisSubmitter>(),
                scope.ServiceProvider.GetRequiredService<NcbiClient>(),
                _options.ImportDir(batch.Id));
            Directory.CreateDirectory(context.WorkDir);

            try
            {
                if (job.ArchivePath != null) await ImportArchiveAsync(context, job.ArchivePath, cancellationToken);
                else await ImportAccessionsAsync(context, cancellationToken);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Імпорт {Id} не вдався", batch.Id);
                batch.MarkFailed(ex.Message);
            }
            finally
            {
                await db.SaveChangesAsync(CancellationToken.None);
                // Файли запусків уже скопійовано в runs/{id}; тимчасові завантаження більше не потрібні.
                try { Directory.Delete(context.WorkDir, recursive: true); } catch (IOException) { }
            }
        }

        private sealed record ImportContext(PandemicDbContext Db, ImportBatchEntity Batch, AnalysisSubmitter Submitter, NcbiClient Ncbi, string WorkDir);

        // ==========================================
        // СПИСОК НОМЕРІВ
        // ==========================================
        private async Task ImportAccessionsAsync(ImportContext c, CancellationToken cancellationToken)
        {
            var parsed = NcbiAccessions.Parse(c.Batch.Accessions);

            if (c.Batch.Category != ThreatCategory.Bacteria)
            {
                await ImportVirusSequencesAsync(c, parsed.Nucleotides, cancellationToken);
                return;
            }

            await ImportIsolatesAsync(c, parsed.All, async (accession, path) =>
            {
                if (NcbiAccessions.IsAssembly(accession))
                {
                    string zipPath = Path.Combine(c.WorkDir, $"{accession}.zip");
                    await c.Ncbi.DownloadAssemblyAsync(accession, zipPath, cancellationToken);
                    using var zip = OpenNcbiZip(zipPath);
                    var assembly = NcbiArchiveReader.ReadAssemblies(zip).FirstOrDefault()
                        ?? throw new InvalidDataException("в архіві NCBI немає геномної послідовності (збірку могли вилучити)");
                    await using (var output = File.Create(path))
                        await NcbiArchiveReader.ConcatenateAsync(assembly.FastaEntries, output, cancellationToken);
                    File.Delete(zipPath);
                    return assembly.OrganismName;
                }

                // Окрема послідовність (наприклад, повна хромосома CP…): вид — з опису запису.
                await using (var output = File.Create(path))
                    await c.Ncbi.FetchNucleotideFastaAsync([accession], output, cancellationToken);
                var header = FastaHeaders.Read(path).FirstOrDefault()
                    ?? throw new InvalidDataException("не знайдено в NCBI Nucleotide");
                return FastaHeaders.Organism(header.Description);
            }, cancellationToken);
        }

        private async Task ImportVirusSequencesAsync(ImportContext c, IReadOnlyList<string> accessions, CancellationToken cancellationToken)
        {
            c.Batch.MarkRunning(accessions.Count);
            c.Batch.MarkProgress(0, 0, "Завантаження послідовностей з NCBI…");
            await c.Db.SaveChangesAsync(cancellationToken);

            string path = Path.Combine(c.WorkDir, "sequences.fasta");
            await using (var output = File.Create(path))
                await c.Ncbi.FetchNucleotideFastaAsync(accessions, output, cancellationToken);

            var found = FastaHeaders.Read(path).Select(h => h.Id).ToList();
            var foundBase = found.Select(NcbiAccessions.WithoutVersion).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var missing = accessions.Where(a => !foundBase.Contains(NcbiAccessions.WithoutVersion(a))).ToList();
            foreach (string accession in missing) c.Batch.AddError($"{accession}: не знайдено в NCBI Nucleotide");

            if (found.Count == 0)
            {
                c.Batch.MarkProgress(0, missing.Count, null);
                c.Batch.MarkFailed("Жодної послідовності не знайдено в NCBI.");
                return;
            }

            await SubmitFileAsync(c, path, FastaHeaders.RunName(found), c.Batch.AmrOrganism, sourceAccession: null, cancellationToken);
            c.Batch.MarkProgress(found.Count, missing.Count, null);
            c.Batch.MarkCompleted($"Імпортовано {found.Count} послідовностей в один запуск" +
                                  (missing.Count > 0 ? $", не знайдено: {missing.Count}" : ""));
        }

        /// <summary>
        /// Бактерії: кожен номер — окремий запуск. Помилка з одним ізолятом не зупиняє інші.
        /// <paramref name="fetch"/> записує FASTA у вказаний шлях і повертає наукову назву виду (якщо відома).
        /// </summary>
        private async Task ImportIsolatesAsync(ImportContext c, IReadOnlyList<string> accessions,
            Func<string, string, Task<string?>> fetch, CancellationToken cancellationToken)
        {
            c.Batch.MarkRunning(accessions.Count);
            await c.Db.SaveChangesAsync(cancellationToken);

            int done = 0, failed = 0;
            foreach (string accession in accessions)
            {
                c.Batch.MarkProgress(done, failed, $"{accession}: завантаження…");
                await c.Db.SaveChangesAsync(cancellationToken);

                string path = Path.Combine(c.WorkDir, $"{accession}.fasta");
                try
                {
                    string? organismName = await fetch(accession, path);
                    string? organism = c.Batch.AmrOrganism ?? _options.MatchOrganism(organismName);
                    string runName = organismName == null ? accession : $"{accession} · {organismName}";
                    await SubmitFileAsync(c, path, runName, organism, accession, cancellationToken);
                    done++;
                }
                catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                {
                    failed++;
                    c.Batch.AddError($"{accession}: {ex.Message}");
                }
                finally
                {
                    File.Delete(path);
                }
            }

            c.Batch.MarkProgress(done, failed, null);
            if (done == 0) c.Batch.MarkFailed("Жодної збірки не вдалося імпортувати.");
            else c.Batch.MarkCompleted($"Імпортовано {done} з {accessions.Count} ізолятів" + (failed > 0 ? $", з помилкою: {failed}" : ""));
        }

        // ==========================================
        // АРХІВ NCBI DATASETS
        // ==========================================
        private async Task ImportArchiveAsync(ImportContext c, string archivePath, CancellationToken cancellationToken)
        {
            using var zip = ZipFile.OpenRead(archivePath);

            if (c.Batch.Category == ThreatCategory.Bacteria)
            {
                var assemblies = NcbiArchiveReader.ReadAssemblies(zip).ToDictionary(a => a.Accession);
                if (assemblies.Count == 0)
                    throw new InvalidDataException("В архіві немає збірок (тек GCA_/GCF_ з *_genomic.fna). Завантажте з NCBI Datasets «Genome» з FASTA.");

                await ImportIsolatesAsync(c, assemblies.Keys.ToList(), async (accession, path) =>
                {
                    await using var output = File.Create(path);
                    await NcbiArchiveReader.ConcatenateAsync(assemblies[accession].FastaEntries, output, cancellationToken);
                    return assemblies[accession].OrganismName;
                }, cancellationToken);
                return;
            }

            var entries = NcbiArchiveReader.GenomicEntries(zip);
            if (entries.Count == 0)
                throw new InvalidDataException("В архіві немає genomic.fna. Завантажте з NCBI Virus / Datasets послідовності у форматі FASTA.");

            c.Batch.MarkRunning(entries.Count);
            await c.Db.SaveChangesAsync(cancellationToken);

            string path = Path.Combine(c.WorkDir, "sequences.fasta");
            await using (var output = File.Create(path))
                await NcbiArchiveReader.ConcatenateAsync(entries, output, cancellationToken);

            int records = FastaHeaders.Read(path).Count;
            await SubmitFileAsync(c, path, c.Batch.ArchiveName ?? "NCBI Datasets", c.Batch.AmrOrganism, null, cancellationToken);
            c.Batch.MarkProgress(entries.Count, 0, null);
            c.Batch.MarkCompleted($"Імпортовано {records} послідовностей з архіву в один запуск");
        }

        // На номер, якого немає, NCBI Datasets відповідає 200 і обірваним ZIP лише з README (без центрального каталогу).
        private static ZipArchive OpenNcbiZip(string path)
        {
            try
            {
                return ZipFile.OpenRead(path);
            }
            catch (InvalidDataException)
            {
                throw new InvalidDataException("NCBI не має збірки з таким номером (архів порожній)");
            }
        }

        private static async Task SubmitFileAsync(ImportContext c, string path, string runName, string? organism, string? sourceAccession,
            CancellationToken cancellationToken)
        {
            await using var input = File.OpenRead(path);
            await c.Submitter.SubmitAsync(c.Db, input,
                new AnalysisSubmitter.Request(runName, c.Batch.Category, c.Batch.NextcladeDataset, organism, c.Batch.Id, sourceAccession),
                cancellationToken);
        }
    }
}
