using System.Globalization;
using System.Text;
using PandemicShield.DataAccess.Entities;

namespace PandemicShield.Api.Reports
{
    /// <summary>
    /// CSV для подальшої обробки (Excel, R, pandas): назви колонок англійською й стабільні, числа з крапкою (RFC 4180).
    /// Віруси — рядок на геном; бактерії — рядок на знахідку AMRFinderPlus (з ST у кожному рядку); інакше — рядок на сповіщення.
    /// </summary>
    public static class CsvReport
    {
        public static string Build(RunReport report)
        {
            var csv = new StringBuilder();
            var run = report.Run;

            if (report.HasNextclade)
            {
                Row(csv, "run_id", "file", "sequence", "dataset", "dataset_tag", "clade", "clade_name", "lineage", "qc_status", "qc_score",
                    "coverage", "substitutions", "deletions", "insertions", "missing", "frameshifts", "alerts", "errors");
                foreach (var s in report.Sequences)
                {
                    Row(csv, run.Id, run.FileName, s.SeqName, s.Dataset, s.DatasetTag, s.Clade, s.CladeDisplay, s.Lineage, s.QcStatus,
                        s.QcScore, s.Coverage, s.TotalSubstitutions, s.TotalDeletions, s.TotalInsertions, s.TotalMissing, s.TotalFrameShifts,
                        string.Join("; ", report.AlertsFor(s).Select(a => a.Variant)), s.Errors);
                }
            }
            else if (report.IsBacterial)
            {
                var alertByGene = report.Alerts
                    .Where(a => a.Source == ThreatAlertEntity.AmrFinderSource)
                    .GroupBy(a => (a.RecordId, a.NtStart, a.Variant))
                    .ToDictionary(g => g.Key, g => string.Join("; ", g.Select(a => a.ThreatName)));
                Row(csv, "run_id", "file", "species", "amrfinder_db", "mlst_scheme", "mlst_st", "mlst_alleles", "gene", "name", "type", "subtype",
                    "class", "subclass", "contig", "start", "stop", "strand", "identity_pct", "coverage_pct", "method", "closest_reference", "alerts");
                foreach (var h in report.AmrHits)
                {
                    Row(csv, run.Id, run.FileName, run.AmrOrganism, run.AmrDatabaseVersion, run.MlstScheme, run.MlstSequenceType, run.MlstAlleles,
                        h.ElementSymbol, h.ElementName, h.Type, h.Subtype, h.Class, h.Subclass, h.ContigId, h.Start, h.Stop, h.Strand,
                        h.IdentityPercent, h.CoveragePercent, h.Method, h.ClosestReference,
                        alertByGene.GetValueOrDefault((h.ContigId, h.Start, h.ElementSymbol)));
                }
            }
            else
            {
                Row(csv, "run_id", "file", "rule", "source", "variant", "record", "strand", "start", "end", "protein_position");
                foreach (var a in report.Alerts)
                    Row(csv, run.Id, run.FileName, a.ThreatName, a.Source, a.Variant, a.RecordId, a.Strand, a.NtStart, a.NtEnd, a.AaPosition);
            }

            return csv.ToString();
        }

        /// <summary>UTF-8 з BOM: інакше Excel відкриває кирилицю (назви правил) кракозябрами.</summary>
        public static byte[] ToBytes(string csv) => [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(csv)];

        private static void Row(StringBuilder csv, params object?[] values)
        {
            csv.AppendJoin(',', values.Select(Field));
            csv.Append("\r\n");
        }

        private static string Field(object? value)
        {
            string text = value switch
            {
                null => string.Empty,
                double d => d.ToString("0.####", CultureInfo.InvariantCulture),
                IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
                _ => value.ToString() ?? string.Empty
            };
            return text.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? $"\"{text.Replace("\"", "\"\"")}\"" : text;
        }
    }
}
