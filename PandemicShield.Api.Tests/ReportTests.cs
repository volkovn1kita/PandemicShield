using System.Text;
using PandemicShield.Api.Reports;
using PandemicShield.Contracts;
using PandemicShield.DataAccess.Entities;

namespace PandemicShield.Api.Tests
{
    /// <summary>
    /// Звіт по зразку без бази: дані запуску збираються в пам'яті з тих самих сутностей, що й у БД.
    /// Значення — з наскрізної перевірки: український ізолят GCA_059491835.1 (ST23, NDM-1 + OXA-48) і Delta з прикладів Nextclade.
    /// </summary>
    public class ReportTests
    {
        private static RunReport Bacterial()
        {
            var run = new AnalysisRunEntity("GCA_059491835.1 · Klebsiella pneumoniae", ThreatCategory.Bacteria, Guid.NewGuid(), "GCA_059491835.1");
            run.MarkStreamed(110, 5_700_000, 0, 110, "ok");
            run.RequestAmr("Klebsiella_pneumoniae");
            run.MarkAmrCompleted("2026-08-07.1", "ok");
            run.RequestMlst();
            run.MarkMlstCompleted("klebsiella", "23", "gapA(2) infB(1) mdh(1) pgi(1) phoE(9) rpoB(4) tonB(12)", "2.35.0", "ST23 (klebsiella)");

            var ndm = Hit(run.Id, "contig_7", "blaNDM-1", "AMR", "BETA-LACTAM", "CARBAPENEM", "ALLELEX");
            var oxa = Hit(run.Id, "contig_2", "blaOXA-48", "AMR", "BETA-LACTAM", "CARBAPENEM", "ALLELEX");
            // Назва з комою й лапками перевіряє екранування CSV.
            var mgrB = new AmrHitEntity(run.Id, "contig_1", 10, 200, '-', "mgrB_F35SfsTer37", "MgrB, \"truncated\", colistin", "core",
                "AMR", "POINT", "COLISTIN", "COLISTIN", "POINTX", 100, 99.5, null);
            var stress = Hit(run.Id, "contig_3", "arsC", "STRESS", "ARSENIC", "ARSENIC", "HMM");

            return new RunReport(run, [], [ndm, oxa, mgrB, stress],
            [
                ThreatAlertEntity.FromAmrFinder(run.Id, "Карбапенемази: стійкість до карбапенемів", ndm),
                ThreatAlertEntity.FromMlst(run.Id, "K. pneumoniae ST23: гіпервірулентний клон (hvKp)", "klebsiella", "23")
            ], DateTime.UtcNow);
        }

        private static RunReport Virus()
        {
            var run = new AnalysisRunEntity("sars-cov-2-examples.fasta", ThreatCategory.Virus);
            run.MarkStreamed(2, 59_000, 0, 2, "ok");
            run.RequestNextclade("nextstrain/sars-cov-2/wuhan-hu-1/orfs");
            var delta = new SequenceResultEntity(run.Id, "USA/OKPHLCOV0000666/2021", "nextstrain/sars-cov-2/wuhan-hu-1/orfs", "2026-10-01--00-00-00Z");
            delta.SetSummary("21J", "21J (Delta)", "AY.3", "good", 12.5, 0.98, false, 41, 2, 0, 600, 0, null);
            var tooShort = new SequenceResultEntity(run.Id, "too_short", "nextstrain/sars-cov-2/wuhan-hu-1/orfs", "2026-10-01--00-00-00Z");
            tooShort.SetErrors("Unable to align: seed matching failed");

            return new RunReport(run, [delta, tooShort], [],
            [
                ThreatAlertEntity.FromNextclade(run.Id, delta.Id, "SARS-CoV-2 Spike L452R", delta.SeqName, "S:L452R", 22917, 22919, 452),
                ThreatAlertEntity.FromNextclade(run.Id, delta.Id, "SARS-CoV-2 Delta", delta.SeqName, "clade:Delta (21J (Delta))", 0, 0, 0)
            ], DateTime.UtcNow);
        }

        private static AmrHitEntity Hit(Guid runId, string contig, string symbol, string type, string @class, string subclass, string method) =>
            new(runId, contig, 100, 900, '+', symbol, symbol, "core", type, type, @class, subclass, method, 100, 100, null);

        private static List<string> Lines(string csv) => csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).ToList();

        [Fact]
        public void Bacterial_csv_has_a_row_per_finding_with_sequence_type_and_alert()
        {
            var lines = Lines(CsvReport.Build(Bacterial()));

            Assert.Equal(5, lines.Count);
            Assert.StartsWith("run_id,file,species,amrfinder_db,mlst_scheme,mlst_st,", lines[0]);
            var ndm = lines.Single(l => l.Contains(",blaNDM-1,"));
            Assert.Contains(",klebsiella,23,", ndm);
            Assert.EndsWith("Карбапенемази: стійкість до карбапенемів", ndm);
            // Ім'я файлу з «·» і назва гена з комою та лапками — у лапках, лапки подвоєні.
            Assert.Contains("\"MgrB, \"\"truncated\"\", colistin\"", lines.Single(l => l.Contains("mgrB_")));
            Assert.Contains(",99.5,100,", lines.Single(l => l.Contains("mgrB_")));
        }

        [Fact]
        public void Virus_csv_has_a_row_per_genome_with_alerts()
        {
            var lines = Lines(CsvReport.Build(Virus()));

            Assert.Equal(3, lines.Count);
            Assert.Contains("USA/OKPHLCOV0000666/2021,nextstrain/sars-cov-2/wuhan-hu-1/orfs,2026-10-01--00-00-00Z,21J,21J (Delta),AY.3,good,12.5,0.98,41,2,", lines[1]);
            Assert.Contains("S:L452R; clade:Delta (21J (Delta))", lines[1]);
            Assert.EndsWith("Unable to align: seed matching failed", lines[2]);
        }

        [Fact]
        public void Csv_bytes_start_with_utf8_bom_for_excel()
        {
            byte[] bytes = CsvReport.ToBytes("a,b\r\n");
            Assert.Equal(Encoding.UTF8.GetPreamble(), bytes[..3]);
        }

        [Theory]
        [InlineData("uk")]
        [InlineData("en")]
        public void Pdf_is_generated_for_bacterial_and_viral_runs(string lang)
        {
            foreach (var report in new[] { Bacterial(), Virus() })
            {
                byte[] pdf = PdfReport.Build(report, ReportText.For(lang));
                Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
                Assert.True(pdf.Length > 5_000);
            }
        }

        [Fact]
        public void Resistance_groups_genes_by_subclass_and_puts_alerted_first()
        {
            var resistance = Bacterial().Resistance();

            Assert.Equal("CARBAPENEM", resistance[0].Subclass);
            Assert.True(resistance[0].Alert);
            Assert.Equal(["blaNDM-1", "blaOXA-48"], resistance[0].Genes);
            Assert.Contains(resistance, r => r.Subclass == "COLISTIN" && !r.Alert);
            // Гени стресу (arsC) у зведенні стійкості до антибіотиків не показуються.
            Assert.DoesNotContain(resistance, r => r.Subclass == "ARSENIC");
        }

        [Fact]
        public void File_name_is_safe_and_short()
        {
            Assert.Equal("pandemicshield-GCA_059491835.1___Klebsiella_pneumoniae.pdf", Bacterial().FileName("pdf"));
            Assert.Equal("pandemicshield-sars-cov-2-examples.csv", Virus().FileName("csv"));
        }
    }
}
