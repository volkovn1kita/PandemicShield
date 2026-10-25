using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PandemicShield.Contracts.Events;
using PandemicShield.DataAccess.Data;
using PandemicShield.DataAccess.Entities;

namespace PandemicShield.AmrFinder.Surveillance
{
    /// <summary>
    /// Оновлює дані нагляду: знаходить найновіший <c>amr.metadata.tsv</c> групи бактерій на FTP NCBI Pathogen Detection,
    /// читає його потоком, лишає ізоляти з обраних країн, класифікує їхні гени каталогом AMRFinderPlus і замінює
    /// попередні дані цієї групи в БД.
    /// </summary>
    public sealed partial class SurveillanceSync
    {
        private readonly HttpClient _http;
        private readonly string _baseUrl;
        private readonly Func<Task<AmrCatalog>> _catalog;

        public SurveillanceSync(HttpClient http, Func<Task<AmrCatalog>> catalog)
        {
            _http = http;
            _catalog = catalog;
            _baseUrl = (Environment.GetEnvironmentVariable("PATHOGEN_DETECTION_URL") ?? "https://ftp.ncbi.nlm.nih.gov/pathogen/Results").TrimEnd('/');
        }

        public async Task RunAsync(Guid syncId, CancellationToken cancellationToken = default)
        {
            SurveillanceSyncEntity? sync;
            using (var db = new PandemicDbContext())
            {
                sync = await db.SurveillanceSyncs.FindAsync([syncId], cancellationToken);
                if (sync == null || sync.Status != ImportStatus.Queued) return;
            }

            try
            {
                await SyncAsync(sync, cancellationToken);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                Console.WriteLine($"Нагляд {sync.Taxgroup}: помилка: {ex.Message}");
                await UpdateAsync(syncId, s => s.MarkFailed(ex.Message));
            }
        }

        private async Task SyncAsync(SurveillanceSyncEntity sync, CancellationToken cancellationToken)
        {
            var catalog = await _catalog();
            var (url, pdgVersion) = await ResolveLatestAsync(sync.Taxgroup, cancellationToken);
            Console.WriteLine($"Нагляд {sync.Taxgroup}: {url}");

            using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            long total = response.Content.Headers.ContentLength ?? 0;
            await UpdateAsync(sync.Id, s => s.MarkRunning(pdgVersion, total));

            await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
            var counting = new CountingStream(body);
            using var reader = new StreamReader(counting);

            var countries = sync.Countries.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var isolates = new List<SurveillanceIsolateEntity>();
            var genes = new List<SurveillanceGeneEntity>();
            int rows = 0, found = 0;

            // Збіги з обраною країною рідкісні (у файлі на сотні мегабайт — кількасот рядків), тож прогрес
            // оновлюється за таймером, а не на кожній знахідці.
            using var stopProgress = new CancellationTokenSource();
            var progress = Task.Run(async () =>
            {
                while (!stopProgress.IsCancellationRequested)
                {
                    try { await Task.Delay(TimeSpan.FromSeconds(3), stopProgress.Token); }
                    catch (OperationCanceledException) { return; }
                    long read = counting.BytesRead;
                    int scanned = Volatile.Read(ref rows), stored = Volatile.Read(ref found);
                    await UpdateAsync(sync.Id, s => s.MarkProgress(read, scanned, stored));
                }
            });

            try
            {
                foreach (var row in PathogenDetectionReader.Read(reader, countries, n => Volatile.Write(ref rows, n)))
                {
                    var isolate = new SurveillanceIsolateEntity(
                        sync.Taxgroup, pdgVersion, row.TargetAccession, row.AssemblyAccession, row.BioSample, row.Organism, row.Strain,
                        row.Country, row.Region, row.CollectionDate, row.CollectionYear, row.IsolationSource, row.Host,
                        row.AmrGenotypes, row.Genotypes.Count, row.AmrFinderVersion, row.DatabaseVersion);
                    isolates.Add(isolate);
                    foreach (string token in row.Genotypes)
                    {
                        var call = catalog.Classify(token);
                        genes.Add(new SurveillanceGeneEntity(isolate.Id, call.Symbol, call.Family, call.Qualifier, call.Class, call.Subclass));
                    }
                    Interlocked.Increment(ref found);
                }
            }
            finally
            {
                stopProgress.Cancel();
                await progress;
            }

            using (var db = new PandemicDbContext())
            {
                await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
                // Гени видаляються каскадно разом з ізолятами.
                await db.SurveillanceIsolates.Where(i => i.Taxgroup == sync.Taxgroup).ExecuteDeleteAsync(cancellationToken);
                db.SurveillanceIsolates.AddRange(isolates);
                db.SurveillanceGenes.AddRange(genes);
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }

            string message = $"Переглянуто {rows:N0} ізолятів, збережено {isolates.Count} ({string.Join(", ", countries)})";
            await UpdateAsync(sync.Id, s => s.MarkCompleted(isolates.Count, message));
            Console.WriteLine($"Нагляд {sync.Taxgroup}: {message}");
        }

        /// <summary>Найновіша вибірка групи: у теці latest_snps/AMR лежить один файл PDG….amr.metadata.tsv.</summary>
        public async Task<(string Url, string PdgVersion)> ResolveLatestAsync(string taxgroup, CancellationToken cancellationToken)
        {
            string directory = $"{_baseUrl}/{Uri.EscapeDataString(taxgroup)}/latest_snps/AMR/";
            string listing = await _http.GetStringAsync(directory, cancellationToken);
            var match = AmrMetadataRegex().Match(listing);
            if (!match.Success)
                throw new InvalidDataException($"На NCBI немає файлу amr.metadata.tsv для групи {taxgroup}.");
            return (directory + match.Value, match.Groups["pdg"].Value);
        }

        private static async Task UpdateAsync(Guid syncId, Action<SurveillanceSyncEntity> update)
        {
            using var db = new PandemicDbContext();
            var sync = await db.SurveillanceSyncs.FindAsync(syncId);
            if (sync == null) return;
            update(sync);
            await db.SaveChangesAsync();
            await Changes.NotifyAsync(ChangeTopics.Surveillance);
        }

        private static readonly ChangeNotifier Changes = new();

        [GeneratedRegex(@"(?<pdg>PDG\d+\.\d+)\.amr\.metadata\.tsv")]
        private static partial Regex AmrMetadataRegex();

        /// <summary>Рахує прочитані байти, щоб показати прогрес завантаження файлу на сотні мегабайт.</summary>
        private sealed class CountingStream(Stream inner) : Stream
        {
            public long BytesRead { get; private set; }

            public override int Read(byte[] buffer, int offset, int count)
            {
                int read = inner.Read(buffer, offset, count);
                BytesRead += read;
                return read;
            }

            public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                int read = await inner.ReadAsync(buffer, cancellationToken);
                BytesRead += read;
                return read;
            }

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => BytesRead; set => throw new NotSupportedException(); }
            public override void Flush() { }
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
    }
}
