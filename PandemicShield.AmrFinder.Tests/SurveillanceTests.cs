using PandemicShield.AmrFinder.Surveillance;

namespace PandemicShield.AmrFinder.Tests
{
    /// <summary>
    /// Дані нагляду на справжніх файлах NCBI:
    /// <list type="bullet">
    /// <item><c>klebsiella-excerpt.amr.metadata.tsv</c> — заголовок і 7 рядків з PDG000000012.2562.amr.metadata.tsv
    /// (NCBI Pathogen Detection, Klebsiella): 2 ізоляти без країни і 5 з України, з них 2 з Вінниці;</item>
    /// <item><c>AMRProt.fa</c>, <c>AMRProt-mutation.tsv</c>, <c>fam.tsv</c> — рядки бази AMRFinderPlus 2026-08-07.1
    /// для всіх символів із цих 7 ізолятів.</item>
    /// </list>
    /// </summary>
    public class SurveillanceTests
    {
        private static string DataPath(string name) => Path.Combine(AppContext.BaseDirectory, "Data", "surveillance", name);

        private static readonly AmrCatalog Catalog = AmrCatalog.Load(Path.Combine(AppContext.BaseDirectory, "Data", "surveillance"));

        private static List<IsolateRow> ReadUkraine()
        {
            using var reader = new StreamReader(DataPath("klebsiella-excerpt.amr.metadata.tsv"));
            return PathogenDetectionReader.Read(reader, ["ukraine"]).ToList();
        }

        [Fact]
        public void Keeps_only_isolates_from_selected_countries()
        {
            int rows = 0;
            using var reader = new StreamReader(DataPath("klebsiella-excerpt.amr.metadata.tsv"));
            var isolates = PathogenDetectionReader.Read(reader, ["Ukraine"], n => rows = n).ToList();

            Assert.Equal(7, rows);
            Assert.Equal(5, isolates.Count);
            Assert.All(isolates, i => Assert.Equal("Ukraine", i.Country));
            Assert.Equal([null, null, null, "Vinnitsa", "Vinnitsa"], isolates.Select(i => i.Region));
        }

        [Fact]
        public void Reads_isolate_metadata_and_genotypes()
        {
            var isolate = ReadUkraine().First(i => i.AssemblyAccession == "GCA_009887825.1");

            Assert.Equal("PDT000665245.1", isolate.TargetAccession);
            Assert.Equal(("2017-10-03", 2017), (isolate.CollectionDate, isolate.CollectionYear));
            Assert.Equal("wound", isolate.IsolationSource);
            Assert.Equal("Klebsiella pneumoniae", isolate.Organism);
            Assert.Equal(16, isolate.Genotypes.Count);
            Assert.Contains("blaOXA-48", isolate.Genotypes);
            Assert.Contains("gyrA_S83I=POINT", isolate.Genotypes);
            // Лапки навколо списку в TSV прибрано.
            Assert.False(isolate.AmrGenotypes!.StartsWith('"'));
        }

        [Theory]
        [InlineData("Ukraine: Vinnitsa", "Ukraine", "Vinnitsa")]
        [InlineData("Ukraine:Kyiv", "Ukraine", "Kyiv")]
        [InlineData("Ukraine", "Ukraine", null)]
        [InlineData("Ukraine: ", "Ukraine", null)]
        public void Splits_geo_location(string geo, string country, string? region)
        {
            Assert.Equal((country, region), PathogenDetectionReader.SplitGeo(geo));
        }

        [Theory]
        [InlineData("2017-10-03", 2017)]
        [InlineData("2015", 2015)]
        [InlineData("2022/2023", 2022)]
        [InlineData("missing", null)]
        [InlineData(null, null)]
        public void Parses_collection_year(string? date, int? year)
        {
            Assert.Equal(year, PathogenDetectionReader.Year(date));
        }

        [Fact]
        public void Classifies_carbapenemases_esbl_and_point_mutations()
        {
            var oxa48 = Catalog.Classify("blaOXA-48");
            Assert.Equal(("blaOXA-48", "BETA-LACTAM", "CARBAPENEM"), (oxa48.Family, oxa48.Class, oxa48.Subclass));
            Assert.True(oxa48.IsCarbapenemase);

            var ctxm = Catalog.Classify("blaCTX-M-15");
            Assert.Equal("blaCTX-M", ctxm.Family);
            Assert.True(ctxm.IsCephalosporinase);
            Assert.False(ctxm.IsCarbapenemase);

            // OXA-1 — теж OXA, але не карбапенемаза: класифікація за алелем, а не за префіксом назви.
            var oxa1 = Catalog.Classify("blaOXA-1");
            Assert.False(oxa1.IsCarbapenemase);
            Assert.Equal("blaOXA-1", oxa1.Family);

            // Внутрішній вузол каталогу (fos_A_A2) показуємо символом гена.
            Assert.Equal(("fosA", "FOSFOMYCIN"), (Catalog.Classify("fosA").Family, Catalog.Classify("fosA").Class));

            var gyrA = Catalog.Classify("gyrA_S83I=POINT");
            Assert.Equal(("gyrA_S83I", "gyrA", "POINT", "QUINOLONE"), (gyrA.Symbol, gyrA.Family, gyrA.Qualifier, gyrA.Class));
        }

        [Fact]
        public void Partial_genes_keep_their_class_and_qualifier()
        {
            var catB3 = Catalog.Classify("catB3=PARTIAL_END_OF_CONTIG");
            Assert.Equal(("catB3", "PARTIAL_END_OF_CONTIG", "PHENICOL"), (catB3.Symbol, catB3.Qualifier, catB3.Class));
        }

        [Fact]
        public void Unknown_point_mutation_falls_back_to_gene_without_class()
        {
            // Обрив порину OmpK35 зсувом рамки: у таблиці мутацій його немає.
            var ompK35 = Catalog.Classify("ompK35_P255QfsTer5=POINT");
            Assert.Equal(("ompK35", null), (ompK35.Family, ompK35.Class));
        }

        [Fact]
        public void Every_ukrainian_isolate_gene_is_classified_except_porin_truncations()
        {
            var unclassified = ReadUkraine()
                .SelectMany(i => i.Genotypes)
                .Select(Catalog.Classify)
                .Where(c => c.Class == null)
                .Select(c => c.Family)
                .Distinct()
                .ToList();

            Assert.Equal(["ompK35"], unclassified);
        }

        [Fact]
        public void Porin_mutation_gives_carbapenem_resistance_but_is_not_a_carbapenemase()
        {
            var ompK36 = Catalog.Classify("ompK36_D135DGD=POINT");

            Assert.Equal(("BETA-LACTAM", "CARBAPENEM"), (ompK36.Class, ompK36.Subclass));
            Assert.False(ompK36.IsCarbapenemase);
        }

        [Fact]
        public void Three_of_five_ukrainian_isolates_carry_oxa48()
        {
            var carbapenemase = ReadUkraine()
                .Where(i => i.Genotypes.Select(Catalog.Classify).Any(c => c.IsCarbapenemase))
                .Select(i => i.AssemblyAccession)
                .ToList();

            Assert.Equal(["GCA_009887825.1", "GCA_019927345.1", "GCA_051906385.1"], carbapenemase);
        }
    }
}
