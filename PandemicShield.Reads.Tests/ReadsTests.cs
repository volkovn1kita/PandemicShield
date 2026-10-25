namespace PandemicShield.Reads.Tests
{
    /// <summary>
    /// Розбір виводу інструментів воркера Reads. <c>seqkit.*.tsv</c> і <c>references.tsv</c> — справжній вивід з образу
    /// (seqkit 2.9.0 на прочитаннях, симульованих docs/scripts/simulate_reads.py з геному Delta USA/OKPHLCOV0000666/2021).
    /// </summary>
    public class ReadsTests
    {
        private static string DataPath(string name) => Path.Combine(AppContext.BaseDirectory, "Data", name);

        private static ReadStats Stats(string file)
        {
            using var reader = new StreamReader(DataPath(file));
            return ReadStats.Parse(reader);
        }

        [Fact]
        public void Paired_illumina_stats_are_summed_over_both_files()
        {
            var stats = Stats("seqkit.illumina.tsv");

            Assert.Equal((39_038L, 5_855_700L, 150.0, 150L), (stats.Reads, stats.Bases, stats.AverageLength, stats.N50));
            Assert.Equal(100.0, stats.Q30Percent);
        }

        [Fact]
        public void Nanopore_stats_have_long_reads_and_low_q30()
        {
            var stats = Stats("seqkit.nanopore.tsv");

            Assert.Equal((353L, 2_935_606L, 9374L), (stats.Reads, stats.Bases, stats.N50));
            Assert.Equal(8316, stats.AverageLength, 0);
            Assert.Equal(0.0, stats.Q30Percent);
        }

        [Fact]
        public void Reference_panel_lists_every_installed_dataset()
        {
            using var reader = new StreamReader(DataPath("references.tsv"));
            var panel = ReferencePanel.Read(reader);

            Assert.Equal(6, panel.Count);
            Assert.Contains(new PanelReference("nextstrain/sars-cov-2/wuhan-hu-1/orfs", "MN908947"), panel);
            Assert.Contains(new PanelReference("nextstrain/flu/h3n2/ha/EPI1857216", "EPI_ISL_1563628"), panel);
        }

        [Fact]
        public void Virus_is_the_reference_with_most_mapped_reads_unless_a_dataset_is_chosen()
        {
            PanelReference sars = new("nextstrain/sars-cov-2/wuhan-hu-1/orfs", "MN908947"), h3 = new("nextstrain/flu/h3n2/ha/EPI1857216", "EPI_ISL_1563628");
            var mapped = new Dictionary<string, long> { ["MN908947"] = 12, ["EPI_ISL_1563628"] = 4_500 };

            Assert.Equal((h3, 4_500L), ReferencePanel.Choose([sars, h3], mapped, "auto"));
            Assert.Equal((h3, 4_500L), ReferencePanel.Choose([sars, h3], mapped, null));
            // Явно обраний набір перемагає, навіть якщо прочитань на нього менше (а далі спрацює поріг MinMappedReads).
            Assert.Equal((sars, 12L), ReferencePanel.Choose([sars, h3], mapped, sars.Dataset));
            Assert.Throws<InvalidOperationException>(() => ReferencePanel.Choose([sars], mapped, "nextstrain/mpox/all-clades"));
        }

        [Fact]
        public void Idxstats_skip_the_unmapped_line()
        {
            var mapped = ReferencePanel.ReadIdxstats(new StringReader("MN908947\t29903\t39123\t0\nNC_063383\t197209\t0\t0\n*\t0\t0\t15\n"));
            Assert.Equal(new Dictionary<string, long> { ["MN908947"] = 39123, ["NC_063383"] = 0 }, mapped);
        }

        [Fact]
        public void Depth_gives_mean_and_breadth_above_threshold()
        {
            var depth = DepthSummary.Read(new StringReader("MN908947\t1\t0\nMN908947\t2\t5\nMN908947\t3\t10\nMN908947\t4\t45\n"), minDepth: 10);

            Assert.Equal((4L, 15.0, 0.5), (depth.Length, depth.MeanDepth, depth.Breadth));
        }

        [Fact]
        public void Assembly_n50_is_the_contig_that_reaches_half_of_the_total()
        {
            // Контиги 100, 100 (c3 — два рядки по 60 і 40), 80, 16, 4: 100 + 100 = 200 ≥ 300 / 2 → N50 = 100.
            var stats = AssemblyStats.FromFasta(new StringReader(">c1\nACGT\n>c2\n" + new string('A', 80) + "\n>c3\n" + new string('C', 60) + "\n" +
                                                                 new string('G', 40) + "\n>c4\n" + new string('T', 100) + "\n>c5\n" + new string('A', 16)));

            Assert.Equal((5, 300L, 100L, 100L), (stats.Contigs, stats.TotalLength, stats.N50, stats.Largest));
            Assert.Equal(new AssemblyStats(0, 0, 0, 0), AssemblyStats.FromLengths([]));
        }

        [Fact]
        public void Consensus_counts_undetermined_bases()
        {
            var (length, n) = ConsensusStats.Read(new StringReader(">s\nNNACGT\nacgtnn\n"));
            Assert.Equal((12L, 4.0 / 12), (length, n));
        }

        [Theory]
        [InlineData("KP-12_S3_L001_R1_001.fastq.gz", "KP-12_S3_L001")]
        [InlineData("delta_illumina_R1.fastq.gz", "delta_illumina")]
        [InlineData("SRR1234567_1.fastq", "SRR1234567")]
        [InlineData("barcode05.fq.gz", "barcode05")]
        public void Sample_name_drops_read_and_extension_suffixes(string file, string sample)
        {
            Assert.Equal(sample, ReadsNaming.SampleName([Path.Combine("/data/imports/x/reads", file)]));
        }
    }
}
