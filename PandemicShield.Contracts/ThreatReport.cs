using System;
using System.Collections.Generic;
using System.Text;

namespace PandemicShield.Contracts
{
    public class ThreatReport
    {
        public string ThreatName { get; private set; }
        public string ProteinSequence { get; private set; }
        public int GlobalPosition { get; private set; }

        public ThreatReport(string threatName, string proteinSequence, int globalPosition)
        {
            ThreatName = threatName;
            ProteinSequence = proteinSequence;
            GlobalPosition = globalPosition;
        }
    }
}
