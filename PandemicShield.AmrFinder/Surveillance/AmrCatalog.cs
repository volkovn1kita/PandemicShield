using System.Text.RegularExpressions;

namespace PandemicShield.AmrFinder.Surveillance
{
    /// <summary>Ген або точкова мутація з таблиці NCBI, з класом антибіотиків.</summary>
    public sealed record GeneCall(string Symbol, string Family, string? Qualifier, string? Class, string? Subclass)
    {
        /// <summary>
        /// Фермент, що руйнує карбапенеми (KPC, NDM, OXA-48…). Точкові мутації поринів (ompK36_D135DGD) теж мають
        /// підклас CARBAPENEM, але це не карбапенемаза: вони лише звужують «вхід» антибіотика в клітину.
        /// </summary>
        public bool IsCarbapenemase => Qualifier != "POINT" && Subclass?.Split('/').Contains("CARBAPENEM") == true;
        public bool IsColistin => Subclass?.Split('/').Contains("COLISTIN") == true;

        /// <summary>ESBL та інші β-лактамази, що руйнують цефалоспорини (CTX-M та ін.).</summary>
        public bool IsCephalosporinase => Class == "BETA-LACTAM" && Subclass?.Split('/').Contains("CEPHALOSPORIN") == true;
    }

    /// <summary>
    /// Каталог AMRFinderPlus з бази в образі <c>ncbi/amr</c>: який клас антибіотиків стоїть за символом гена з NCBI
    /// Pathogen Detection (там у колонці AMR_genotypes є лише символи: "blaOXA-48,gyrA_S83I=POINT").
    /// <list type="bullet">
    /// <item><c>AMRProt.fa</c> — алелі: <c>&gt;WP_…|1|1|blaNDM-1|blaNDM|hydrolase|2|CARBAPENEM|BETA-LACTAM|…</c> (символ, родина, підклас, клас);</item>
    /// <item><c>AMRProt-mutation.tsv</c>, <c>AMR_DNA-*.tsv</c> — точкові мутації в білках і нуклеотидах;</item>
    /// <item><c>fam.tsv</c> — родини генів (fosA, blaCTX-M…), коли алель не вказано.</item>
    /// </list>
    /// </summary>
    public sealed partial class AmrCatalog
    {
        private readonly Dictionary<string, (string Family, string? Class, string? Subclass)> _alleles = new(StringComparer.Ordinal);
        private readonly Dictionary<string, (string? Class, string? Subclass)> _mutations = new(StringComparer.Ordinal);
        private readonly Dictionary<string, (string Family, string? Class, string? Subclass)> _families = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _nodeSymbols = new(StringComparer.Ordinal);

        public int AlleleCount => _alleles.Count;
        public int MutationCount => _mutations.Count;
        public int FamilyCount => _families.Count;

        public static AmrCatalog Load(string databaseDir)
        {
            var catalog = new AmrCatalog();
            using (var reader = new StreamReader(Path.Combine(databaseDir, "AMRProt.fa"))) catalog.ReadProteins(reader);
            using (var reader = new StreamReader(Path.Combine(databaseDir, "AMRProt-mutation.tsv"))) catalog.ReadMutations(reader, symbolColumn: 3);
            foreach (string file in Directory.GetFiles(databaseDir, "AMR_DNA-*.tsv"))
                using (var reader = new StreamReader(file)) catalog.ReadMutations(reader, symbolColumn: 2);
            using (var reader = new StreamReader(Path.Combine(databaseDir, "fam.tsv"))) catalog.ReadFamilies(reader);
            return catalog;
        }

