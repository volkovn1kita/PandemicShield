using System.Globalization;

namespace PandemicShield.AmrFinder.Report
{
    /// <summary>Один рядок звіту AMRFinderPlus. Координати 1-based у контигу.</summary>
    public sealed record AmrHit(
        string ContigId,
        long Start,
        long Stop,
        char Strand,
        string ElementSymbol,
        string ElementName,
        string Scope,
        string Type,
        string Subtype,
        string Class,
        string Subclass,
        string Method,
        double? CoveragePercent,
        double? IdentityPercent,
        string? ClosestReference)
    {
        /// <summary>
        /// Підкласи у звіті бувають складеними ("AMOXICILLIN-CLAVULANIC_ACID/PIPERACILLIN-TAZOBACTAM") —
        /// повертаємо їх окремо, з пробілами замість підкреслень.
        /// </summary>
        public IEnumerable<string> Subclasses => Subclass.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => s.Replace('_', ' '));

        public bool IsPointMutation => Subtype.Equals("POINT", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Читає табличний звіт <c>amrfinder -o report.tsv</c>. Колонки шукаються за назвою в заголовку,
    /// тож підтримуються і назви AMRFinderPlus 4.x ("Element symbol"), і 3.x ("Gene symbol").
    /// </summary>
    public static class AmrReportReader
    {
        private static readonly Dictionary<string, string[]> Aliases = new()
        {
            ["contig"] = ["Contig id"],
            ["start"] = ["Start"],
            ["stop"] = ["Stop"],
            ["strand"] = ["Strand"],
            ["symbol"] = ["Element symbol", "Gene symbol"],
            ["name"] = ["Element name", "Sequence name"],
            ["scope"] = ["Scope"],
            ["type"] = ["Type", "Element type"],
            ["subtype"] = ["Subtype", "Element subtype"],
            ["class"] = ["Class"],
            ["subclass"] = ["Subclass"],
            ["method"] = ["Method"],
            ["coverage"] = ["% Coverage of reference", "% Coverage of reference sequence"],
            ["identity"] = ["% Identity to reference", "% Identity to reference sequence"],
            ["reference"] = ["Closest reference accession", "Accession of closest sequence"],
        };

        public static List<AmrHit> Read(TextReader reader)
        {
            string? header = reader.ReadLine();
            if (header == null) return [];

            string[] columns = header.Split('\t');
            var index = new Dictionary<string, int>();
            foreach (var (key, names) in Aliases)
            {
                int i = Array.FindIndex(columns, c => names.Contains(c.Trim(), StringComparer.OrdinalIgnoreCase));
                if (i >= 0) index[key] = i;
            }

            foreach (string required in new[] { "contig", "start", "stop", "symbol", "class", "subclass" })
            {
                if (!index.ContainsKey(required))
                    throw new FormatException($"У звіті AMRFinderPlus немає колонки «{Aliases[required][0]}».");
            }

            var hits = new List<AmrHit>();
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                string[] f = line.Split('\t');

                string Get(string key) => index.TryGetValue(key, out int i) && i < f.Length && f[i] != "NA" ? f[i] : string.Empty;

                hits.Add(new AmrHit(
                    ContigId: Get("contig"),
                    Start: long.TryParse(Get("start"), out long start) ? start : 0,
                    Stop: long.TryParse(Get("stop"), out long stop) ? stop : 0,
                    Strand: Get("strand") is { Length: 1 } s ? s[0] : '+',
                    ElementSymbol: Get("symbol"),
                    ElementName: Get("name"),
                    Scope: Get("scope"),
                    Type: Get("type"),
                    Subtype: Get("subtype"),
                    Class: Get("class"),
                    Subclass: Get("subclass"),
                    Method: Get("method"),
                    CoveragePercent: Number(Get("coverage")),
                    IdentityPercent: Number(Get("identity")),
                    ClosestReference: Get("reference") is { Length: > 0 } r ? r : null));
            }
            return hits;
        }

        private static double? Number(string value) =>
            double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) ? d : null;
    }
}
