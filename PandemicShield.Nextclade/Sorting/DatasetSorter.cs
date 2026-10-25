using System.Globalization;

namespace PandemicShield.Nextclade.Sorting
{
    /// <summary>Збіг послідовності з набором даних за <c>nextclade sort</c> (частка спільних мінімізаторів).</summary>
    public sealed record SortMatch(int Index, string SeqName, string Dataset, double Score);

    /// <summary>
    /// Автовибір набору даних: <c>nextclade sort --all-matches</c> порівнює кожну послідовність з індексом мінімізаторів
    /// усіх наборів Nextstrain і повертає всі збіги. Найкращий збіг може бути набором, якого немає в образі
    /// (для SARS-CoV-2 — <c>nextstrain/sars-cov-2/BA.2.86</c>), тож обирається найкращий серед установлених.
    /// </summary>
    public static class DatasetSorter
    {
        /// <summary>Читає TSV <c>index, seqName, dataset, score, numHits</c>; рядки без набору (не розпізнано) пропускаються.</summary>
        public static List<SortMatch> Read(TextReader reader)
        {
            var matches = new List<SortMatch>();
            string? header = reader.ReadLine();
            if (header == null) return matches;

            var columns = header.Split('\t').Select((name, i) => (name, i)).ToDictionary(c => c.name, c => c.i);
            int index = columns["index"], seqName = columns["seqName"], dataset = columns["dataset"], score = columns["score"];

            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                string[] f = line.Split('\t');
                if (f.Length <= score || f[dataset].Length == 0) continue;
                matches.Add(new SortMatch(
                    int.Parse(f[index], CultureInfo.InvariantCulture),
                    f[seqName],
                    f[dataset],
                    double.Parse(f[score], CultureInfo.InvariantCulture)));
            }
            return matches;
        }

        /// <summary>Номер запису у файлі → найкращий установлений набір. Записів, яких немає у словнику, не розпізнано.</summary>
        public static Dictionary<int, SortMatch> Assign(IEnumerable<SortMatch> matches, IReadOnlyCollection<string> installed)
        {
            var set = new HashSet<string>(installed, StringComparer.Ordinal);
            return matches
                .Where(m => set.Contains(m.Dataset))
                .GroupBy(m => m.Index)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(m => m.Score).ThenBy(m => m.Dataset, StringComparer.Ordinal).First());
        }
    }
}
