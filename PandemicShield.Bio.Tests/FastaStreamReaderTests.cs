using System.Text;

namespace PandemicShield.Bio.Tests
{
    public class FastaStreamReaderTests
    {
        private static readonly byte[] MultiRecord = Encoding.ASCII.GetBytes(
            ">seg1 Influenza A segment 4\r\n" +
            "atgaaGGCAA\r\n" +
            "TACTGG-TC\r\n" +
            ">seg2\n" +
            "ACGU ACGT 123\n" +
            "\n" +
            ">empty\n" +
            ">seg3 last line without newline\n" +
            "TTTRN");

        [Fact]
        public void Splits_records_and_strips_headers_line_breaks_and_gaps()
        {
            var records = TestSequences.ReadFasta(MultiRecord);

            Assert.Equal(4, records.Count);
            Assert.Equal(("seg1", "Influenza A segment 4", "ATGAAGGCAATACTGGTC"), records[0]);
            Assert.Equal(("seg2", "", "ACGTACGT"), records[1]);
            Assert.Equal(("empty", "", ""), records[2]);
            Assert.Equal(("seg3", "last line without newline", "TTTRN"), records[3]);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(7)]
        public void Result_does_not_depend_on_how_the_stream_is_chunked(int chunkSize)
        {
            Assert.Equal(TestSequences.ReadFasta(MultiRecord), TestSequences.ReadFasta(MultiRecord, chunkSize));
        }

        [Fact]
        public void Greater_than_sign_inside_a_line_is_not_a_header()
        {
            var records = TestSequences.ReadFasta(Encoding.ASCII.GetBytes(">a\nAC>GT\n"));

            Assert.Equal("ACNGT", records.Single().Sequence);
        }

        [Fact]
        public void Counts_symbols_that_are_not_nucleotides()
        {
            var sink = new TestSequences.CollectingSink();
            var reader = new FastaStreamReader(sink);

            reader.Feed(Encoding.ASCII.GetBytes(">protein\nMEEPQSDPSV\n"));
            reader.Complete();

            // E, P, Q не є нуклеотидами (M, S, D, V — це неоднозначні коди IUPAC): це, ймовірно, білок.
            Assert.Equal(5, reader.InvalidSymbolCount);
            Assert.Equal(1, reader.RecordCount);
            Assert.Equal(10, reader.BaseCount);
        }

        [Fact]
        public void Sequence_without_header_becomes_unnamed_record()
        {
            var records = TestSequences.ReadFasta(Encoding.ASCII.GetBytes("ACGT\nACGT\n"));

            Assert.Equal(("unnamed", "", "ACGTACGT"), records.Single());
        }
    }
}
