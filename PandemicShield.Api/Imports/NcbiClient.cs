namespace PandemicShield.Api.Imports
{
    /// <summary>
    /// Завантаження відкритих даних NCBI:
    /// <list type="bullet">
    /// <item>послідовності за номером — E-utilities efetch (FASTA), пачками до 200 номерів;</item>
    /// <item>збірки геномів — NCBI Datasets API v2 (ZIP з FASTA і звітом про збірку).</item>
    /// </list>
    /// Без ключа NCBI дозволяє 3 запити на секунду, з ключем (<c>NCBI_API_KEY</c>) — 10.
    /// </summary>
    public sealed class NcbiClient
    {
        private const int EfetchBatchSize = 200;

        private static readonly SemaphoreSlim Throttle = new(1, 1);
        private static DateTime _lastRequest = DateTime.MinValue;

        private readonly HttpClient _http;
        private readonly string? _apiKey;
        private readonly string _eutilsUrl;
        private readonly string _datasetsUrl;
        private readonly TimeSpan _minInterval;

        public NcbiClient(HttpClient http)
        {
            _http = http;
            _apiKey = Environment.GetEnvironmentVariable("NCBI_API_KEY") is { Length: > 0 } key ? key : null;
            _eutilsUrl = (Environment.GetEnvironmentVariable("NCBI_EUTILS_URL") ?? "https://eutils.ncbi.nlm.nih.gov/entrez/eutils").TrimEnd('/');
            _datasetsUrl = (Environment.GetEnvironmentVariable("NCBI_DATASETS_URL") ?? "https://api.ncbi.nlm.nih.gov/datasets/v2").TrimEnd('/');
            _minInterval = TimeSpan.FromMilliseconds(_apiKey == null ? 350 : 110);
        }

        /// <summary>Записує FASTA послідовностей у потік. Номери, яких немає в NCBI, efetch мовчки пропускає.</summary>
        public async Task FetchNucleotideFastaAsync(IReadOnlyList<string> accessions, Stream output, CancellationToken cancellationToken)
        {
            foreach (var batch in accessions.Chunk(EfetchBatchSize))
            {
                var form = new Dictionary<string, string>
                {
                    ["db"] = "nuccore",
                    ["id"] = string.Join(',', batch),
                    ["rettype"] = "fasta",
                    ["retmode"] = "text",
                    ["tool"] = "PandemicShield"
                };
                if (_apiKey != null) form["api_key"] = _apiKey;

                // POST, бо 200 номерів не влазять в адресу запиту.
                using var request = new HttpRequestMessage(HttpMethod.Post, $"{_eutilsUrl}/efetch.fcgi") { Content = new FormUrlEncodedContent(form) };
                using var response = await SendAsync(request, cancellationToken);
                await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
                await body.CopyToAsync(output, cancellationToken);
            }
        }

        /// <summary>Завантажує архів збірки (геномна FASTA + звіт із видом) у файл.</summary>
        public async Task DownloadAssemblyAsync(string accession, string zipPath, CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"{_datasetsUrl}/genome/accession/{Uri.EscapeDataString(accession)}/download?include_annotation_type=GENOME_FASTA");
            if (_apiKey != null) request.Headers.Add("api-key", _apiKey);

            using var response = await SendAsync(request, cancellationToken);
            await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var file = File.Create(zipPath);
            await body.CopyToAsync(file, cancellationToken);
        }

        private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Throttle.WaitAsync(cancellationToken);
            try
            {
                var wait = _lastRequest + _minInterval - DateTime.UtcNow;
                if (wait > TimeSpan.Zero) await Task.Delay(wait, cancellationToken);
                _lastRequest = DateTime.UtcNow;
            }
            finally
            {
                Throttle.Release();
            }

            var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                string body = await response.Content.ReadAsStringAsync(cancellationToken);
                response.Dispose();
                throw new HttpRequestException(
                    $"NCBI відповів {(int)response.StatusCode}: {(body.Length > 300 ? body[..300] : body).Trim()}", null, response.StatusCode);
            }
            return response;
        }
    }
}
