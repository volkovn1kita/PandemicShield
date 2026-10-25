using System.Text.RegularExpressions;

namespace PandemicShield.Api.Imports
{
    /// <summary>
    /// Номери NCBI зі списку, який вставив користувач: збірки геномів (GCA_/GCF_, бактерії) і окремі послідовності
    /// GenBank/RefSeq (OQ291490.1, MN908947.3, NC_045512.2, CP012345.1).
    /// </summary>
    public static partial class NcbiAccessions
    {
        public const int MaxPerImport = 500;

        public sealed record Parsed(IReadOnlyList<string> Assemblies, IReadOnlyList<string> Nucleotides, IReadOnlyList<string> Invalid)
        {
            public int Total => Assemblies.Count + Nucleotides.Count;

            /// <summary>Усі коректні номери в порядку, в якому їх вставили.</summary>
            public IReadOnlyList<string> All { get; init; } = [];
        }

        /// <summary>Розбирає список: роздільники — пробіли, переноси рядків, коми, крапки з комою; повтори прибираються.</summary>
        public static Parsed Parse(string? text)
        {
            var assemblies = new List<string>();
            var nucleotides = new List<string>();
            var invalid = new List<string>();
            var all = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (string token in (text ?? string.Empty).Split([' ', '\t', '\r', '\n', ',', ';'], StringSplitOptions.RemoveEmptyEntries))
            {
                string accession = token.Trim().ToUpperInvariant();
                if (!seen.Add(accession)) continue;

                if (IsAssembly(accession))
                {
                    assemblies.Add(accession);
                    all.Add(accession);
                }
                else if (NucleotideRegex().IsMatch(accession))
                {
                    nucleotides.Add(accession);
                    all.Add(accession);
                }
                else
                {
                    invalid.Add(token.Trim());
                }
            }

            return new Parsed(assemblies, nucleotides, invalid) { All = all };
        }

        public static bool IsAssembly(string accession) => AssemblyRegex().IsMatch(accession);

        /// <summary>Номер без версії: OQ291490.1 → OQ291490. NCBI може повернути іншу версію, ніж попросили.</summary>
        public static string WithoutVersion(string accession)
        {
            int dot = accession.LastIndexOf('.');
            return dot > 0 ? accession[..dot] : accession;
        }

        [GeneratedRegex(@"^GC[AF]_\d{9}(\.\d+)?$")]
        private static partial Regex AssemblyRegex();

        // GenBank: 1–2 літери + 5–6 цифр, WGS: 4–6 літер + 8–10 цифр; RefSeq має префікс NC_, NZ_, NW_ тощо.
        [GeneratedRegex(@"^(?:[A-Z]{2}_)?(?:[A-Z]{1,2}\d{5,8}|[A-Z]{4,6}\d{8,10}|\d{6,9})(\.\d+)?$")]
        private static partial Regex NucleotideRegex();
    }
}
