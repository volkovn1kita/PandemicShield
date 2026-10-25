namespace PandemicShield.Bio.Tests
{
    public class GeneticCodeTests
    {
        [Theory]
        [InlineData("ATG", 'M')]
        [InlineData("TGG", 'W')]
        [InlineData("GAG", 'E')]
        [InlineData("GTG", 'V')]
        [InlineData("TAA", '*')]
        [InlineData("GCN", 'A')]   // усі чотири варіанти — аланін
        [InlineData("TAR", '*')]   // TAA і TAG — обидва стоп
        [InlineData("TAN", 'X')]   // тирозин або стоп — невідомо
        [InlineData("NNN", 'X')]
        public void Standard_code_translates_codons(string codon, char expected)
        {
            Assert.Equal(expected, GeneticCode.Standard.Translate(codon[0], codon[1], codon[2]));
        }

        [Fact]
        public void Ambiguous_codon_is_stop_only_if_every_variant_is_stop()
        {
            Assert.True(GeneticCode.Standard.IsStop('T', 'R', 'A'));   // TAA, TGA
            Assert.False(GeneticCode.Standard.IsStop('T', 'A', 'N'));
        }

        [Fact]
        public void Vertebrate_mitochondrial_code_differs_from_standard()
        {
            var mito = GeneticCode.VertebrateMitochondrial;
            Assert.Equal('W', mito.Translate('T', 'G', 'A'));
            Assert.Equal('M', mito.Translate('A', 'T', 'A'));
            Assert.True(mito.IsStop('A', 'G', 'A'));
        }

        [Fact]
        public void Alternative_starts_are_used_only_when_enabled()
        {
            var bacterial = GeneticCode.BacterialArchaealPlastid;
            Assert.True(bacterial.IsStart('G', 'T', 'G', allowAlternative: true));
            Assert.False(bacterial.IsStart('G', 'T', 'G', allowAlternative: false));
            Assert.True(bacterial.IsStart('A', 'T', 'G', allowAlternative: false));
        }

        [Fact]
        public void Reverse_complement_handles_iupac_codes()
        {
            Assert.Equal("NCRYAT", Nucleotides.ReverseComplement("ATRYGN"));
        }
    }
}
