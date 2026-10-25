using System.Diagnostics;

namespace PandemicShield.Mlst
{
    /// <summary>
    /// Запускає <c>mlst</c> (Torsten Seemann) з образу <c>staphb/mlst</c>: BLAST шукає сім генів «домашнього
    /// господарства» і порівнює їхні алелі зі схемами PubMLST, що лежать в образі (працює без інтернету).
    /// </summary>
    public sealed class MlstRunner
    {
        /// <summary>
        /// Вид AMRFinderPlus → схема PubMLST. Схему краще задавати явно: при автовизначенні гени Klebsiella можуть
        /// дати такий самий бал і в схемі E. coli (mlst попереджає «klebsiella(11)==ecoli_achtman_4(14464)»).
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> Schemes = new Dictionary<string, string>
        {
            ["Klebsiella_pneumoniae"] = "klebsiella",
            ["Klebsiella_oxytoca"] = "koxytoca",
            ["Escherichia"] = "ecoli_achtman_4",
            ["Acinetobacter_baumannii"] = "abaumannii_2",
            ["Pseudomonas_aeruginosa"] = "paeruginosa",
            ["Staphylococcus_aureus"] = "saureus",
            ["Staphylococcus_epidermidis"] = "sepidermidis",
            ["Enterococcus_faecium"] = "efaecium",
            ["Enterococcus_faecalis"] = "efaecalis",
            ["Enterobacter_cloacae"] = "ecloacae",
            ["Enterobacter_asburiae"] = "ecloacae",
            ["Citrobacter_freundii"] = "cfreundii",
            ["Salmonella"] = "senterica_achtman_2",
            ["Campylobacter"] = "campylobacter",
            ["Clostridioides_difficile"] = "cdifficile",
            ["Corynebacterium_diphtheriae"] = "cdiphtheriae",
            ["Haemophilus_influenzae"] = "hinfluenzae",
            ["Neisseria_gonorrhoeae"] = "neisseria",
            ["Neisseria_meningitidis"] = "neisseria",
            ["Streptococcus_pneumoniae"] = "spneumoniae",
            ["Streptococcus_pyogenes"] = "spyogenes",
            ["Streptococcus_agalactiae"] = "sagalactiae",
            ["Vibrio_cholerae"] = "vcholerae",
            ["Vibrio_parahaemolyticus"] = "vparahaemolyticus",
            ["Vibrio_vulnificus"] = "vvulnificus",
            ["Bordetella_pertussis"] = "bordetella_3",
            ["Burkholderia_pseudomallei"] = "bpseudomallei",
            ["Burkholderia_cepacia"] = "bcc"
        };

        private readonly string _executable;

        public MlstRunner(string executable) => _executable = executable;

        public static MlstRunner FromEnvironment() => new(Environment.GetEnvironmentVariable("MLST_BIN") ?? "mlst");

        public async Task<string?> VersionAsync()
        {
            var (_, output) = await RunAsync(["--version"]);
            return output.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        }

        /// <summary>Визначає тип штаму. Без відомої схеми mlst перебирає всі схеми й обирає найкращу.</summary>
        public async Task<MlstResult> RunAsync(string fastaPath, string? organism, string outputDir)
        {
            Directory.CreateDirectory(outputDir);
            string json = Path.Combine(outputDir, "mlst.json");

            var args = new List<string> { "--quiet", "--json", json, "--label", Path.GetFileName(Path.GetDirectoryName(fastaPath)) ?? "sample" };
            if (organism != null && Schemes.TryGetValue(organism, out string? scheme))
            {
                args.Add("--scheme");
                args.Add(scheme);
            }
            args.Add(fastaPath);

            var (exitCode, output) = await RunAsync(args);
            if (exitCode != 0 || !File.Exists(json))
                throw new InvalidOperationException($"mlst завершився з кодом {exitCode}: {Tail(output)}");

            return MlstReport.Read(await File.ReadAllTextAsync(json));
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
    }
}
