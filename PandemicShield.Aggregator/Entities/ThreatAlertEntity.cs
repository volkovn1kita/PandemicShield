using System;
using System.Collections.Generic;
using System.Text;

namespace PandemicShield.Aggregator.Entities
{
    public class ThreatAlertEntity
    {
        public Guid Id { get; private set; }
        public string ThreatName { get; private set; }
        public string ProteinSequence { get; private set; }
        public int GlobalPosition { get; private set; }
        public DateTime DetectedAt { get; private set; }

        public ThreatAlertEntity(
            string threatName,
            string proteinSequence,
            int globalPosition)
        {
            Id = Guid.NewGuid();
            ThreatName = threatName;
            ProteinSequence = proteinSequence;
            GlobalPosition = globalPosition;
            DetectedAt = DateTime.UtcNow;
        }
    }
}
