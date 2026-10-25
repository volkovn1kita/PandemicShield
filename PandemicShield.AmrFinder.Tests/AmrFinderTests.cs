using PandemicShield.AmrFinder.Report;
using PandemicShield.AmrFinder.Rules;
using PandemicShield.Bio.Variants;

namespace PandemicShield.AmrFinder.Tests
{
    /// <summary>
    /// Справжні звіти AMRFinderPlus 4.2.7 (база 2026-08-07.1):
    /// <list type="bullet">
    /// <item><c>kpneumoniae-demo</c> — синтетична збірка: випадкові контиги + референсні CDS blaNDM-1, blaOXA-48, blaCTX-M-15
    /// з бази NCBI Reference Gene Catalog; запуск з <c>--organism Klebsiella_pneumoniae --plus</c>.</item>
    /// <item><c>amrfinder-test-contigs</c> — офіційні тестові контиги AMRFinderPlus (test_dna.fa), <c>--organism Escherichia --plus</c>;
    /// звіт збігається з test_dna.expected з репозиторію ncbi/amr.</item>
    /// </list>
    /// </summary>
    public class AmrFinderTests
    {
        private static List<AmrHit> Load(string name)
        {
            using var reader = new StreamReader(Path.Combine(AppContext.BaseDirectory, "Data", name));
            return AmrReportReader.Read(reader);
        }

        private static readonly List<AmrHit> Klebsiella = Load("kpneumoniae-demo.amrfinder.tsv");
        private static readonly List<AmrHit> TestContigs = Load("amrfinder-test-contigs.amrfinder.tsv");

        // Ті самі правила, що в DbSeeder.
        private static readonly string[] SeedNotations =
        [
            "subclass:CARBAPENEM", "subclass:COLISTIN", "gene:blaCTX-M*", "gene:mecA", "subclass:VANCOMYCIN", "class:STX2"
        ];

        private static string[] Matched(List<AmrHit> hits, params string[] notations) =>
            AmrRuleMatcher.Match(hits, notations.Select(n => new AmrRule(Guid.NewGuid(), n, VariantPattern.Parse(n))))
                .Select(m => $"{m.Rule.Name} {m.Hit.ElementSymbol}")
                .ToArray();

        [Fact]
        public void Reads_klebsiella_report()
        {
            Assert.Equal(["blaNDM-1", "blaOXA-48", "blaCTX-M-15"], Klebsiella.Select(h => h.ElementSymbol));

            var ndm = Klebsiella[0];
            Assert.Equal(("contig_2", 1501L, 2310L, '+'), (ndm.ContigId, ndm.Start, ndm.Stop, ndm.Strand));
            Assert.Equal(("AMR", "BETA-LACTAM", "CARBAPENEM", "ALLELEX"), (ndm.Type, ndm.Class, ndm.Subclass, ndm.Method));
            Assert.Equal((100.0, 100.0), (ndm.CoveragePercent, ndm.IdentityPercent));
        }

        [Fact]
        public void Reads_official_test_report_with_points_stress_and_virulence()
        {
            Assert.Equal(24, TestContigs.Count);
            Assert.Contains(TestContigs, h => h is { ElementSymbol: "pmrB_C84R", Subtype: "POINT", Subclass: "COLISTIN" });
            Assert.Contains(TestContigs, h => h is { Type: "STRESS", Class: "QUATERNARY AMMONIUM" });
            Assert.Contains(TestContigs, h => h is { Type: "VIRULENCE", ElementSymbol: "stxA2" });
            Assert.Contains(TestContigs, h => h is { ElementSymbol: "aph(3'')-Ib", Strand: '-' });
        }

        [Fact]
        public void Compound_subclasses_are_split()
        {
            var promoter = TestContigs.First(h => h.ElementSymbol == "blaTEMp_G162T");

            Assert.Contains("PIPERACILLIN-TAZOBACTAM", promoter.Subclasses);
            Assert.Equal(["subclass:PIPERACILLIN-TAZOBACTAM blaTEMp_G162T"], Matched(TestContigs, "subclass:PIPERACILLIN-TAZOBACTAM").Distinct());
        }

        [Fact]
        public void Seed_rules_flag_carbapenemases_and_esbl_in_klebsiella()
        {
            Assert.Equal(
                ["subclass:CARBAPENEM blaNDM-1", "subclass:CARBAPENEM blaOXA-48", "gene:blaCTX-M* blaCTX-M-15"],
                Matched(Klebsiella, SeedNotations));
        }

        [Fact]
        public void Seed_rules_flag_colistin_vancomycin_and_shiga_toxin_in_test_contigs()
        {
            Assert.Equal(
                ["subclass:COLISTIN pmrB_C84R", "subclass:VANCOMYCIN vanG", "class:STX2 stxA2", "class:STX2 stx2a_operon", "class:STX2 stxB2"],
                Matched(TestContigs, SeedNotations));
        }

        [Fact]
        public void Gene_pattern_without_star_matches_exact_allele_only()
        {
            Assert.Single(Matched(Klebsiella, "gene:blaNDM-1"));
            Assert.Empty(Matched(Klebsiella, "gene:blaNDM"));
            Assert.Single(Matched(Klebsiella, "gene:blaNDM*"));
        }

        [Fact]
        public void Report_with_old_column_names_is_supported()
        {
            const string tsv =
                "Protein identifier\tContig id\tStart\tStop\tStrand\tGene symbol\tSequence name\tScope\tElement type\tElement subtype\tClass\tSubclass\tMethod\n" +
                "NA\tc1\t10\t891\t+\tblaKPC-2\tcarbapenem-hydrolyzing class A beta-lactamase KPC-2\tcore\tAMR\tAMR\tBETA-LACTAM\tCARBAPENEM\tALLELEX\n";

            var hit = AmrReportReader.Read(new StringReader(tsv)).Single();

            Assert.Equal(("blaKPC-2", "AMR", "CARBAPENEM"), (hit.ElementSymbol, hit.Type, hit.Subclass));
            Assert.Null(hit.IdentityPercent);
        }
    }
}
