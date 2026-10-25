using PandemicShield.Bio.Alignment;
using PandemicShield.Bio.Formats;

namespace PandemicShield.Bio.Tests
{
    /// <summary>
    /// Ядро режиму Explorer на справжніх файлах NCBI:
    /// <list type="bullet">
    /// <item><c>MN908947.gb</c> — SARS-CoV-2 Wuhan-Hu-1 (GenBank, efetch rettype=gbwithparts);</item>
    /// <item><c>pBR322.gb</c> — класичний кільцевий вектор pBR322 (J01749.1) з генами tet, rop і bla;</item>
    /// <item><c>delta-OKPHLCOV0000666.fasta</c> — геном Delta (AY.3) з прикладів Nextstrain; Nextclade знаходить у ньому
    /// 38 замін і 13 делетованих нуклеотидів відносно MN908947.</item>
    /// </list>
    /// </summary>
    public class ExplorerCoreTests
    {
        private static string DataPath(string name) => Path.Combine(AppContext.BaseDirectory, "Data", name);

        private static SequenceRecord Load(string name)
        {
            using var reader = new StreamReader(DataPath(name));
            return SequenceFile.Read(reader).Single();
        }

        private static readonly SequenceRecord Wuhan = Load("MN908947.gb");
        private static readonly SequenceRecord PBr322 = Load("pBR322.gb");

        // ==========================================
        // GenBank
        // ==========================================
        [Fact]
        public void Reads_genbank_header_sequence_and_topology()
        {
            Assert.Equal(("MN908947.3", 29903, false), (Wuhan.Id, Wuhan.Sequence.Length, Wuhan.Circular));
            Assert.StartsWith("Severe acute respiratory syndrome coronavirus 2 isolate Wuhan-Hu-1", Wuhan.Description);
            Assert.StartsWith("ATTAAAGGTTTATACC", Wuhan.Sequence);

            Assert.Equal(("J01749.1", 4361, true), (PBr322.Id, PBr322.Sequence.Length, PBr322.Circular));
        }

        [Fact]
        public void Orf1ab_is_a_join_with_a_programmed_frameshift()
        {
            var orf1ab = Wuhan.Features.First(f => f.Type == "CDS" && f.Gene == "orf1ab");

            // Нуклеотид 13468 читається двічі: рамка зсувається на −1, тож пошук ORF такий ген не знайде.
            Assert.Equal([new Segment(266, 13468), new Segment(13468, 21555)], orf1ab.Segments);
            Assert.Equal((266L, 21555L, Strand.Forward), (orf1ab.Start, orf1ab.End, orf1ab.Strand));
            Assert.Equal(7096, orf1ab.Translation!.Length);
        }

        [Fact]
        public void Spike_cds_has_its_translation()
        {
            var spike = Wuhan.Features.Single(f => f.Type == "CDS" && f.Gene == "S");

            Assert.Equal((21563L, 25384L), (spike.Start, spike.End));
            Assert.Equal("surface glycoprotein", spike.Product);
            Assert.Equal(1273, spike.Translation!.Length);
            Assert.StartsWith("MFVFLVLLPLVSSQCVNL", spike.Translation);
        }

        [Fact]
        public void Reads_complement_features_of_a_plasmid()
        {
            var cds = PBr322.Features.Where(f => f.Type == "CDS").ToList();

            Assert.Equal(3, cds.Count);
            var bla = cds.Single(f => f.Strand == Strand.Reverse);
            Assert.Equal([new Segment(3293, 4153)], bla.Segments);
        }