        public void ReadProteins(TextReader reader)
        {
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.Length == 0 || line[0] != '>') continue;
                string[] f = line[1..].Split('|');
                if (f.Length < 9 || f[3].Length == 0) continue;
                _alleles.TryAdd(f[3], (f[4].Length > 0 ? f[4] : f[3], Empty(f[8]), Empty(f[7])));
            }
        }

        // AMRProt-mutation.tsv: #taxgroup accession position standard_mutation_symbol reported class subclass name
        // AMR_DNA-*.tsv:       #accession@gene position standard_mutation_symbol reported class subclass name
        public void ReadMutations(TextReader reader, int symbolColumn)
        {
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.Length == 0 || line[0] == '#') continue;
                string[] f = line.Split('\t');
                if (f.Length < symbolColumn + 4) continue;
                _mutations.TryAdd(f[symbolColumn], (Empty(f[symbolColumn + 2]), Empty(f[symbolColumn + 3])));
            }
        }

        // fam.tsv: #node_id parent gene_symbol hmm_id … reportable type subtype class subclass family_name
        public void ReadFamilies(TextReader reader)
        {
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.Length == 0 || line[0] == '#') continue;
                string[] f = line.Split('\t');
                if (f.Length >= 3 && f[2].Length > 1) _nodeSymbols.TryAdd(f[0], f[2]);
                if (f.Length < 17 || f[15].Length == 0) continue;
                var value = (FamilyName(f[2] is { Length: > 1 } symbol ? symbol : f[0]), Empty(f[15]), Empty(f[16]));
                if (f[2].Length > 1) _families.TryAdd(f[2], value);
                _families.TryAdd(f[0], value);
            }
        }

        /// <summary>Символ з AMR_genotypes: "blaOXA-48", "gyrA_S83I=POINT", "catB3=PARTIAL_END_OF_CONTIG".</summary>
        public GeneCall Classify(string token)
        {
            string text = token.Trim();
            int eq = text.IndexOf('=');
            string symbol = eq < 0 ? text : text[..eq];
            string? qualifier = eq < 0 ? null : text[(eq + 1)..];

            if (_alleles.TryGetValue(symbol, out var allele))
                return new GeneCall(symbol, ResolveFamily(allele.Family), qualifier, allele.Class, allele.Subclass);

            if (_mutations.TryGetValue(symbol, out var mutation))
                return new GeneCall(symbol, MutatedGene(symbol), qualifier, mutation.Class, mutation.Subclass);

            if (_families.TryGetValue(symbol, out var family))
                return new GeneCall(symbol, family.Family, qualifier, family.Class, family.Subclass);

            // Невідомий алель відомої родини: oqxB32 → oqxB, blaTEM-238 → blaTEM.
            string trimmed = TrailingAlleleRegex().Replace(symbol, "");
            if (trimmed != symbol && _families.TryGetValue(trimmed, out family))
                return new GeneCall(symbol, family.Family, qualifier, family.Class, family.Subclass);

            // Точкова мутація, якої немає в таблиці (наприклад, обрив ompK35_P255QfsTer5): родина — сам ген.
            return new GeneCall(symbol, qualifier == "POINT" ? MutatedGene(symbol) : symbol, qualifier, null, null);
        }

        // Родина алеля в AMRProt.fa — це вузол ієрархії fam.tsv. Вузли «…_fam» — справжні підродини (blaOXA-48_fam
        // і blaOXA-1_fam різні), а інші вузли з підкресленням — внутрішні назви (fos_A_A2), тож для них беремо символ гена (fosA).
        private string ResolveFamily(string node)
        {
            if (node.EndsWith("_fam", StringComparison.Ordinal)) return FamilyName(node);
            return node.Contains('_') && _nodeSymbols.TryGetValue(node, out string? symbol) ? symbol : node;
        }

        private static string MutatedGene(string symbol)
        {
            int underscore = symbol.IndexOf('_');
            return underscore > 0 ? symbol[..underscore] : symbol;
        }

        // blaOXA-48_fam → blaOXA-48: так родину пишуть у статтях і так її шукають.
        private static string FamilyName(string family) => family.EndsWith("_fam", StringComparison.Ordinal) ? family[..^4] : family;

        private static string? Empty(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        [GeneratedRegex(@"-?\d+$")]
        private static partial Regex TrailingAlleleRegex();
    }
}
