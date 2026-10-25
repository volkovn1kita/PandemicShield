using PandemicShield.Contracts;
using System;

namespace PandemicShield.DataAccess.Entities
{
    /// <summary>
    /// Сповіщення про знайдений маркер. Source визначає систему координат:
    /// "orf-motif" — координати в самому записі FASTA; "nextclade" — у референсі набору даних (наприклад, MN908947);
    /// "amrfinder" — у контигу бактеріальної збірки (RecordId); "mlst" — без координат (RecordId — схема MLST).
    /// </summary>
    public class ThreatAlertEntity
    {
        public const string OrfMotifSource = "orf-motif";
        public const string NextcladeSource = "nextclade";
        public const string AmrFinderSource = "amrfinder";
        public const string MlstSource = "mlst";

        public Guid Id { get; private set; }
        public Guid? RunId { get; private set; }
        public string ThreatName { get; private set; }
        public string RecordId { get; private set; }
        public char Strand { get; private set; }

        // Координати 1-based включно на прямому ланцюзі запису.
        public long NtStart { get; private set; }
        public long NtEnd { get; private set; }

        // Позиція маркера в білку ORF (1-based).
        public int AaPosition { get; private set; }
        public string ProteinSequence { get; private set; }
        public ThreatCategory Category { get; private set; }
        public DateTime DetectedAt { get; private set; }
        public string Source { get; private set; } = OrfMotifSource;

        // Позначення варіанта для сповіщень Nextclade (S:N501Y, clade:Delta) і посилання на послідовність.
        public string? Variant { get; private set; }
        public Guid? SequenceResultId { get; private set; }

        public ThreatAlertEntity(
            Guid? runId,
            string threatName,
            string recordId,
            char strand,
            long ntStart,
            long ntEnd,
            int aaPosition,
            string proteinSequence,
            ThreatCategory category)
        {
            Id = Guid.NewGuid();
            RunId = runId;
            ThreatName = threatName;
            RecordId = recordId;
            Strand = strand;
            NtStart = ntStart;
            NtEnd = ntEnd;
            AaPosition = aaPosition;
            ProteinSequence = proteinSequence;
            Category = category;
            DetectedAt = DateTime.UtcNow;
        }

        /// <summary>Сповіщення про тип штаму: координат немає, бо ST описує весь геном (сім генів одразу).</summary>
        public static ThreatAlertEntity FromMlst(Guid runId, string threatName, string scheme, string sequenceType)
        {
            var alert = new ThreatAlertEntity(runId, threatName, scheme, '+', 0, 0, 0, string.Empty, ThreatCategory.Bacteria);
            alert.Source = MlstSource;
            alert.Variant = $"ST{sequenceType}";
            return alert;
        }

        public static ThreatAlertEntity FromAmrFinder(Guid runId, string threatName, AmrHitEntity hit)
        {
            var alert = new ThreatAlertEntity(runId, threatName, hit.ContigId, hit.Strand, hit.Start, hit.Stop, 0,
                string.Empty, ThreatCategory.Bacteria);
            alert.Source = AmrFinderSource;
            alert.Variant = hit.ElementSymbol;
            return alert;
        }

        public static ThreatAlertEntity FromNextclade(
            Guid runId,
            Guid sequenceResultId,
            string threatName,
            string seqName,
            string variant,
            long ntStart,
            long ntEnd,
            int aaPosition)
        {
            var alert = new ThreatAlertEntity(runId, threatName, seqName, '+', ntStart, ntEnd, aaPosition,
                string.Empty, ThreatCategory.Virus);
            alert.Source = NextcladeSource;
            alert.Variant = variant;
            alert.SequenceResultId = sequenceResultId;
            return alert;
        }
    }
}
