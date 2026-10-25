namespace PandemicShield.Mlst.Tests
{
    /// <summary>
    /// Справжній вивід <c>mlst 2.35.0 --json</c> (образ staphb/mlst:2.35.0):
    /// <list type="bullet">
    /// <item><c>hs11286.klebsiella.json</c> — K. pneumoniae HS11286 (GCF_000240185.1) зі схемою klebsiella: відомий штам ST11;</item>
    /// <item><c>hs11286.auto.json</c> — та сама збірка з автовизначенням схеми (mlst попереджає про нічию з ecoli_achtman_4);</item>
    /// <item><c>demo.klebsiella.json</c> — синтетична демо-збірка (випадкові контиги + гени резистентності): генів схеми немає.</item>
    /// </list>
    /// </summary>
    public class MlstTests
    {
        private static MlstResult Load(string name) =>
            MlstReport.Read(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", name)));

        [Theory]
        [InlineData("hs11286.klebsiella.json")]
        [InlineData("hs11286.auto.json")]
        public void Reads_known_sequence_type(string file)
        {
            var result = Load(file);

            Assert.Equal(("klebsiella", "11", true), (result.Scheme, result.SequenceType, result.HasSequenceType));
            // Гени впорядковано за абеткою, як у таблицях PubMLST, хоч mlst пише JSON у довільному порядку.
            Assert.Equal("gapA(3) infB(3) mdh(1) pgi(1) phoE(1) rpoB(1) tonB(4)", result.AlleleProfile);
            Assert.Equal((7, 0, 0), (result.ExactAlleles, result.MissingAlleles, result.UncertainAlleles));
            Assert.Equal("ST11 (klebsiella)", MlstReport.Describe(result));
        }

        [Fact]
        public void Synthetic_assembly_without_housekeeping_genes_has_no_sequence_type()
        {
            var result = Load("demo.klebsiella.json");

            Assert.Equal(("-", false, 7), (result.SequenceType, result.HasSequenceType, result.MissingAlleles));
            Assert.StartsWith("Тип штаму не визначено", MlstReport.Describe(result));
        }

        [Fact]
        public void Novel_and_partial_alleles_are_uncertain()
        {
            // Позначення з README mlst: ~ — новий алель, ? — частковий збіг, кома — кілька копій гена.
            var result = new MlstResult("klebsiella", "-",
                [("gapA", "3"), ("infB", "~3"), ("mdh", "1?"), ("pgi", "1,2"), ("phoE", "-"), ("rpoB", "1"), ("tonB", "4")]);

            Assert.Equal((3, 1, 3), (result.ExactAlleles, result.MissingAlleles, result.UncertainAlleles));
            Assert.Equal("Новий або неповний профіль (klebsiella): відомих алелів 3 з 7.", MlstReport.Describe(result));
        }

        [Fact]
        public void No_scheme_when_nothing_matched()
        {
            var result = MlstReport.Read("""[{"id":"x","filename":"x.fna","scheme":"-","sequence_type":"-","alleles":null}]""");
            Assert.True(result.NoScheme);
            Assert.Empty(result.Alleles);
        }

        [Fact]
        public void Every_mapped_scheme_exists_in_the_mlst_image()
        {
            // Вивід `mlst --list` в образі staphb/mlst:2.35.0 (лише схеми, на які посилається мапа).
            string[] available =
            [
                "abaumannii", "abaumannii_2", "bcc", "bordetella_3", "bpseudomallei", "campylobacter", "cdifficile", "cdiphtheriae",
                "cfreundii", "ecloacae", "ecoli", "ecoli_achtman_4", "efaecalis", "efaecium", "hinfluenzae", "klebsiella", "koxytoca",
                "neisseria", "paeruginosa", "sagalactiae", "saureus", "senterica_achtman_2", "sepidermidis", "spneumoniae", "spyogenes",
                "vcholerae", "vcholerae_2", "vparahaemolyticus", "vvulnificus"
            ];
            Assert.All(MlstRunner.Schemes.Values, scheme => Assert.Contains(scheme, available));
        }
    }
}
