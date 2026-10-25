using System.Globalization;
using PandemicShield.Contracts;
using PandemicShield.DataAccess.Entities;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PandemicShield.Api.Reports
{
    /// <summary>
    /// PDF-звіт по зразку (QuestPDF). Оформлення — за docs/DESIGN.md: монохромна основа, тонкі межі, IBM Plex,
    /// червоний лише для сповіщень. На першій сторінці — що за зразок і що знайдено; далі — таблиці.
    /// </summary>
    public static class PdfReport
    {
        private const string Sans = "IBM Plex Sans";
        private const string Mono = "IBM Plex Mono";
        private const string Ink = "#1C1B19";
        private const string Muted = "#6F6D68";
        private const string Faint = "#A3A19C";
        private const string Border = "#E7E6E3";
        private const string Danger = "#D93636";

        // QuestPDF Community — безкоштовна для open-source, некомерційних проєктів і компаній з доходом до $1 млн на рік.
        private static readonly Lazy<bool> Setup = new(() =>
        {
            QuestPDF.Settings.License = LicenseType.Community;
            var assembly = typeof(PdfReport).Assembly;
            foreach (string resource in assembly.GetManifestResourceNames().Where(n => n.EndsWith(".ttf", StringComparison.Ordinal)))
            {
                using var stream = assembly.GetManifestResourceStream(resource)!;
                FontManager.RegisterFontFromStream(stream);
            }
            return true;
        });

        public static byte[] Build(RunReport report, ReportText t)
        {
            _ = Setup.Value;
            return Document.Create(document => document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(style => style.FontFamily(Sans).FontSize(9).FontColor(Ink).LineHeight(1.3f));

                page.Header().PaddingBottom(14).Row(row =>
                {
                    row.RelativeItem().Text(text =>
                    {
                        text.Span("PandemicShield").SemiBold();
                        text.Span($"  ·  {t.Title}").FontColor(Muted);
                    });
                    row.AutoItem().Text($"{t.Generated} {Date(report.GeneratedAt)}").FontColor(Muted);
                });

                page.Content().Column(column =>
                {
                    column.Spacing(16);
                    column.Item().Element(c => Sample(c, report, t));
                    column.Item().Element(c => Analysis(c, report, t));
                    if (report.HasNextclade) column.Item().Element(c => Genomes(c, report, t));
                    if (report.IsBacterial)
                    {
                        column.Item().Element(c => Mlst(c, report, t));
                        column.Item().Element(c => Resistance(c, report, t));
                        column.Item().Element(c => Findings(c, report, t));
                    }
                    column.Item().Element(c => Alerts(c, report, t));
                });

                page.Footer().PaddingTop(10).Row(row =>
                {
                    row.RelativeItem().Text(t.Footer).FontSize(7.5f).FontColor(Faint);
                    row.AutoItem().Text(text =>
                    {
                        text.DefaultTextStyle(style => style.FontSize(7.5f).FontColor(Faint));
                        text.Span($"{t.Page} ");
                        text.CurrentPageNumber();
                        text.Span(" / ");
                        text.TotalPages();
                    });
                });
            })).GeneratePdf();
        }

        // ==========================================
        // SECTIONS
        // ==========================================
        private static void Sample(IContainer container, RunReport report, ReportText t)
        {
            var run = report.Run;
            container.Column(column =>
            {
                column.Item().Text(run.FileName).FontSize(15).SemiBold();
                column.Item().PaddingTop(8).Element(c => KeyValues(c,
                [
                    (t.Type, run.Category switch
                    {
                        ThreatCategory.Virus => t.Virus,
                        ThreatCategory.Bacteria => t.Bacterium,
                        _ => t.Human
                    }),
                    (t.Received, Date(run.StartedAt)),
                    (t.Records, run.RecordCount.ToString(CultureInfo.InvariantCulture)),
                    (t.Bases, run.BaseCount.ToString("N0", CultureInfo.InvariantCulture)),
                    (t.Source, run.SourceAccession != null ? $"NCBI {run.SourceAccession}" : run.ImportId != null ? "NCBI" : t.File),
                    (t.RunId, run.Id.ToString())
                ]));
                column.Item().PaddingTop(10).Border(0.5f).BorderColor(Border).Padding(8)
                    .Text(t.Disclaimer).FontSize(8).FontColor(Muted);
            });
        }

        private static void Analysis(IContainer container, RunReport report, ReportText t)
        {
            var run = report.Run;
            var rows = new List<(string, string)>();
            if (report.HasNextclade)
            {
                var first = report.Sequences[0];
                rows.Add((t.Dataset, first.DatasetTag == null ? first.Dataset : $"{first.Dataset} ({first.DatasetTag})"));
            }
            if (report.IsBacterial)
            {
                rows.Add((t.Species, run.AmrOrganism?.Replace('_', ' ') ?? t.NotSpecified));
                rows.Add((t.AmrDatabase, run.AmrDatabaseVersion ?? "—"));
                rows.Add((t.SequenceType, run.MlstVersion == null ? "—" : $"mlst {run.MlstVersion}, PubMLST"));
            }
            if (rows.Count == 0) return;

            Section(container, t.Analysis, c => KeyValues(c, rows));
        }

        private static void Genomes(IContainer container, RunReport report, ReportText t)
        {
            Section(container, $"{t.Genomes} · {report.Sequences.Count}", c => c.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(2.9f);
                    columns.RelativeColumn(2.1f);
                    columns.RelativeColumn(1.2f);
                    columns.RelativeColumn(1);
                    columns.RelativeColumn(1);
                    columns.RelativeColumn(1.2f);
                    columns.RelativeColumn(2.4f);
                });
                Header(table, t.Genome, t.Clade, t.Lineage, t.Qc, t.Coverage, t.Changes, t.Alerts);

                foreach (var s in report.Sequences)
                {
                    bool failed = s.Errors != null;
                    table.Cell().Element(Cell).Text(s.SeqName);
                    table.Cell().Element(Cell).Text(failed ? t.NotAnalysed : s.CladeDisplay ?? s.Clade ?? "—").FontColor(failed ? Danger : Ink);
                    table.Cell().Element(Cell).Text(s.Lineage ?? "—").FontFamily(Mono).FontSize(8);
                    table.Cell().Element(Cell).Text(t.QcStatus(s.QcStatus));
                    table.Cell().Element(Cell).Text(s.Coverage == null ? "—" : $"{s.Coverage * 100:0.0}%");
                    table.Cell().Element(Cell).Text(failed ? "" : $"{s.TotalSubstitutions} / {s.TotalDeletions}");
                    table.Cell().Element(Cell).Text(string.Join(", ", report.AlertsFor(s).Select(a => ShortVariant(a.Variant))))
                        .FontFamily(Mono).FontSize(8).FontColor(Danger);
                }
            }));
        }

        private static void Mlst(IContainer container, RunReport report, ReportText t)
        {
            var run = report.Run;
            bool known = run.MlstSequenceType is { Length: > 0 } st && st.All(char.IsDigit);
            var mlstAlerts = report.Alerts.Where(a => a.Source == ThreatAlertEntity.MlstSource).ToList();

            Section(container, t.SequenceType, c => c.Column(column =>
            {
                column.Item().Text(text =>
                {
                    if (run.MlstStatus != ToolStatus.Completed)
                    {
                        text.Span(t.MlstPending).FontColor(Muted);
                        return;
                    }
                    text.Span(known ? $"ST{run.MlstSequenceType}" : "ST —").FontSize(13).SemiBold().FontColor(mlstAlerts.Count > 0 ? Danger : Ink);
                    if (run.MlstScheme != null) text.Span($"   {t.Scheme}: {run.MlstScheme}").FontColor(Muted);
                });
                if (run.MlstAlleles != null)
                    column.Item().PaddingTop(3).Text($"{t.Alleles}: {run.MlstAlleles}").FontFamily(Mono).FontSize(8).FontColor(Muted);
                if (!known && run.MlstMessage != null)
                    column.Item().PaddingTop(3).Text(run.MlstMessage).FontSize(8).FontColor(Muted);
                foreach (var alert in mlstAlerts)
                    column.Item().PaddingTop(3).Text(alert.ThreatName).FontColor(Danger);
            }));
        }

        private static void Resistance(IContainer container, RunReport report, ReportText t)
        {
            var resistance = report.Resistance();
            Section(container, t.Resistance, c =>
            {
                if (resistance.Count == 0)
                {
                    c.Text(t.NoResistance).FontColor(Muted);
                    return;
                }
                c.Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(5);
                    });
                    Header(table, t.Drug, t.GenesAndMutations);
                    foreach (var (subclass, genes, alert) in resistance)
                    {
                        table.Cell().Element(Cell).Text(t.DrugName(subclass)).FontColor(alert ? Danger : Ink);
                        table.Cell().Element(Cell).Text(string.Join(", ", genes)).FontFamily(Mono).FontSize(8);
                    }
                });
            });
        }

        private static void Findings(IContainer container, RunReport report, ReportText t)
        {
            var amr = report.AmrHits.Where(h => h.Type == "AMR").ToList();
            if (amr.Count == 0) return;
            var alerted = report.Alerts.Where(a => a.Source == ThreatAlertEntity.AmrFinderSource).Select(a => a.Variant).ToHashSet();

            Section(container, $"{t.Findings} · {amr.Count}", c => c.Column(column =>
            {
                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        // Назви контигів NCBI довгі (DBPZJG010000076.1): колонці позиції — найбільше місця.
                        columns.RelativeColumn(1.5f);
                        columns.RelativeColumn(2.2f);
                        columns.RelativeColumn(3.1f);
                        columns.RelativeColumn(0.9f);
                        columns.RelativeColumn(1);
                    });
                    Header(table, t.Gene, t.ClassCol, t.Location, t.Identity, t.Method);
                    foreach (var h in amr)
                    {
                        table.Cell().Element(Cell).Text(h.ElementSymbol).FontFamily(Mono).FontSize(8)
                            .FontColor(alerted.Contains(h.ElementSymbol) ? Danger : Ink);
                        table.Cell().Element(Cell).Text(h.Class == h.Subclass ? h.Class : $"{h.Class} / {h.Subclass}").FontSize(8);
                        table.Cell().Element(Cell).Text($"{h.ContigId}:{h.Start}-{h.Stop} {h.Strand}").FontFamily(Mono).FontSize(7.5f).FontColor(Muted);
                        table.Cell().Element(Cell).Text(h.IdentityPercent == null ? "—" : $"{h.IdentityPercent:0.0}%");
                        table.Cell().Element(Cell).Text(h.Method).FontFamily(Mono).FontSize(7.5f).FontColor(Muted);
                    }
                });
                int stress = report.AmrHits.Count(h => h.Type == "STRESS"), virulence = report.AmrHits.Count(h => h.Type == "VIRULENCE");
                if (stress + virulence > 0)
                    column.Item().PaddingTop(6).Text(t.OtherFindings(stress, virulence)).FontSize(8).FontColor(Muted);
            }));
        }

        private static void Alerts(IContainer container, RunReport report, ReportText t)
        {
            Section(container, $"{t.Alerts} · {report.Alerts.Count}", c =>
            {
                if (report.Alerts.Count == 0)
                {
                    c.Text(t.NoAlerts).FontColor(Muted);
                    return;
                }
                c.Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(4);
                        columns.RelativeColumn(1.6f);
                        columns.RelativeColumn(2.4f);
                    });
                    Header(table, t.Rule, t.Variant, t.Where);
                    foreach (var a in report.Alerts)
                    {
                        table.Cell().Element(Cell).Text(a.ThreatName);
                        table.Cell().Element(Cell).Text(ShortVariant(a.Variant)).FontFamily(Mono).FontSize(8).FontColor(Danger);
                        table.Cell().Element(Cell).Text(Where(a)).FontFamily(Mono).FontSize(7.5f).FontColor(Muted);
                    }
                });
            });
        }

        // ==========================================
        // HELPERS
        // ==========================================
        private static void Section(IContainer container, string title, Action<IContainer> content)
        {
            container.Column(column =>
            {
                column.Item().PaddingBottom(6).Text(title).FontSize(10.5f).SemiBold();
                column.Item().Element(content);
            });
        }

        private static void KeyValues(IContainer container, IReadOnlyList<(string Key, string Value)> rows)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(130);
                    columns.RelativeColumn();
                });
                foreach (var (key, value) in rows)
                {
                    table.Cell().PaddingVertical(1.5f).Text(key).FontColor(Muted);
                    table.Cell().PaddingVertical(1.5f).Text(value);
                }
            });
        }

        private static void Header(TableDescriptor table, params string[] titles)
        {
            table.Header(header =>
            {
                foreach (string title in titles)
                    header.Cell().BorderBottom(0.75f).BorderColor(Faint).PaddingVertical(4).PaddingRight(6)
                        .Text(title).FontSize(7.5f).SemiBold().FontColor(Muted);
            });
        }

        private static IContainer Cell(IContainer container) =>
            container.BorderBottom(0.5f).BorderColor(Border).PaddingVertical(3.5f).PaddingRight(6);

        private static string Where(ThreatAlertEntity alert) => alert.Source switch
        {
            ThreatAlertEntity.NextcladeSource => alert.NtStart > 0 ? $"{alert.RecordId} · ref {alert.NtStart}" : alert.RecordId,
            ThreatAlertEntity.MlstSource => alert.RecordId,
            _ => $"{alert.RecordId}:{alert.NtStart}-{alert.NtEnd} {alert.Strand}"
        };

        // "lineage:BA.2.86* (23I (BA.2.86), JN.3.3)" → "BA.2.86*", як у застосунку.
        private static string ShortVariant(string? variant)
        {
            if (string.IsNullOrEmpty(variant)) return "";
            string head = variant.Split(" (")[0];
            int colon = head.IndexOf(':');
            return colon > 0 && (head.StartsWith("lineage:") || head.StartsWith("clade:")) ? head[(colon + 1)..] : head;
        }

        private static string Date(DateTime utc) => utc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
    }
}
