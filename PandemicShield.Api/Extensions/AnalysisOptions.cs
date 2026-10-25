namespace PandemicShield.Api.Extensions
{
    /// <summary>
    /// Налаштування зовнішніх інструментів: які набори даних Nextclade встановлено, для яких видів бактерій
    /// AMRFinderPlus шукає точкові мутації, і куди зберігати файли запусків.
    /// </summary>
    public sealed class AnalysisOptions
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

        // Вихід `amrfinder --list_organisms` для бази 2026-08-07.1.
        public static readonly string[] DefaultOrganisms =
        [
            "Acinetobacter_baumannii", "Bordetella_pertussis", "Burkholderia_cepacia", "Burkholderia_mallei",
            "Burkholderia_pseudomallei", "Campylobacter", "Citrobacter_freundii", "Clostridioides_difficile",
            "Corynebacterium_diphtheriae", "Enterobacter_asburiae", "Enterobacter_cloacae", "Enterococcus_faecalis",
            "Enterococcus_faecium", "Escherichia", "Haemophilus_influenzae", "Helicobacter_pylori", "Klebsiella_oxytoca",
            "Klebsiella_pneumoniae", "Neisseria_gonorrhoeae", "Neisseria_meningitidis", "Pseudomonas_aeruginosa",
            "Salmonella", "Serratia_marcescens", "Staphylococcus_aureus", "Staphylococcus_epidermidis",
            "Staphylococcus_pseudintermedius", "Streptococcus_agalactiae", "Streptococcus_pneumoniae",
            "Streptococcus_pyogenes", "Vibrio_cholerae", "Vibrio_parahaemolyticus", "Vibrio_vulnificus"
        ];

        public IReadOnlyList<string> Datasets { get; init; } = DefaultDatasets;
        public IReadOnlyList<string> Organisms { get; init; } = DefaultOrganisms;

        /// <summary>Спільний том: API пише сюди вхідні файли, воркери Nextclade і AMRFinderPlus їх читають.</summary>
        public string DataDir { get; init; } = "data";

        public static AnalysisOptions FromEnvironment() => new()
        {
            Datasets = List("NEXTCLADE_DATASETS") ?? DefaultDatasets,
            Organisms = List("AMRFINDER_ORGANISMS") ?? DefaultOrganisms,
            DataDir = Environment.GetEnvironmentVariable("DATA_DIR") ?? "data"
        };

        public bool IsKnownDataset(string dataset) => Datasets.Contains(dataset, StringComparer.Ordinal);

        public bool IsKnownOrganism(string organism) => Organisms.Contains(organism, StringComparer.Ordinal);

        public string RunInputPath(Guid runId) => Path.GetFullPath(Path.Combine(DataDir, "runs", runId.ToString(), "input.fasta"));

        private static string[]? List(string variable)
        {
            string? value = Environment.GetEnvironmentVariable(variable);
            return string.IsNullOrWhiteSpace(value) ? null : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }
    }
}
