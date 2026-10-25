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

        /// <summary>Автовибір: набір для кожної послідовності визначає <c>nextclade sort</c> (див. воркер Nextclade).</summary>
        public const string AutoDataset = "auto";

        public bool IsKnownDataset(string dataset) => dataset == AutoDataset || Datasets.Contains(dataset, StringComparer.Ordinal);

        public bool IsKnownOrganism(string organism) => Organisms.Contains(organism, StringComparer.Ordinal);

        public string RunInputPath(Guid runId) => Path.GetFullPath(Path.Combine(DataDir, "runs", runId.ToString(), "input.fasta"));

        /// <summary>Тимчасова тека імпорту NCBI (завантажені архіви й FASTA до того, як вони стануть запусками).</summary>
        public string ImportDir(Guid importId) => Path.GetFullPath(Path.Combine(DataDir, "imports", importId.ToString()));

        /// <summary>
        /// Вид AMRFinderPlus за науковою назвою з NCBI: "Klebsiella pneumoniae subsp. pneumoniae HS11286" → Klebsiella_pneumoniae.
        /// Обирається найдовший збіг, тож "Escherichia coli" стане Escherichia, а Klebsiella oxytoca — Klebsiella_oxytoca.
        /// </summary>
        public string? MatchOrganism(string? scientificName)
        {
            if (string.IsNullOrWhiteSpace(scientificName)) return null;
            string name = scientificName.Trim();
            // AMRFinderPlus аналізує Shigella разом з Escherichia (це одна група видів).
            if (name.StartsWith("Shigella", StringComparison.OrdinalIgnoreCase) && IsKnownOrganism("Escherichia")) return "Escherichia";

            return Organisms
                .Where(o => name.StartsWith(o.Replace('_', ' '), StringComparison.OrdinalIgnoreCase) &&
                            (name.Length == o.Length || name[o.Length] == ' '))
                .OrderByDescending(o => o.Length)
                .FirstOrDefault();
        }

        private static string[]? List(string variable)
        {
            string? value = Environment.GetEnvironmentVariable(variable);
            return string.IsNullOrWhiteSpace(value) ? null : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }
    }
}
