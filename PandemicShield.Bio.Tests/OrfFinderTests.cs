namespace PandemicShield.Bio.Tests
{
    public class OrfFinderTests
    {
        private static readonly OrfFinderOptions NoMinimum = new() { MinNucleotideLength = 0 };

        [Fact]
        public void Finds_orf_from_atg_to_stop_without_stop_in_protein()
        {
            var orf = OrfFinder.Find("s", "CCATGGCCTGGTAACC", NoMinimum).Single();

            Assert.Equal(("MAW", 2L, 14L, Strand.Forward, true), (orf.Protein, orf.Start, orf.End, orf.Strand, orf.IsComplete));
        }

        [Fact]
        public void Finds_orf_on_reverse_strand_with_forward_coordinates()
        {
            string forward = "CCATGGCCTGGTAACC";
            var orf = OrfFinder.Find("s", Nucleotides.ReverseComplement(forward), NoMinimum).Single();

            Assert.Equal(("MAW", 2L, 14L, Strand.Reverse), (orf.Protein, orf.Start, orf.End, orf.Strand));
        }

        [Fact]
        public void Longest_orf_per_stop_starts_at_first_atg()
        {
            // Внутрішній ATG у тій самій рамці не породжує окремого ORF.
            var orfs = OrfFinder.Find("s", "ATGAAAATGCCCTAA", NoMinimum with { BothStrands = false });

            Assert.Equal("MKMP", orfs.Single().Protein);
        }

        [Fact]
        public void Finds_overlapping_orf_in_another_frame()
        {
            // Рамка 1: ATG GAT GTA AAT GAG TAA → MDVNE.
            // Рамка 2: ATG у позиції 4, одразу за ним TAA → M.
            string sequence = "ATGGATGTAAATGAGTAACC";
            var proteins = OrfFinder.Find("s", sequence, NoMinimum with { BothStrands = false })
                .Select(o => o.Protein).ToList();

            Assert.Contains("MDVNE", proteins);
            Assert.Contains("M", proteins);
        }

        [Fact]
        public void Applies_minimum_length_including_stop_codon()
        {
            string sequence = "ATGAAATAA";
            Assert.Single(OrfFinder.Find("s", sequence, new OrfFinderOptions { MinNucleotideLength = 9, BothStrands = false }));
            Assert.Empty(OrfFinder.Find("s", sequence, new OrfFinderOptions { MinNucleotideLength = 10, BothStrands = false }));
        }

        [Fact]
        public void Partial_orfs_are_returned_only_on_request()
        {
            string sequence = "CCATGAAACCC";
            Assert.Empty(OrfFinder.Find("s", sequence, NoMinimum with { BothStrands = false }));

            var partial = OrfFinder.Find("s", sequence, NoMinimum with { BothStrands = false, IncludePartial = true }).Single();
            Assert.Equal(("MKP", false), (partial.Protein, partial.IsComplete));
        }

        [Fact]
        public void Alternative_start_is_translated_as_methionine()
        {
            var options = new OrfFinderOptions
            {
                MinNucleotideLength = 0,
                BothStrands = false,
                GeneticCode = GeneticCode.BacterialArchaealPlastid,
                AllowAlternativeStarts = true
            };

            Assert.Equal("MK", OrfFinder.Find("s", "GTGAAATAA", options).Single().Protein);
        }

        [Fact]
        public void Windowed_search_equals_whole_record_search_on_random_dna()
        {
            string sequence = TestSequences.RandomDna(200_000, seed: 42);
            var options = new OrfFinderOptions { MinNucleotideLength = 30 };

            var whole = OrfFinder.Find("r", sequence, options);
            var windowed = TestSequences.FindOrfsWindowed("r", sequence, coreSize: 5_000, flankSize: 2_000, options);

            Assert.True(whole.Count > 1_000);
            Assert.Equal(whole, windowed);
        }

        [Fact]
        public void Motif_coordinates_map_back_to_reverse_strand()
        {
            string forward = "CCATGGCCTGGTAACC";
            var orf = OrfFinder.Find("s", Nucleotides.ReverseComplement(forward), NoMinimum).Single();
            var match = ProteinMotifScanner.Scan(orf, "AW").Single();

            // На прямому ланцюзі "AW" (GCC TGG) — позиції 5..11 вихідної послідовності;
            // у зворотно-комплементарній — 16 - 11 = 5 .. 16 - 5 = 11.
            Assert.Equal((1, 5L, 11L), (match.AminoAcidIndex, match.NucleotideStart, match.NucleotideEnd));
        }
    }
}
