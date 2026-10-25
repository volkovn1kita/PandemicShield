using System.Text.RegularExpressions;

namespace PandemicShield.AmrFinder.Surveillance
{
    /// <summary>Рядок <c>amr.metadata.tsv</c> NCBI Pathogen Detection: один ізолят.</summary>
    public sealed record IsolateRow(
        string TargetAccession,
        string? AssemblyAccession,
        string? BioSample,
        string? Organism,
        string? Strain,
        string Country,
        string? Region,
        string? CollectionDate,
        int? CollectionYear,
        string? IsolationSource,
        string? Host,
        string? AmrGenotypes,
        IReadOnlyList<string> Genotypes,
        string? AmrFinderVersion,
        string? DatabaseVersion);

    /// <summary>
    /// Читає <c>PDG….amr.metadata.tsv</c> рядок за рядком (файл Klebsiella — ~185 МБ, Salmonella — ~830 МБ),
    /// тож пам'ять не залежить від розміру файлу. Повертає лише ізоляти з обраних країн.
    /// Відсутні значення NCBI пише як <c>NULL</c>.
    /// </summary>
    public static partial class PathogenDetectionReader
    {
        public static IEnumerable<IsolateRow> Read(TextReader reader, ICollection<string> countries, Action<int>? onRow = null)
        {
            string? header = reader.ReadLine();
            if (header == null) yield break;

            var columns = header.TrimStart('#').Split('\t')
                .Select((name, index) => (name, index))
                .ToDictionary(c => c.name, c => c.index, StringComparer.Ordinal);
            foreach (string required in new[] { "target_acc", "geo_loc_name", "AMR_genotypes" })
                if (!columns.ContainsKey(required))
                    throw new InvalidDataException($"У файлі Pathogen Detection немає колонки {required}.");

            var wanted = new HashSet<string>(countries, StringComparer.OrdinalIgnoreCase);
            int rows = 0;
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                onRow?.Invoke(++rows);
                string[] f = line.Split('\t');

                string? geo = Get(f, columns, "geo_loc_name");
                if (geo == null) continue;
                var (country, region) = SplitGeo(geo);
                if (!wanted.Contains(country)) continue;

                string? genotypes = Unquote(Get(f, columns, "AMR_genotypes"));
                string? date = Get(f, columns, "collection_date");

                yield return new IsolateRow(
                    Get(f, columns, "target_acc") ?? string.Empty,
                    Get(f, columns, "asm_acc"),
                    Get(f, columns, "biosample_acc"),
                    Get(f, columns, "scientific_name"),
                    Get(f, columns, "strain"),
                    country,
                    region,
                    date,
                    Year(date),
                    Get(f, columns, "isolation_source"),
                    Get(f, columns, "host"),
                    genotypes,
                    genotypes == null ? [] : genotypes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                    Get(f, columns, "amrfinder_version"),
                    Get(f, columns, "refgene_db_version"));
            }
        }

        /// <summary>"Ukraine: Vinnitsa" → (Ukraine, Vinnitsa); "Ukraine" → (Ukraine, null).</summary>
        public static (string Country, string? Region) SplitGeo(string geo)
        {
            int colon = geo.IndexOf(':');
            if (colon < 0) return (geo.Trim(), null);
            string region = geo[(colon + 1)..].Trim();
            return (geo[..colon].Trim(), region.Length == 0 ? null : region);
        }

        /// <summary>Рік з дати збору: "2017-10-03", "2015", "2022/2023" → 2017, 2015, 2022; "missing" → null.</summary>
        public static int? Year(string? date)
        {
            if (date == null) return null;
            var match = YearRegex().Match(date);
            return match.Success ? int.Parse(match.Value) : null;
        }

        private static string? Get(string[] fields, Dictionary<string, int> columns, string name)
        {
            if (!columns.TryGetValue(name, out int index) || index >= fields.Length) return null;
            string value = fields[index].Trim();
            return value.Length == 0 || value == "NULL" ? null : value;
        }

        private static string? Unquote(string? value) => value is { Length: >= 2 } && value[0] == '"' && value[^1] == '"' ? value[1..^1] : value;

        [GeneratedRegex(@"\b(19|20)\d{2}\b")]
        private static partial Regex YearRegex();
    }
}
