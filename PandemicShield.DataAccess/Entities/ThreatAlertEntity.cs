using PandemicShield.Contracts;
using System;
using System.Collections.Generic;
using System.Text;

namespace PandemicShield.DataAccess.Entities
{
    public class ThreatAlertEntity
    {
        public Guid Id { get; private set; }
        public string ThreatName { get; private set; }
        public string ProteinSequence { get; private set; }
        public int GlobalPosition { get; private set; }
        public ThreatCategory Category { get; private set; }
        public DateTime DetectedAt { get; private set; }

        public ThreatAlertEntity(
            string threatName,
            string proteinSequence,
            int globalPosition,
            ThreatCategory category)
        {
            Id = Guid.NewGuid();
            ThreatName = threatName;
            ProteinSequence = proteinSequence;
            GlobalPosition = globalPosition;
            Category = category;
            DetectedAt = DateTime.UtcNow;
        }
    }
}
