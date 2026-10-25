using System.Diagnostics;
using System.Text.RegularExpressions;

namespace PandemicShield.AmrFinder
{
    /// <summary>
    /// Запускає NCBI AMRFinderPlus. У Docker-образі <c>ncbi/amr</c> програма й база даних уже встановлені,
    /// тож аналіз працює без інтернету.
    /// </summary>
    public sealed partial class AmrFinderRunner
    {
        private readonly string _executable;
        private readonly int _threads;

        public AmrFinderRunner(string executable, int threads)
        {
            _executable = executable;
            _threads = threads;
        }

        public static AmrFinderRunner FromEnvironment() => new(
            Environment.GetEnvironmentVariable("AMRFINDER_BIN") ?? "amrfinder",
            int.TryParse(Environment.GetEnvironmentVariable("AMRFINDER_THREADS"), out int threads) && threads > 0
                ? threads
                : Math.Max(1, Environment.ProcessorCount - 1));

        /// <summary>Версія бази даних (наприклад, 2026-08-07.1): її зберігаємо з результатом для відтворюваності.</summary>
        public async Task<string?> DatabaseVersionAsync()
        {
            var (_, output) = await RunAsync(["-V"]);
            var match = DatabaseVersionRegex().Match(output);
            return match.Success ? match.Groups[1].Value.Trim() : null;
        }

        /// <summary>Аналізує нуклеотидну збірку (контиги FASTA) і повертає шлях до звіту TSV.</summary>
        public async Task<string> RunAsync(string fastaPath, string? organism, string outputDir)
        {
            Directory.CreateDirectory(outputDir);
            string report = Path.Combine(outputDir, "amrfinder.tsv");

            // --plus додає гени стресу (біоциди, метали) і вірулентності; --organism — точкові мутації цього виду.
            var args = new List<string> { "--nucleotide", fastaPath, "--plus", "--threads", _threads.ToString(), "--output", report };
            if (!string.IsNullOrWhiteSpace(organism))
            {
                args.Add("--organism");
                args.Add(organism);
            }

            var (exitCode, output) = await RunAsync(args);
            if (exitCode != 0)
                throw new InvalidOperationException($"AMRFinderPlus завершився з кодом {exitCode}: {Tail(output)}");

            return report;
        }

        private async Task<(int ExitCode, string Output)> RunAsync(IEnumerable<string> args)
        {
            var startInfo = new ProcessStartInfo(_executable)
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            };
            foreach (string arg in args) startInfo.ArgumentList.Add(arg);

            using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Не вдалося запустити {_executable}.");
            Task<string> stderr = process.StandardError.ReadToEndAsync();
            Task<string> stdout = process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            return (process.ExitCode, (await stdout) + (await stderr));
        }

        private static string Tail(string text, int max = 2000) => text.Length <= max ? text.Trim() : text[^max..].Trim();

        [GeneratedRegex(@"Database version:\s*(\S+)")]
        private static partial Regex DatabaseVersionRegex();
    }
}
