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
        public void Scheme_tie_is_broken_by_gc_content()
        {
            // Справжнє попередження mlst 2.35.0 на збірці K. pneumoniae HS11286 без вказаного виду.
            const string warning = "WARNING: klebsiella(11)==ecoli_achtman_4(14464) score=100 /data/hs11286.fna\n";
            var tied = SchemeTieBreaker.TiedSchemes(warning);

            Assert.Equal(["klebsiella", "ecoli_achtman_4"], tied);
            Assert.Equal("klebsiella", SchemeTieBreaker.Choose(tied, assemblyGc: 57.1));
            Assert.Equal("ecoli_achtman_4", SchemeTieBreaker.Choose(tied, assemblyGc: 50.8));
            Assert.Null(SchemeTieBreaker.Choose(["unknown_scheme"], 50));
            Assert.Empty(SchemeTieBreaker.TiedSchemes("HS11286\tklebsiella\t11\tgapA(3)"));
        }

        [Fact]
        public void Gc_percent_ignores_headers_and_ambiguous_bases()
        {
            Assert.Equal(50.0, SchemeTieBreaker.GcPercent(new StringReader(">contig_1 GC\nGGCCAATT\nnnNN\n>contig_2\nacgt\n")));
        }

        [Fact]
        public void Every_scheme_with_typical_gc_is_a_real_scheme()
        {
            Assert.All(MlstRunner.Schemes.Values, scheme => Assert.True(SchemeTieBreaker.TypicalGc.ContainsKey(scheme), scheme));
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
