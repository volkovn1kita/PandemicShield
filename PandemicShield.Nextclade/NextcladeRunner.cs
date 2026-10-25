using System.Diagnostics;
using System.Text.Json;

namespace PandemicShield.Nextclade
{
    /// <summary>
    /// Запускає Nextclade CLI. Набори даних (datasets) завантажуються під час збирання Docker-образу
    /// в каталог <c>{datasetsDir}/{назва набору}</c>, тож аналіз працює без інтернету.
    /// </summary>
    public sealed class NextcladeRunner
    {
        private readonly string _executable;
        private readonly string _datasetsDir;

        public NextcladeRunner(string executable, string datasetsDir)
        {
            _executable = executable;
            _datasetsDir = datasetsDir;
        }

        public static NextcladeRunner FromEnvironment() => new(
            Environment.GetEnvironmentVariable("NEXTCLADE_BIN") ?? "nextclade",
            Environment.GetEnvironmentVariable("NEXTCLADE_DATASETS_DIR") ?? "/datasets");

        public string DatasetDirectory(string dataset)
        {
            // Назва набору — шлях на кшталт nextstrain/sars-cov-2/wuhan-hu-1/orfs; не дозволяємо виходити за межі каталогу.
            string full = Path.GetFullPath(Path.Combine(_datasetsDir, dataset));
            string root = Path.GetFullPath(_datasetsDir) + Path.DirectorySeparatorChar;
            if (!full.StartsWith(root, StringComparison.Ordinal))
                throw new ArgumentException($"Некоректна назва набору даних: {dataset}", nameof(dataset));
            return full;
        }

        /// <summary>Версія (тег) набору даних з pathogen.json, щоб результат можна було відтворити.</summary>
        public string? DatasetTag(string dataset)
        {
            string path = Path.Combine(DatasetDirectory(dataset), "pathogen.json");
            if (!File.Exists(path)) return null;

            using var document = JsonDocument.Parse(File.ReadAllText(path));
            return document.RootElement.TryGetProperty("version", out var version)
                   && version.TryGetProperty("tag", out var tag)
                ? tag.GetString()
                : null;
        }

        /// <summary>Аналізує FASTA-файл і повертає шлях до результатів у форматі NDJSON.</summary>
        public async Task<string> RunAsync(string fastaPath, string dataset, string outputDir, CancellationToken cancellationToken = default)
        {
            string datasetDir = DatasetDirectory(dataset);
            if (!File.Exists(Path.Combine(datasetDir, "pathogen.json")))
                throw new InvalidOperationException($"Набір даних «{dataset}» не встановлено в {datasetDir}.");

            Directory.CreateDirectory(outputDir);
            string ndjson = Path.Combine(outputDir, "nextclade.ndjson");

            var startInfo = new ProcessStartInfo(_executable)
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            };
            foreach (string arg in new[]
            {
                "run",
                "--input-dataset", datasetDir,
                "--output-ndjson", ndjson,
                "--output-tsv", Path.Combine(outputDir, "nextclade.tsv"),
                "--verbosity", "warn",
                fastaPath
            })
            {
                startInfo.ArgumentList.Add(arg);
            }

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException($"Не вдалося запустити {_executable}.");

            Task<string> stderr = process.StandardError.ReadToEndAsync(cancellationToken);
            Task<string> stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
            {
                string error = (await stderr).Trim();
                throw new InvalidOperationException(
                    $"Nextclade завершився з кодом {process.ExitCode}: {Tail(error.Length > 0 ? error : await stdout)}");
            }

            return ndjson;
        }

        private static string Tail(string text, int max = 2000) => text.Length <= max ? text : text[^max..];
    }
}
