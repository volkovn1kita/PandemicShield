using System;

namespace PandemicShield.Contracts
{
    /// <summary>
    /// Знайдений маркер. Координати 1-based включно на прямому ланцюзі запису (як у NCBI/GenBank).
    /// </summary>
    public class ThreatReport
    {
        public Guid RunId { get; private set; }
        public string ThreatName { get; private set; }
        public string RecordId { get; private set; }
        public char Strand { get; private set; }
        public long NtStart { get; private set; }
        public long NtEnd { get; private set; }
        public int AaPosition { get; private set; }
        public string ProteinSequence { get; private set; }
        public ThreatCategory Category { get; private set; }

        public ThreatReport(
            Guid runId,
            string threatName,
            string recordId,
            char strand,
            long ntStart,
            long ntEnd,
            int aaPosition,
            string proteinSequence,
            ThreatCategory category)
        {
            RunId = runId;
            ThreatName = threatName;
            RecordId = recordId;
            Strand = strand;
            NtStart = ntStart;
            NtEnd = ntEnd;
            AaPosition = aaPosition;
            ProteinSequence = proteinSequence;
            Category = category;
        }
    }
}
