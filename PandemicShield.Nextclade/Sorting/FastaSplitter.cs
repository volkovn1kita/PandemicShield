namespace PandemicShield.Nextclade.Sorting
{
    /// <summary>
    /// Розкладає записи FASTA по файлах наборів даних потоком (запис за записом), тож файл з тисячами геномів
    /// не завантажується в пам'ять. Номер запису (з 0) збігається з колонкою <c>index</c> у результатах <c>nextclade sort</c>.
    /// </summary>
    public static class FastaSplitter
    {
        public sealed record SplitResult(IReadOnlyDictionary<string, int> RecordsPerDataset, IReadOnlyList<string> Unassigned);

        /// <param name="writerFor">Потік для набору даних (створюється при першому записі цього набору).</param>
        public static SplitResult Split(TextReader input, IReadOnlyDictionary<int, SortMatch> assignment, Func<string, TextWriter> writerFor)
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            var unassigned = new List<string>();
            var writers = new Dictionary<string, TextWriter>(StringComparer.Ordinal);
            TextWriter? current = null;
            int index = -1;

            string? line;
            while ((line = input.ReadLine()) != null)
            {
                if (line.StartsWith('>'))
                {
                    index++;
                    if (assignment.TryGetValue(index, out var match))
                    {
                        if (!writers.TryGetValue(match.Dataset, out current))
                        {
                            current = writerFor(match.Dataset);
                            writers[match.Dataset] = current;
                        }
                        counts[match.Dataset] = counts.GetValueOrDefault(match.Dataset) + 1;
                    }
                    else
                    {
                        current = null;
                        unassigned.Add(line[1..].Trim());
                    }
                }
                current?.WriteLine(line);
            }

            foreach (var writer in writers.Values) writer.Flush();
            return new SplitResult(counts, unassigned);
        }
    }
}
