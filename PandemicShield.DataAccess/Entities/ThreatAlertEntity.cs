using PandemicShield.Contracts;
using System;

namespace PandemicShield.DataAccess.Entities
{
    public class ThreatAlertEntity
    {
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
    }
}
