using PandemicShield.Contracts;
using System;
using System.Collections.Generic;
using System.Text;

namespace PandemicShield.Worker.Data
{
    public class DiseaseMarker
    {
        public string Name { get; set; } = string.Empty;
        public string Sequence { get; set; } = string.Empty;
        public ThreatCategory Category { get; set; }
    }
}