        [Theory]
        [InlineData("complement(join(10..20,30..40))", Strand.Reverse, "30-40,10-20", false)]
        [InlineData("join(complement(30..40),complement(10..20))", Strand.Reverse, "30-40,10-20", false)]
        [InlineData("<1..>100", Strand.Forward, "1-100", true)]
        [InlineData("join(1..10,J00194.1:100..202)", Strand.Forward, "1-10", false)]
        [InlineData("467", Strand.Forward, "467-467", false)]
        public void Parses_genbank_locations(string location, Strand strand, string segments, bool partial)
        {
            var (parsed, parsedStrand, parsedPartial) = GenBankReader.ParseLocation(location);

            Assert.Equal((strand, partial), (parsedStrand, parsedPartial));
            Assert.Equal(segments, string.Join(",", parsed.Select(s => $"{s.Start}-{s.End}")));
        }

        [Fact]
        public void Reads_multi_record_fasta_and_normalises_rna()
        {
            var records = SequenceFile.Read(new StringReader(">a first\nacgu\nNN\n>b\nAC-T\n"));

            Assert.Equal(["a", "b"], records.Select(r => r.Id));
            Assert.Equal(("first", "ACGTNN"), (records[0].Description, records[0].Sequence));
            Assert.Equal("ACNT", records[1].Sequence);
            Assert.Throws<FormatException>(() => SequenceFile.Read(new StringReader("hello")));
        }

        // ==========================================
        // Кільцеві ORF
        // ==========================================
        [Fact]
        public void Circular_search_finds_the_same_orfs_as_linear_when_nothing_crosses_the_origin()
        {
            var linear = OrfFinder.Find(PBr322.Id, PBr322.Sequence).Select(o => (o.Strand, o.Start, o.End)).ToHashSet();
            var circular = OrfFinder.FindCircular(PBr322.Id, PBr322.Sequence);

            // Лінійний пошук — підмножина кільцевого; кільцевий додає лише ORF через «нуль».
            Assert.All(linear, l => Assert.Contains(l, circular.Select(o => (o.Strand, o.Start, o.End))));
            Assert.All(circular.Where(o => !linear.Contains((o.Strand, o.Start, o.End))), o => Assert.True(o.End > PBr322.Sequence.Length));
        }

        [Fact]
        public void Gene_crossing_the_origin_is_found_only_in_circular_mode()
        {
            // Повертаємо плазміду так, щоб «нуль» припав посередині гена tet (86..1276).
            const int cut = 600;
            string rotated = PBr322.Sequence[cut..] + PBr322.Sequence[..cut];
            int length = rotated.Length;
            long tetStart = 86 - 1 - cut + length, tetEnd = 1276 - cut + length;   // 0-based, кінець не включається

            var circular = OrfFinder.FindCircular("rotated", rotated);
            var tet = circular.Single(o => o.Strand == Strand.Forward && o.End == tetEnd);
            Assert.Equal(tetStart, tet.Start);
            Assert.Equal(1276 - 86 + 1, tet.Length);
            Assert.Equal(PBr322.Features.First(f => f.Type == "CDS" && f.Gene == "tet").Translation, tet.Protein);

            Assert.DoesNotContain(OrfFinder.Find("rotated", rotated), o => o.Strand == Strand.Forward && o.Protein == tet.Protein);
        }

        [Fact]
        public void Reverse_gene_crossing_the_origin_is_found_too()
        {
            // bla на зворотному ланцюзі (complement(3293..4153)); ріжемо посередині.
            const int cut = 3700;
            string rotated = PBr322.Sequence[cut..] + PBr322.Sequence[..cut];
            string bla = PBr322.Features.First(f => f.Type == "CDS" && f.Strand == Strand.Reverse).Translation!;

            var orf = OrfFinder.FindCircular("rotated", rotated).Single(o => o.Strand == Strand.Reverse && o.Protein == bla);
            Assert.True(orf.End > rotated.Length);
            Assert.Equal(4153 - 3293 + 1, orf.Length);
        }

