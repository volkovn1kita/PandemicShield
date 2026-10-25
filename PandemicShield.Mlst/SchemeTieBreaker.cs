using System.Text.RegularExpressions;

namespace PandemicShield.Mlst
{
    /// <summary>
    /// Розв'язує «нічию» схем при автовизначенні: гени Klebsiella дають 100% і в схемі E. coli
    /// (mlst попереджає <c>klebsiella(11)==ecoli_achtman_4(14464)</c>) і обирає між ними навмання.
    /// Види сильно різняться за вмістом GC (K. pneumoniae ~57%, E. coli ~51%), тож обирається схема,
    /// чий типовий GC найближчий до GC збірки. Це запасний шлях: коли вид відомий, схема задається явно.
    /// </summary>
    public static partial class SchemeTieBreaker
    {
        /// <summary>Типовий вміст GC геномів (%, за референсними збірками NCBI; наближено).</summary>
        public static readonly IReadOnlyDictionary<string, double> TypicalGc = new Dictionary<string, double>
        {
            ["klebsiella"] = 57.2, ["koxytoca"] = 55.6, ["ecoli_achtman_4"] = 50.7, ["ecoli"] = 50.7,
            ["senterica_achtman_2"] = 52.2, ["ecloacae"] = 55.0, ["cfreundii"] = 51.6, ["abaumannii_2"] = 39.0,
            ["abaumannii"] = 39.0, ["paeruginosa"] = 66.3, ["saureus"] = 32.8, ["sepidermidis"] = 32.1,
            ["efaecium"] = 38.0, ["efaecalis"] = 37.4, ["spneumoniae"] = 39.7, ["spyogenes"] = 38.5,
            ["sagalactiae"] = 35.6, ["campylobacter"] = 30.5, ["cdifficile"] = 29.1, ["neisseria"] = 51.8,
            ["hinfluenzae"] = 38.1, ["vcholerae"] = 47.5, ["vparahaemolyticus"] = 45.4, ["vvulnificus"] = 46.7,
            ["bpseudomallei"] = 68.1, ["bcc"] = 66.7, ["cdiphtheriae"] = 53.5, ["bordetella_3"] = 67.7
        };

        /// <summary>Схеми з однаковим балом зі stderr mlst: "WARNING: klebsiella(11)==ecoli_achtman_4(14464) score=100".</summary>
        public static List<string> TiedSchemes(string mlstOutput)
        {
            var schemes = new List<string>();
            foreach (Match match in TieRegex().Matches(mlstOutput))
            {
                foreach (string scheme in new[] { match.Groups["a"].Value, match.Groups["b"].Value })
                    if (!schemes.Contains(scheme)) schemes.Add(scheme);
            }
            return schemes;
        }

        /// <summary>Схема з типовим GC, найближчим до GC збірки; null — якщо GC жодної з них невідомий.</summary>
        public static string? Choose(IEnumerable<string> schemes, double assemblyGc) =>
            schemes
                .Where(TypicalGc.ContainsKey)
                .OrderBy(s => Math.Abs(TypicalGc[s] - assemblyGc))
                .FirstOrDefault();

        /// <summary>Вміст GC у відсотках (лише A, C, G, T; N та інші коди не враховуються).</summary>
        public static double GcPercent(TextReader fasta)
        {
            long gc = 0, acgt = 0;
            string? line;
            while ((line = fasta.ReadLine()) != null)
            {
                if (line.StartsWith('>')) continue;
                foreach (char c in line)
                {
                    switch (char.ToUpperInvariant(c))
                    {
                        case 'G' or 'C':
                            gc++;
                            acgt++;
                            break;
                        case 'A' or 'T':
                            acgt++;
                            break;
                    }
                }
            }
            return acgt == 0 ? 0 : 100.0 * gc / acgt;
        }

        [GeneratedRegex(@"(?<a>[A-Za-z0-9_]+)\([^)]*\)==(?<b>[A-Za-z0-9_]+)\([^)]*\)")]
        private static partial Regex TieRegex();
    }
}
