using System.Globalization;

namespace PandemicShield.Reads
{
    /// <summary>Статистика прочитань з <c>seqkit stats -T -a</c> (усі файли зразка разом).</summary>
    public sealed record ReadStats(long Reads, long Bases, double AverageLength, long N50, double? Q30Percent)
    {
        /// <summary>Читає TSV seqkit: колонки шукаються за назвою (num_seqs, sum_len, N50, Q30(%)), рядки файлів сумуються.</summary>
        public static ReadStats Parse(TextReader reader)
        {
            string? header = reader.ReadLine() ?? throw new InvalidDataException("seqkit stats нічого не вивів.");
            var col = header.Split('\t').Select((name, i) => (name, i)).ToDictionary(c => c.name, c => c.i);

            long reads = 0, bases = 0, n50 = 0;
            double q30Weighted = 0;
            bool hasQ30 = col.ContainsKey("Q30(%)");
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.Length == 0) continue;
                string[] f = line.Split('\t');
                long fileReads = long.Parse(f[col["num_seqs"]].Replace(",", ""), CultureInfo.InvariantCulture);
                long fileBases = long.Parse(f[col["sum_len"]].Replace(",", ""), CultureInfo.InvariantCulture);
                reads += fileReads;
                bases += fileBases;
                if (col.TryGetValue("N50", out int n50Col))
                    n50 = Math.Max(n50, long.Parse(f[n50Col].Replace(",", ""), CultureInfo.InvariantCulture));
                if (hasQ30) q30Weighted += fileBases * double.Parse(f[col["Q30(%)"]], CultureInfo.InvariantCulture);
            }
            return new ReadStats(reads, bases, reads == 0 ? 0 : (double)bases / reads, n50,
                hasQ30 && bases > 0 ? q30Weighted / bases : null);
        }
    }

    /// <summary>Референс з панелі вірусів: набір Nextclade і назва послідовності в об'єднаному FASTA.</summary>
    public sealed record PanelReference(string Dataset, string SequenceName);

    /// <summary>
    /// Вибір вірусу за картуванням: прочитання картуються одразу на референси всіх наборів, і виграє той,
    /// на який лягло найбільше прочитань (<c>samtools idxstats</c>: назва, довжина, картовані, некартовані).
    /// </summary>
    public static class ReferencePanel
    {
        /// <summary>references.tsv з образу: набір даних і назва послідовності референсу, через табуляцію.</summary>
        public static List<PanelReference> Read(TextReader reader)
        {
            var references = new List<PanelReference>();
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                string[] f = line.Split('\t');
                if (f.Length >= 2 && f[0].Length > 0) references.Add(new PanelReference(f[0], f[1]));
            }
            return references;
        }

        public static Dictionary<string, long> ReadIdxstats(TextReader reader)
        {
            var mapped = new Dictionary<string, long>(StringComparer.Ordinal);
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                string[] f = line.Split('\t');
                if (f.Length >= 3 && f[0] != "*") mapped[f[0]] = long.Parse(f[2], CultureInfo.InvariantCulture);
            }
            return mapped;
        }

        /// <summary>Референс з найбільшою кількістю картованих прочитань або заданий набір, якщо його обрано явно.</summary>
        public static (PanelReference Reference, long Mapped) Choose(IReadOnlyList<PanelReference> panel, IReadOnlyDictionary<string, long> mapped,
            string? dataset)
        {
            var candidates = dataset is null or "auto" ? panel : panel.Where(r => r.Dataset == dataset).ToList();
            if (candidates.Count == 0)
                throw new InvalidOperationException($"Референсу для набору «{dataset}» немає в образі воркера Reads.");
            return candidates
                .Select(r => (r, mapped.GetValueOrDefault(r.SequenceName)))
                .OrderByDescending(x => x.Item2)
                .First();
        }
    }

    /// <summary>Глибина покриття з <c>samtools depth -a</c>: середня й частка геному з глибиною не нижче порогу.</summary>
    public sealed record DepthSummary(long Length, double MeanDepth, double Breadth)
    {
        public static DepthSummary Read(TextReader reader, int minDepth)
        {
            long positions = 0, covered = 0;
            double total = 0;
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                int tab = line.LastIndexOf('\t');
                if (tab < 0) continue;
                int depth = int.Parse(line.AsSpan(tab + 1), CultureInfo.InvariantCulture);
                positions++;
                total += depth;
                if (depth >= minDepth) covered++;
            }
            return new DepthSummary(positions, positions == 0 ? 0 : total / positions, positions == 0 ? 0 : (double)covered / positions);
        }
    }

    /// <summary>Підсумок збірки: кількість контигів, загальна довжина, N50, найдовший контиг.</summary>
    public sealed record AssemblyStats(int Contigs, long TotalLength, long N50, long Largest)
    {
        public static AssemblyStats FromLengths(IEnumerable<long> lengths)
        {
            var sorted = lengths.Where(l => l > 0).OrderByDescending(l => l).ToList();
            long total = sorted.Sum(), running = 0, n50 = 0;
            foreach (long length in sorted)
            {
                running += length;
                if (running * 2 >= total)
                {
                    n50 = length;
                    break;
                }
            }
            return new AssemblyStats(sorted.Count, total, n50, sorted.FirstOrDefault());
        }

        /// <summary>Довжини записів FASTA (без заголовків і переносів).</summary>
        public static AssemblyStats FromFasta(TextReader reader)
        {
            var lengths = new List<long>();
            long current = -1;
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.StartsWith('>'))
                {
                    if (current >= 0) lengths.Add(current);
                    current = 0;
                }
                else if (current >= 0)
                {
                    current += line.Trim().Length;
                }
            }
            if (current >= 0) lengths.Add(current);
            return FromLengths(lengths);
        }
    }

    /// <summary>Частка невизначених нуклеотидів (N) у консенсусі: там, де глибина нижча за поріг.</summary>
    public static class ConsensusStats
    {
        public static (long Length, double NFraction) Read(TextReader reader)
        {
            long length = 0, n = 0;
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.StartsWith('>')) continue;
                foreach (char c in line)
                {
                    if (char.IsWhiteSpace(c)) continue;
                    length++;
                    if (c is 'N' or 'n') n++;
                }
            }
            return (length, length == 0 ? 1 : (double)n / length);
        }
    }

    public static class ReadsNaming
    {
        /// <summary>Назва зразка з імені файлу: "KP-12_S3_L001_R1_001.fastq.gz" → "KP-12_S3_L001".</summary>
        public static string SampleName(IReadOnlyList<string> files)
        {
            string name = Path.GetFileName(files[0]);
            foreach (string ext in new[] { ".gz", ".fastq", ".fq" })
                if (name.EndsWith(ext, StringComparison.OrdinalIgnoreCase)) name = name[..^ext.Length];
            foreach (string suffix in new[] { "_R1_001", "_R1", "_1", ".R1", "_001" })
                if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) return name[..^suffix.Length];
            return name;
        }
    }
}
