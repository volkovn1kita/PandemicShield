using Microsoft.EntityFrameworkCore;
using PandemicShield.Contracts;
using PandemicShield.DataAccess.Data;
using PandemicShield.DataAccess.Entities;

namespace PandemicShield.Api.Reports
{
    /// <summary>
    /// Усе, що потрібно для звіту по зразку: запуск, результати Nextclade або AMRFinderPlus і MLST, сповіщення.
    /// Звіт іде з лабораторії далі, тож у ньому є версії баз і наборів даних — щоб результат можна було відтворити.
    /// </summary>
    public sealed record RunReport(
        AnalysisRunEntity Run,
        IReadOnlyList<SequenceResultEntity> Sequences,
        IReadOnlyList<AmrHitEntity> AmrHits,
        IReadOnlyList<ThreatAlertEntity> Alerts,
        DateTime GeneratedAt)
    {
        public bool IsBacterial => Run.Category == ThreatCategory.Bacteria;
        public bool HasNextclade => Sequences.Count > 0;

        public static async Task<RunReport?> LoadAsync(PandemicDbContext db, Guid runId)
        {
            var run = await db.Runs.AsNoTracking().FirstOrDefaultAsync(r => r.Id == runId);
            if (run == null) return null;

            var sequences = await db.SequenceResults.AsNoTracking().Where(s => s.RunId == runId).OrderBy(s => s.SeqName).ToListAsync();
            var hits = await db.AmrHits.AsNoTracking().Where(h => h.RunId == runId)
                .OrderBy(h => h.Type).ThenBy(h => h.Class).ThenBy(h => h.ElementSymbol).ToListAsync();
            var alerts = await db.Threats.AsNoTracking().Where(t => t.RunId == runId)
                .OrderBy(t => t.Source).ThenBy(t => t.RecordId).ThenBy(t => t.NtStart).ToListAsync();
            return new RunReport(run, sequences, hits, alerts, DateTime.UtcNow);
        }

        /// <summary>Стійкість за підкласами антибіотиків: підклас → гени (лише тип AMR, як у застосунку).</summary>
        public IReadOnlyList<(string Subclass, IReadOnlyList<string> Genes, bool Alert)> Resistance()
        {
            var alerted = Alerts.Where(a => a.Source == ThreatAlertEntity.AmrFinderSource).Select(a => a.Variant).ToHashSet();
            return AmrHits
                .Where(h => h.Type == "AMR")
                .SelectMany(h => h.Subclass.Split('/').Select(s => (Subclass: s.Trim(), Hit: h)))
                .GroupBy(x => x.Subclass)
                .Select(g => (g.Key, (IReadOnlyList<string>)g.Select(x => x.Hit.ElementSymbol).Distinct().ToList(),
                    g.Any(x => alerted.Contains(x.Hit.ElementSymbol))))
                .OrderByDescending(x => x.Item3).ThenBy(x => x.Key)
                .ToList();
        }

        public IEnumerable<ThreatAlertEntity> AlertsFor(SequenceResultEntity sequence) =>
            Alerts.Where(a => a.SequenceResultId == sequence.Id);

        /// <summary>Назва файлу звіту: ім'я запуску без небезпечних символів + розширення.</summary>
        public string FileName(string extension)
        {
            var invalid = Path.GetInvalidFileNameChars().Concat([' ', '·', ',']).ToHashSet();
            string name = new string(Run.FileName.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim('_');
            if (name.EndsWith(".fasta", StringComparison.OrdinalIgnoreCase)) name = name[..^6];
            return $"pandemicshield-{(name.Length > 60 ? name[..60] : name)}.{extension}";
        }
    }
}
