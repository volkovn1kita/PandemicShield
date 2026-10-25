namespace PandemicShield.Bio.Tests
{
    /// <summary>
    /// «Золоті» тести на референсному геномі SARS-CoV-2 Wuhan-Hu-1 (MN908947, файл з nextstrain/ncov).
    /// Координати генів (1-based, включно) — з анотації Nextstrain/NCBI.
    /// </summary>
    public class SarsCov2ReferenceTests
    {
        private static readonly string Genome = TestSequences.SarsCov2Reference();
        private static readonly List<Orf> Orfs = OrfFinder.Find("MN908947", Genome);

        [Fact]
        public void Reference_is_read_without_header_and_line_breaks()
        {
            Assert.Equal(29_903, Genome.Length);
            Assert.StartsWith("ATTAAAGGTTTATACC", Genome);
            Assert.All(Genome, c => Assert.Contains(c, "ACGT"));
        }

        [Theory]
        [InlineData("ORF1a", 266, 13483, 4405)]   // далі ORF1b читається лише через рамковий зсув −1
        [InlineData("ORF3a", 25393, 26220, 275)]
        [InlineData("E", 26245, 26472, 75)]
        [InlineData("M", 26523, 27191, 222)]
        [InlineData("ORF6", 27202, 27387, 61)]
        [InlineData("ORF7a", 27394, 27759, 121)]
        [InlineData("ORF7b", 27756, 27887, 43)]
        [InlineData("ORF8", 27894, 28259, 121)]
        [InlineData("N", 28274, 29533, 419)]
        [InlineData("ORF9b", 28284, 28577, 97)]   // вкладений у ген N в іншій рамці
        [InlineData("ORF10", 29558, 29674, 38)]
        public void Finds_annotated_gene(string gene, int start1Based, int end1Based, int proteinLength)
        {
            var orf = Orfs.SingleOrDefault(o => o.Strand == Strand.Forward && o.Start == start1Based - 1 && o.End == end1Based);

            Assert.True(orf is not null, $"Gene {gene} was not found as an ORF.");
            Assert.Equal(proteinLength, orf.Protein.Length);
            Assert.True(orf.IsComplete);
        }

        /// <summary>
        /// Найдовший ORF гена S починається з ATG у позиції 21536, на 9 кодонів раніше за анотований
        /// старт 21563: між ними немає стоп-кодона. Але білок Spike транслюється із субгеномної мРНК,
        /// яка починається після послідовності TRS «ACGAAC» (21556–21561), тож верхній ATG у неї не
        /// потрапляє. Пошук ORF знаходить рамку, але не справжній старт гена: це робить анотація.
        /// </summary>
        [Fact]
        public void Spike_orf_extends_upstream_of_annotated_start_to_first_in_frame_atg()
        {
            var orf = Orfs.Single(o => o.Strand == Strand.Forward && o.End == 25384);

            Assert.Equal(21536 - 1, orf.Start);
            Assert.Equal("ACGAAC", Genome.Substring(21556 - 1, 6));
            Assert.Equal("MFLLTTKRT", orf.Protein[..9]);
            Assert.Equal(9 + 1273, orf.Protein.Length);
        }

        [Fact]
        public void Spike_protein_matches_known_sequence_features()
        {
            string spike = Orfs.Single(o => o.Strand == Strand.Forward && o.End == 25384).Protein[9..];

            Assert.StartsWith("MFVFLVLLPLVSSQ", spike);
            Assert.Equal(1273, spike.Length);
            Assert.Equal('D', spike[613]);                                   // D614 у дикому типі
            Assert.Contains("VYYHKNNKSWMESEFRVYSSANNC", spike);              // NTD без мутацій Delta
        }

        [Fact]
        public void D614G_motif_is_found_only_after_A23403G_mutation()
        {
            const string d614g = "QVAVLYQGVNCTEVP";   // залишки 607–621 Spike із заміною D614G

            Assert.Empty(Orfs.SelectMany(o => ProteinMotifScanner.Scan(o, d614g)));

            char[] mutant = Genome.ToCharArray();
            Assert.Equal('A', mutant[23402]);
            mutant[23402] = 'G';                       // A23403G (1-based): GAT → GGT

            var match = OrfFinder.Find("mutant", new string(mutant))
                .SelectMany(o => ProteinMotifScanner.Scan(o, d614g))
                .Single();

            Assert.Equal(25384, match.Orf.End);
            Assert.Equal(23381 - 1, match.NucleotideStart); // 21563 + 606 * 3 = 23381 (1-based)
            Assert.Equal(23381 - 1 + 15 * 3, match.NucleotideEnd);
        }

        [Fact]
        public void Delta_ntd_signature_is_not_in_the_reference()
        {
            Assert.Empty(Orfs.SelectMany(o => ProteinMotifScanner.Scan(o, "VYYHKNNKSWMESGVYSSANNC")));
        }

        [Fact]
        public void Reverse_complemented_genome_gives_the_same_genes_on_reverse_strand()
        {
            string reverse = Nucleotides.ReverseComplement(Genome);
            var reverseOrfs = OrfFinder.Find("rc", reverse);

            var expected = Orfs.Select(o => (o.Protein, Strand: o.Strand == Strand.Forward ? Strand.Reverse : Strand.Forward,
                    Start: Genome.Length - o.End, End: Genome.Length - o.Start))
                .OrderBy(x => x.Start).ThenBy(x => x.Strand);
            var actual = reverseOrfs.Select(o => (o.Protein, o.Strand, o.Start, o.End))
                .OrderBy(x => x.Start).ThenBy(x => x.Strand);

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(3_000, 15_000)]
        [InlineData(10_000, 14_000)]
        public void Windowed_search_finds_exactly_the_same_orfs(int coreSize, int flankSize)
        {
            // Фланг має бути довшим за найдовший ORF (ORF1a — 13 218 нт).
            var windowed = TestSequences.FindOrfsWindowed("MN908947", Genome, coreSize, flankSize, new OrfFinderOptions());

            Assert.Equal(Orfs, windowed);
        }

        [Fact]
        public void Raw_fasta_bytes_with_crlf_and_lowercase_give_the_same_result()
        {
            string text = File.ReadAllText(TestSequences.DataPath("MN908947.fasta"));
            byte[] messy = System.Text.Encoding.ASCII.GetBytes(text.Replace("\n", "\r\n").ToLowerInvariant());

            var record = TestSequences.ReadFasta(messy).Single();

            Assert.Equal("mn908947", record.Id);
            Assert.Equal(Genome, record.Sequence);
        }
    }
}
