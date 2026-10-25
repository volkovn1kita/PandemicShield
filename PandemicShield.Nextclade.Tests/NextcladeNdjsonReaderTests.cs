using PandemicShield.Nextclade.Results;
using static PandemicShield.Nextclade.Tests.NextcladeFixture;

namespace PandemicShield.Nextclade.Tests
{
    public class NextcladeNdjsonReaderTests
    {
        [Fact]
        public void Reads_every_sequence_including_failed_ones()
        {
            Assert.Equal(8, Sequences.Count);

            var failed = Get(TooShort);
            Assert.True(failed.HasErrors);
            Assert.Contains("sequence is too short", failed.Errors.Single());
            Assert.Empty(failed.Mutations);
        }

        [Theory]
        [InlineData(Alpha, "20I", "20I (Alpha)", "B.1.1.7")]
        [InlineData(Beta, "20H", "20H (Beta)", "B.1.351")]
        [InlineData(Delta, "21J", "21J (Delta)", "AY.3")]
        [InlineData(Ba2, "21L", "21L (BA.2)", "BA.2.3.20")]
        [InlineData(Jn, "23I", "23I (BA.2.86)", "JN.3.3")]
        [InlineData(Eg51, "23F", "23F (EG.5.1)", "EG.5.1.16")]
        public void Reads_clade_and_lineage(string seqName, string clade, string display, string lineage)
        {
            var sequence = Get(seqName);

            Assert.Equal((clade, display, lineage), (sequence.Clade, sequence.CladeDisplay, sequence.Lineage));
            Assert.Equal("good", sequence.QcStatus);
            Assert.False(sequence.HasErrors);
        }

        [Fact]
        public void Converts_positions_to_one_based_reference_coordinates()
        {
            var delta = Get(Delta);
            string[] labels = delta.Mutations.Select(m => m.Label).ToArray();

            // Nextclade віддає pos 23402 (0-based) → A23403G, тобто S:D614G.
            Assert.Contains("A23403G", labels);
            Assert.Contains("S:D614G", labels);
            Assert.Contains("S:L452R", labels);
            Assert.Contains("del:22029-22034", labels);

            var l452r = delta.Mutations.Single(m => m.Label == "S:L452R");
            Assert.Equal(21563 + 451 * 3, l452r.NucleotidePosition);   // перша основа кодона 452 гена S
        }

        /// <summary>
        /// Делецію Delta в Spike зазвичай описують як «E156G + del157-158». Nextclade вирівнює ті самі 6 нуклеотидів
        /// інакше: «E156-, F157-, R158G». Обидва записи дають той самий білок; правила мають це враховувати.
        /// </summary>
        [Fact]
        public void Delta_spike_deletion_is_reported_with_right_aligned_gap()
        {
            string[] labels = Get(Delta).Mutations.Select(m => m.Label).ToArray();

            Assert.Contains("S:E156-", labels);
            Assert.Contains("S:F157-", labels);
            Assert.Contains("S:R158G", labels);
            Assert.DoesNotContain("S:E156G", labels);
        }

        [Fact]
        public void Reads_frameshifts()
        {
            var frameShift = Get(Jf4).Mutations.Single(m => m.Kind == MutationKind.FrameShift);

            Assert.Equal("ORF7b:fs14-44", frameShift.Label);
        }

        [Fact]
        public void Partially_aliased_lineage_reveals_ancestry()
        {
            // EG.5.1 — це скорочення для XBB.1.9.2.5.1, JN — для BA.2.86.1.
            Assert.Equal("XBB.1.9.2.5.1.16", Get(Eg51).PartiallyAliasedLineage);
            Assert.Equal("BA.2.86.1.3.3", Get(Jn).PartiallyAliasedLineage);
        }
    }
}