        // ==========================================
        // Вирівнювання
        // ==========================================
        [Fact]
        public void Aligns_substitution_insertion_and_deletion()
        {
            // Неповторювані послідовності: кожну відмінність можна вирівняти лише одним способом.
            const string a = "GATCCTAGGCATTGCAACGTAGCTTGACCGTAAGCTGGATTCCAGTGACTAG";
            // Заміна G→T у позиції 9; вставка AAA між G22 і C23 (сусіди не A, тож місце однозначне); делеція TC у 41–42.
            string b = a[..8] + "T" + a[9..22] + "AAA" + a[22..40] + a[42..];

            var alignment = PairwiseAligner.Align(a, b, ScoringScheme.Nucleotide);

            Assert.Equal(
            [
                new AlignmentDifference(DifferenceKind.Substitution, 9, 9, "G", "T"),
                new AlignmentDifference(DifferenceKind.Insertion, 22, 23, "", "AAA"),
                new AlignmentDifference(DifferenceKind.Deletion, 41, 43, "TC", "")
            ], alignment.Differences);
        }

        [Fact]
        public void Different_end_trimming_is_not_reported_as_mutations()
        {
            const string genome = "TTGACCATGGCGCTAGCATCGATCGGATCCAAGCTTGACG";
            var alignment = PairwiseAligner.Align(genome, genome[5..^7], ScoringScheme.Nucleotide);

            Assert.Empty(alignment.Differences);
            Assert.Equal(1.0, alignment.Identity);
        }

        [Fact]
        public void Wuhan_vs_delta_gives_the_same_substitutions_and_deletions_as_nextclade()
        {
            string delta;
            using (var reader = new StreamReader(DataPath("delta-OKPHLCOV0000666.fasta"))) delta = SequenceFile.Read(reader).Single().Sequence;

            var alignment = PairwiseAligner.Align(Wuhan.Sequence, delta, ScoringScheme.Nucleotide);

            Assert.Equal(38, alignment.Differences.Count(d => d.Kind == DifferenceKind.Substitution));
            Assert.Equal(13, alignment.Differences.Where(d => d.Kind == DifferenceKind.Deletion).Sum(d => d.A.Length));
            Assert.Contains(alignment.Differences, d => d is { Kind: DifferenceKind.Substitution, PositionA: 23403, A: "A", B: "G" });   // S:D614G
            Assert.Contains(alignment.Differences, d => d is { Kind: DifferenceKind.Deletion, PositionA: 22029, A: "AGTTCA" });           // S:E156-/F157-
        }

        [Fact]
        public void Protein_alignment_shows_delta_spike_changes()
        {
            string wuhanSpike = Wuhan.Features.Single(f => f.Type == "CDS" && f.Gene == "S").Translation!;
            // Spike Delta: T19R, E156G + делеція 157–158, L452R, T478K, D614G, P681R, D950N.
            string deltaSpike = wuhanSpike[..18] + "R" + wuhanSpike[19..155] + "G" + wuhanSpike[158..451] + "R" + wuhanSpike[452..477] + "K" +
                                wuhanSpike[478..613] + "G" + wuhanSpike[614..680] + "R" + wuhanSpike[681..949] + "N" + wuhanSpike[950..];

            var alignment = PairwiseAligner.Align(wuhanSpike, deltaSpike, ScoringScheme.Protein);

            string[] changes = alignment.Differences
                .Select(d => d.Kind == DifferenceKind.Deletion ? $"{d.A}{d.PositionA}-" : $"{d.A}{d.PositionA}{d.B}")
                .ToArray();
            Assert.Contains("T19R", changes);
            Assert.Contains("L452R", changes);
            Assert.Contains("D614G", changes);
            Assert.Contains("P681R", changes);
            Assert.Equal(2, alignment.Differences.Where(d => d.Kind == DifferenceKind.Deletion).Sum(d => d.A.Length));
            Assert.True(alignment.Identity > 0.99);
        }

        [Fact]
        public void Blosum62_scores_identity_and_similar_residues()
        {
            Assert.Equal(11, Blosum62.Score('W', 'W'));
            Assert.Equal(3, Blosum62.Score('I', 'V'));
            Assert.Equal(-4, Blosum62.Score('W', 'D'));
        }
    }
}
