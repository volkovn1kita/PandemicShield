using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;

namespace PandemicShield.Reads
{
    /// <summary>Готова FASTA з прочитань: консенсус вірусу або збірка бактерії, і що про неї сказати користувачу.</summary>
    public sealed record ReadsResult(string FastaPath, string? Dataset, string Summary);

    /// <summary>
    /// Від сирих прочитань (FASTQ) до FASTA, яку далі аналізують Nextclade, AMRFinderPlus і MLST.
    /// <list type="bullet">
    /// <item>Віруси: minimap2 картує прочитання на референси всіх наборів Nextclade одразу; вірус — той, на який лягло
    /// найбільше прочитань; <c>samtools consensus</c> будує консенсус (N там, де глибина нижча за 10).</item>
    /// <item>Бактерії, Illumina: fastp обрізає адаптери й погані кінці, SKESA збирає контиги — тим самим асемблером
    /// NCBI збирає ізоляти Pathogen Detection.</item>
    /// <item>Бактерії, Nanopore: Flye збирає довгі прочитання (часто до повних хромосом і плазмід), Racon полірує збірку.</item>
    /// </list>
    /// </summary>
    public sealed class ReadsPipeline
    {
        public const int MinDepth = 10;
        private const int MinMappedReads = 100;

        /// <summary>
        /// Проходи Racon після Flye. Перший прибирає більшість помилок довгих прочитань, другий — залишки;
        /// далі виграш мізерний, а час росте.
        /// </summary>
        public const int RaconRounds = 2;

        private readonly string _referencesDir;
        private readonly int _threads;
        private readonly int _memoryGb;

        public ReadsPipeline(string referencesDir, int threads, int memoryGb)
        {
            _referencesDir = referencesDir;
            _threads = threads;
            _memoryGb = memoryGb;
        }

        public static ReadsPipeline FromEnvironment() => new(
            Environment.GetEnvironmentVariable("READS_REFERENCES_DIR") ?? "/references",
            int.TryParse(Environment.GetEnvironmentVariable("READS_THREADS"), out int t) && t > 0 ? t : Math.Max(1, Environment.ProcessorCount - 1),
            int.TryParse(Environment.GetEnvironmentVariable("READS_MEMORY_GB"), out int m) && m > 0 ? m : 8);

        public async Task<ReadsResult> RunAsync(IReadOnlyList<string> files, string platform, bool bacterial, string? dataset, string sampleName,
            string workDir, Func<string, Task> progress)
        {
            Directory.CreateDirectory(workDir);
            await progress("Статистика прочитань…");
            var stats = await StatsAsync(files, workDir);
            if (stats.Reads == 0) throw new InvalidDataException("У файлах немає жодного прочитання FASTQ.");

            string platformText = platform == "nanopore" ? "Nanopore" : files.Count == 2 ? "Illumina 2 файли (R1/R2)" : "Illumina";
            string readsText = $"{platformText}: {Number(stats.Reads)} прочитань, {Bases(stats.Bases)}, середня довжина {stats.AverageLength:0} нт" +
                               (stats.Q30Percent is double q30 ? $", Q30 {q30:0}%" : "");

            return bacterial
                ? await AssembleAsync(files, platform, sampleName, workDir, readsText, progress)
                : await ConsensusAsync(files, platform, dataset, sampleName, workDir, stats, readsText, progress);
        }

