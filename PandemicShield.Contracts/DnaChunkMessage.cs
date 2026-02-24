using System;
using System.Collections.Generic;
using System.Text;

namespace PandemicShield.Contracts
{
    public class DnaChunkMessage
    {
        public Guid ChunkId { get; set; } = Guid.NewGuid();
        public string Sequence { get; set; } = string.Empty;
        public int StartPosition { get; set; }
        public ThreatCategory Category { get; set; }
        public bool IsLastChunk { get; set; } = false;

    }
}
