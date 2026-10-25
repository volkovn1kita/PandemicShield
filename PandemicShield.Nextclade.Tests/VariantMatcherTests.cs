using PandemicShield.Bio.Variants;
using PandemicShield.Nextclade.Rules;
using static PandemicShield.Nextclade.Tests.NextcladeFixture;

namespace PandemicShield.Nextclade.Tests
{
    public class VariantMatcherTests
    {
        // Ті самі маркери, що в DbSeeder.
        private static readonly string[] SeedNotations =
        [
            "S:N501Y", "S:E484K", "S:L452R", "S:P681R", "ORF1a:E3429V", "clade:Delta", "lineage:BA.2.86*"
        ];

        private static List<VariantRule> Rules(params string[] notations) =>
            notations.Select(n => new VariantRule(Guid.NewGuid(), n, VariantPattern.Parse(n), Dataset)).ToList();

        private static string[] Matched(string seqName, params string[] notations) =>
            VariantMatcher.Match(Get(seqName), Rules(notations), Dataset)
                .Select(m => m.Rule.Name)
                .ToArray();

        [Theory]
        [InlineData(Alpha, new[] { "S:N501Y" })]
        [InlineData(Beta, new[] { "S:N501Y", "S:E484K" })]
        [InlineData(Delta, new[] { "S:L452R", "S:P681R", "clade:Delta" })]
        [InlineData(Ba2, new[] { "S:N501Y" })]                                         // у цьому BA.2 на місці 484 — R
        [InlineData(Jn, new[] { "S:N501Y", "S:E484K", "S:P681R", "lineage:BA.2.86*" })]
        [InlineData(Eg51, new[] { "S:N501Y" })]
        [InlineData(TooShort, new string[0])]
        public void Seed_markers_match_expected_variants(string seqName, string[] expected)
        {
            Assert.Equal(expected, Matched(seqName, SeedNotations));
        }

        [Fact]
        public void Any_alt_pattern_matches_any_substitution_at_position()
        {
            // У BA.2 (OP523232) в позиції 484 — E484R, у Beta — E484K.
            Assert.Equal(["S:E484"], Matched(Ba2, "S:E484"));
            Assert.Equal(["S:E484"], Matched(Beta, "S:E484"));
            Assert.Empty(Matched(Alpha, "S:E484"));
        }

        [Fact]
        public void Deletion_pattern_matches_inside_deleted_range()
        {
            Assert.Equal(["22031-", "S:F157-"], Matched(Delta, "22031-", "S:F157-", "22040-"));
        }

        [Fact]
        public void Delta_described_as_E156G_does_not_match_nextclade_alignment()
        {
            // Важливий урок: правило треба писати так, як мутацію записує інструмент (S:F157- або S:R158G).
            Assert.Empty(Matched(Delta, "S:E156G"));
            Assert.Equal(["S:R158G"], Matched(Delta, "S:R158G"));
        }

        [Fact]
        public void Clade_matches_by_code_display_name_or_who_label()
        {
            Assert.Equal(["clade:21J", "clade:21J (Delta)", "clade:delta"], Matched(Delta, "clade:21J", "clade:21J (Delta)", "clade:delta", "clade:Alpha"));
        }

        [Fact]
        public void Lineage_with_star_matches_descendants_through_partial_alias()
        {
            Assert.Equal(["lineage:XBB*"], Matched(Eg51, "lineage:XBB*", "lineage:XBB"));
            Assert.Equal(["lineage:EG.5.1.16"], Matched(Eg51, "lineage:EG.5.1.16", "lineage:EG.5.1"));
            Assert.Equal(["lineage:B.1.617.2*"], Matched(Delta, "lineage:B.1.617.2*"));
        }

        [Fact]
        public void Rules_for_another_dataset_are_ignored()
        {
            var rule = new VariantRule(Guid.NewGuid(), "flu", VariantPattern.Parse("S:N501Y"), "nextstrain/flu/h3n2/ha/EPI1857216");

            Assert.Empty(VariantMatcher.Match(Get(Alpha), [rule], Dataset));
        }
    }
}
