namespace PandemicShield.Api.Extensions
{
    /// <summary>
    /// Налаштування Nextclade для API: які набори даних встановлено у воркері та куди зберігати файли запусків.
    /// </summary>
    public sealed class NextcladeOptions
    {
        public static readonly string[] DefaultDatasets =
        [
            "nextstrain/sars-cov-2/wuhan-hu-1/orfs",
            "nextstrain/flu/h1n1pdm/ha/CY121680",
            "nextstrain/flu/h3n2/ha/EPI1857216",
            "nextstrain/rsv/a/EPI_ISL_412866",
            "nextstrain/rsv/b/EPI_ISL_1653999",
            "nextstrain/mpox/all-clades"
        ];

        public IReadOnlyList<string> Datasets { get; init; } = DefaultDatasets;

        /// <summary>Спільний том: API пише сюди вхідні файли, Nextclade-воркер їх читає.</summary>
        public string DataDir { get; init; } = "data";

        public static NextcladeOptions FromEnvironment()
        {
            string? datasets = Environment.GetEnvironmentVariable("NEXTCLADE_DATASETS");
            return new NextcladeOptions
            {
                Datasets = string.IsNullOrWhiteSpace(datasets)
                    ? DefaultDatasets
                    : datasets.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                DataDir = Environment.GetEnvironmentVariable("DATA_DIR") ?? "data"
            };
        }

        public bool IsKnownDataset(string dataset) => Datasets.Contains(dataset, StringComparer.Ordinal);

        public string RunInputPath(Guid runId) => Path.GetFullPath(Path.Combine(DataDir, "runs", runId.ToString(), "input.fasta"));
    }
}
