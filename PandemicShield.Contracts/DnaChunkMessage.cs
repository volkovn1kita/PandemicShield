using System;

namespace PandemicShield.Contracts
{
    /// <summary>
    /// Вікно одного FASTA-запису (див. PandemicShield.Bio.SequenceWindow).
    /// Координати 0-based у межах запису; Sequence містить лише нормалізовані нуклеотиди.
    /// </summary>
    public class DnaChunkMessage
    {
        public Guid ChunkId { get; set; } = Guid.NewGuid();
        public Guid RunId { get; set; }
        public string RecordId { get; set; } = string.Empty;
        public int RecordIndex { get; set; }
        public string Sequence { get; set; } = string.Empty;
        public long WindowStart { get; set; }
        public long CoreStart { get; set; }
        public long CoreEnd { get; set; }
        public bool AtRecordStart { get; set; }
        public bool AtRecordEnd { get; set; }
        public ThreatCategory Category { get; set; }
    }
}
