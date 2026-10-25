using Microsoft.Extensions.Caching.Memory;
using PandemicShield.Api.Explorer;
using PandemicShield.Bio;
using PandemicShield.Bio.Formats;

namespace PandemicShield.Api.Tests
{
    /// <summary>Explorer на справжньому GenBank SARS-CoV-2 (MN908947.3) і кільцевому pBR322.</summary>
    public class ExplorerTests
    {
        private static SequenceRecord Load(string name)
        {
            using var reader = new StreamReader(Path.Combine(AppContext.BaseDirectory, "Data", name));
            return SequenceFile.Read(reader).Single();
        }

        private static readonly SequenceRecord Wuhan = Load("MN908947.gb");

        [Fact]
        public void Spike_orf_ends_with_the_spike_stop_but_starts_nine_codons_earlier()
        {
            var (orfs, truncated) = ExplorerService.Orfs(Wuhan, new OrfFinderOptions(), circular: false);
            Assert.False(truncated);

            // «Найдовший ORF» бере перший ATG у рамці; справжній старт Spike (21563) задає субгеномна мРНК.
            var spike = orfs.Single(o => o.MatchesCds == "S");
            Assert.Equal((21536L, 25384L, '+', false), (spike.Start, spike.End, spike.Strand, spike.SameStartAsCds));
            Assert.Equal(1273 + 9, spike.AaLength);

            var n = orfs.Single(o => o.MatchesCds == "N");
            Assert.True(n.SameStartAsCds);
        }

        [Fact]
        public void Orf1ab_frameshift_splits_it_into_two_orfs_in_different_frames()
        {
            var (orfs, _) = ExplorerService.Orfs(Wuhan, new OrfFinderOptions(), circular: false);

            // Анотовано один ген — join(266..13468,13468..21555). Пошук ORF бачить два шматки в різних рамках:
            // перший (ORF1a, рамка +2) закінчується власним стопом 13483 і ні з яким CDS не збігається.
            var orf1a = orfs.Single(o => o is { Start: 266, Strand: '+' });
            Assert.Equal((13483L, 2, (string?)null), (orf1a.End, orf1a.Frame, orf1a.MatchesCds));

            // pp1ab: після зсуву рамки −1 на 13468 решта читається в рамці +1 — це вже інший ORF,
            // що закінчується стопом ORF1ab (21555), але не може починатися на 266.
            var orf1b = orfs.Single(o => o is { End: 21555, Strand: '+' });
            Assert.Equal((1, "orf1ab", false), (orf1b.Frame, orf1b.MatchesCds, orf1b.SameStartAsCds));
            Assert.True(orf1b.Start > 13468);
        }

        [Fact]
        public void Region_with_join_gives_the_annotated_orf1ab_protein()
        {
            var orf1ab = Wuhan.Features.First(f => f.Type == "CDS" && f.Gene == "orf1ab");
            var (nucleotides, protein) = ExplorerService.Region(Wuhan, orf1ab.Segments, Strand.Forward, GeneticCode.Standard);

            Assert.Equal(21555 - 266 + 2, nucleotides.Length);
            Assert.Equal(orf1ab.Translation, protein);
        }

        [Fact]
        public void Region_on_the_reverse_strand_of_a_plasmid_gives_bla()
        {
            var plasmid = Load("pBR322.gb");
            var bla = plasmid.Features.First(f => f.Type == "CDS" && f.Strand == Strand.Reverse);
            var (_, protein) = ExplorerService.Region(plasmid, bla.Segments, Strand.Reverse, GeneticCode.Standard);

            Assert.Equal(bla.Translation, protein);
        }

        [Fact]
        public void Session_keeps_records_by_token()
        {
            var explorer = new ExplorerService(new MemoryCache(new MemoryCacheOptions()));
            var (token, records) = explorer.Open(new StringReader(">a\nATGAAATAG\n>b\nATGCCCTAA\n"));

            Assert.Equal(2, records.Count);
            Assert.Equal("ATGCCCTAA", explorer.Record(token, 1)!.Sequence);
            Assert.Null(explorer.Record(token, 2));
            Assert.Null(explorer.Record(Guid.NewGuid(), 0));
            Assert.Throws<FormatException>(() => explorer.Open(new StringReader(">empty\n")));
        }
    }
}