        // ==========================================
        // ВІРУСИ: картування → консенсус
        // ==========================================
        private async Task<ReadsResult> ConsensusAsync(IReadOnlyList<string> files, string platform, string? dataset, string sampleName,
            string workDir, ReadStats stats, string readsText, Func<string, Task> progress)
        {
            List<PanelReference> panel;
            using (var reader = new StreamReader(Path.Combine(_referencesDir, "references.tsv"))) panel = ReferencePanel.Read(reader);
            string all = Path.Combine(_referencesDir, "all.fasta");
            string bam = Path.Combine(workDir, "aligned.bam");

            await progress("Картування прочитань на референси вірусів (minimap2)…");
            string preset = platform == "nanopore" ? "map-ont" : "sr";
            await ShellAsync($"minimap2 -ax {preset} -t {_threads} {Q(all)} {string.Join(' ', files.Select(Q))} 2> {Q(Path.Combine(workDir, "minimap2.log"))}" +
                             $" | samtools sort -@ {_threads} -o {Q(bam)} - && samtools index {Q(bam)}");

            // Лише первинні вирівнювання (-F 0x904: без некартованих, вторинних і додаткових), інакше одне прочитання рахувалося б двічі.
            var mapped = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach (var r in panel)
            {
                var (_, count) = await RunAsync("samtools", ["view", "-c", "-F", "0x904", bam, r.SequenceName]);
                mapped[r.SequenceName] = long.Parse(count.Trim(), CultureInfo.InvariantCulture);
            }
            var (reference, mappedReads) = ReferencePanel.Choose(panel, mapped, dataset);
            if (mappedReads < MinMappedReads)
                throw new InvalidDataException("Прочитання не схожі на жоден вірус установлених наборів Nextclade " +
                                               $"(найбільше — {mappedReads} прочитань на {reference.Dataset}).");

            await progress($"Консенсус ({reference.Dataset})…");
            string depthFile = Path.Combine(workDir, "depth.tsv");
            await ShellAsync($"samtools depth -a -r {Q(reference.SequenceName)} {Q(bam)} > {Q(depthFile)}");
            DepthSummary depth;
            using (var reader = new StreamReader(depthFile)) depth = DepthSummary.Read(reader, MinDepth);

            string raw = Path.Combine(workDir, "consensus.raw.fasta");
            // Nanopore: проста більшість і без вставок — шум довгих прочитань переважно вставки й делеції, а на малій глибині
            // samtools лишав би довжину вставки, заповнену N; справжні вставки у вірусних геномах рідкісні.
            // Illumina: байєсівська модель samtools, вставки показуються.
            bool nanopore = platform == "nanopore";
            var args = new List<string> { "consensus", "-a", "-r", reference.SequenceName, "--show-ins", nanopore ? "no" : "yes", "--show-del", "no",
                "-d", MinDepth.ToString() };
            if (nanopore) args.AddRange(["-m", "simple", "-c", "0.6"]);
            args.AddRange(["-f", "fasta", "-o", raw, bam]);
            await RunAsync("samtools", args);

            string fasta = Path.Combine(workDir, "consensus.fasta");
            await RenameRecordAsync(raw, fasta, sampleName);
            (long length, double nFraction) consensus;
            using (var reader = new StreamReader(fasta)) consensus = ConsensusStats.Read(reader);

            string summary = $"{readsText}. Картовано на {reference.Dataset}: {Number(mappedReads)} прочитань " +
                             $"({Percent(mappedReads, Math.Max(stats.Reads, 1))} усіх), середня глибина {depth.MeanDepth:0}×, " +
                             $"покрито ≥{MinDepth}× {depth.Breadth * 100:0.0}% референсу. Консенсус: {Number(consensus.length)} нт, N {consensus.nFraction * 100:0.0}%";
            return new ReadsResult(fasta, reference.Dataset, summary);
        }

        // ==========================================
        // БАКТЕРІЇ: збирання геному
        // ==========================================
        private async Task<ReadsResult> AssembleAsync(IReadOnlyList<string> files, string platform, string sampleName, string workDir,
            string readsText, Func<string, Task> progress)
        {
            string contigs;
            string assembler;
            if (platform == "nanopore")
            {
                await progress("Збирання геному з довгих прочитань (Flye)…");
                string outDir = Path.Combine(workDir, "flye");
                await RunAsync("flye", ["--nano-hq", .. files, "--out-dir", outDir, "--threads", _threads.ToString()]);
                string flyeContigs = Path.Combine(outDir, "assembly.fasta");
                if (!File.Exists(flyeContigs) || new FileInfo(flyeContigs).Length == 0)
                    throw new InvalidDataException("Flye не зібрав жодного контигу: замало прочитань або вони погані.");
                contigs = await PolishAsync(flyeContigs, files, workDir, progress);
                assembler = $"Flye + Racon ×{RaconRounds}";
            }
            else
            {
                await progress("Обрізання адаптерів і поганих кінців (fastp)…");
                string t1 = Path.Combine(workDir, "trimmed_1.fastq"), t2 = Path.Combine(workDir, "trimmed_2.fastq");
                var fastp = new List<string> { "-i", files[0], "-o", t1 };
                if (files.Count > 1) fastp.AddRange(["-I", files[1], "-O", t2]);
                fastp.AddRange(["-w", Math.Min(_threads, 16).ToString(), "-j", Path.Combine(workDir, "fastp.json"), "-h", Path.Combine(workDir, "fastp.html")]);
                await RunAsync("fastp", fastp);

                await progress("Збирання геному (SKESA)…");
                contigs = Path.Combine(workDir, "skesa.fasta");
                var skesa = new List<string> { "--reads", files.Count > 1 ? $"{t1},{t2}" : t1 };
                if (files.Count > 1) skesa.Add("--use_paired_ends");
                skesa.AddRange(["--cores", _threads.ToString(), "--memory", _memoryGb.ToString(), "--min_contig", "300", "--contigs_out", contigs]);
                await RunAsync("skesa", skesa);
                assembler = "SKESA";
            }

            if (!File.Exists(contigs) || new FileInfo(contigs).Length == 0)
                throw new InvalidDataException($"{assembler} не зібрав жодного контигу: замало прочитань або вони погані.");

            AssemblyStats stats;
            using (var reader = new StreamReader(contigs)) stats = AssemblyStats.FromFasta(reader);
            string summary = $"{readsText}. Збірка {assembler}: {Number(stats.Contigs)} контигів, {Bases(stats.TotalLength)}, " +
                             $"N50 {Bases(stats.N50)}, найдовший {Bases(stats.Largest)}";
            return new ReadsResult(contigs, null, summary);
        }

