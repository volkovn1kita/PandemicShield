using PandemicShield.Bio.Variants;

namespace PandemicShield.Bio.Tests
{
    public class VariantPatternTests
    {
        [Theory]
        [InlineData("A23403G", VariantKind.NucleotideSubstitution, null, 23403, 'A', 'G', "A23403G")]
        [InlineData("a23403g", VariantKind.NucleotideSubstitution, null, 23403, 'A', 'G', "A23403G")]
        [InlineData("23403", VariantKind.NucleotideSubstitution, null, 23403, null, null, "23403*")]
        [InlineData("22029-", VariantKind.NucleotideDeletion, null, 22029, null, '-', "22029-")]
        [InlineData("S:D614G", VariantKind.AminoAcidSubstitution, "S", 614, 'D', 'G', "S:D614G")]
        [InlineData(" S : E484 ", VariantKind.AminoAcidSubstitution, "S", 484, 'E', null, "S:E484")]
        [InlineData("S:p681h", VariantKind.AminoAcidSubstitution, "S", 681, 'P', 'H', "S:P681H")]
        [InlineData("ORF8:Q27*", VariantKind.AminoAcidSubstitution, "ORF8", 27, 'Q', '*', "ORF8:Q27*")]
        [InlineData("S:F157-", VariantKind.AminoAcidDeletion, "S", 157, 'F', '-', "S:F157-")]
        [InlineData("ORF1a:E3429V", VariantKind.AminoAcidSubstitution, "ORF1a", 3429, 'E', 'V', "ORF1a:E3429V")]
        public void Parses_mutation_notation(string text, VariantKind kind, string? gene, int position, char? @ref, char? alt, string canonical)
        {
            var pattern = VariantPattern.Parse(text);

            Assert.Equal((kind, gene, position, @ref, alt), (pattern.Kind, pattern.Gene, pattern.Position, pattern.Ref, pattern.Alt));
            Assert.Equal(canonical, pattern.ToString());
            Assert.Equal(pattern, VariantPattern.Parse(pattern.ToString()));
        }

        [Fact]
        public void Parses_clade_and_lineage()
        {
            Assert.Equal(new VariantPattern(VariantKind.Clade, null, 0, null, null, "Delta"), VariantPattern.Parse("clade:Delta"));

            var lineage = VariantPattern.Parse("lineage:ba.2.86*");
            Assert.Equal((VariantKind.Lineage, "BA.2.86*", true), (lineage.Kind, lineage.Name, lineage.IncludesDescendants));
        }

        [Theory]
        [InlineData("")]
        [InlineData("S:D0G")]
        [InlineData("S:D614D")]
        [InlineData("A23403A")]
        [InlineData("hello")]
        [InlineData("lineage:not a lineage")]
        public void Rejects_invalid_notation(string text)
        {
            Assert.False(VariantPattern.TryParse(text, out _, out string error));
            Assert.NotEmpty(error);
        }
    }
}