        /// <summary>
        /// Полірування Racon: прочитання знову вирівнюються на збірку (minimap2), і в кожній позиції лишається варіант,
        /// який підтримує більшість прочитань. Так виправляються заміни, вставки й пропуски, які Flye переніс з довгих
        /// прочитань у збірку (найчастіше — у повторах однієї літери), і гени перестають обриватися зайвим стоп-кодоном.
        /// </summary>
        private async Task<string> PolishAsync(string assembly, IReadOnlyList<string> files, string workDir, Func<string, Task> progress)
        {
            // Racon читає прочитання з одного файлу; кілька gzip-файлів, склеєних підряд, — теж коректний gzip.
            string reads = files[0];
            if (files.Count > 1)
            {
                reads = Path.Combine(workDir, files[0].EndsWith(".gz", StringComparison.OrdinalIgnoreCase) ? "reads.fastq.gz" : "reads.fastq");
                await ShellAsync($"cat {string.Join(' ', files.Select(Q))} > {Q(reads)}");
            }

            string current = assembly;
            string log = Path.Combine(workDir, "racon.log");
            for (int round = 1; round <= RaconRounds; round++)
            {
                await progress($"Полірування збірки (Racon), прохід {round} з {RaconRounds}…");
                string paf = Path.Combine(workDir, $"racon{round}.paf");
                string polished = Path.Combine(workDir, $"racon{round}.fasta");
                await ShellAsync($"minimap2 -x map-ont -t {_threads} {Q(current)} {Q(reads)} > {Q(paf)} 2>> {Q(log)}");
                // -u: контиг без жодного вирівняного прочитання лишається як є, а не зникає зі збірки.
                await ShellAsync($"racon -t {_threads} -u {Q(reads)} {Q(paf)} {Q(current)} > {Q(polished)} 2>> {Q(log)}");
                current = polished;
            }
            return current;
        }

        // ==========================================
        // ІНСТРУМЕНТИ
        // ==========================================
        private async Task<ReadStats> StatsAsync(IReadOnlyList<string> files, string workDir)
        {
            var (_, output) = await RunAsync("seqkit", ["stats", "-T", "-a", "-j", Math.Min(_threads, files.Count).ToString(), .. files]);
            return ReadStats.Parse(new StringReader(output));
        }

        private static async Task RenameRecordAsync(string source, string target, string name)
        {
            using var reader = new StreamReader(source);
            await using var writer = new StreamWriter(target);
            string? line;
            bool first = true;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (line.StartsWith('>') && first)
                {
                    await writer.WriteLineAsync($">{name}");
                    first = false;
                }
                else
                {
                    await writer.WriteLineAsync(line);
                }
            }
        }

        /// <summary>Програма з аргументами без оболонки; stdout повертається (idxstats, seqkit), stderr — у помилку.</summary>
        private static async Task<(int ExitCode, string Output)> RunAsync(string executable, IEnumerable<string> args)
        {
            var startInfo = new ProcessStartInfo(executable)
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            };
            foreach (string arg in args) startInfo.ArgumentList.Add(arg);
            return await WaitAsync(startInfo, executable);
        }

        /// <summary>
        /// Конвеєр minimap2 | samtools sort потребує оболонки: bash із pipefail, щоб збій minimap2 не загубився в конвеєрі.
        /// Усі шляхи — наші (Id пакетів), і вони беруться в лапки.
        /// </summary>
        private static Task<(int ExitCode, string Output)> ShellAsync(string command)
        {
            var startInfo = new ProcessStartInfo("/bin/bash")
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            };
            startInfo.ArgumentList.Add("-c");
            startInfo.ArgumentList.Add($"set -o pipefail; {command}");
            return WaitAsync(startInfo, command.Split(' ')[0]);
        }

        private static async Task<(int ExitCode, string Output)> WaitAsync(ProcessStartInfo startInfo, string name)
        {
            using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Не вдалося запустити {name}.");
            Task<string> stdout = process.StandardOutput.ReadToEndAsync();
            Task<string> stderr = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            string output = await stdout, error = await stderr;
            if (process.ExitCode != 0)
                throw new InvalidOperationException($"{name} завершився з кодом {process.ExitCode}: {Tail(error.Length > 0 ? error : output)}");
            return (process.ExitCode, output);
        }

        public static string Q(string path) => $"'{path.Replace("'", "'\\''")}'";

        private static string Tail(string text, int max = 1500) => text.Length <= max ? text.Trim() : text[^max..].Trim();

        public static string Number(long n) => n.ToString("N0", CultureInfo.InvariantCulture).Replace(',', ' ');

        public static string Bases(long n) => n switch
        {
            >= 1_000_000 => $"{n / 1e6:0.0} Мб",
            >= 1_000 => $"{n / 1e3:0.0} кб",
            _ => $"{n} нт"
        };

        private static string Percent(long part, long total) => $"{100.0 * part / total:0.0}%";

        /// <summary>Чи файл схожий на FASTQ: перший рядок починається з «@» (з урахуванням gzip).</summary>
        public static bool LooksLikeFastq(string path)
        {
            using var file = File.OpenRead(path);
            Stream stream = path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase) ? new GZipStream(file, CompressionMode.Decompress) : file;
            using var reader = new StreamReader(stream);
            return reader.ReadLine()?.StartsWith('@') == true;
        }
    }
}
