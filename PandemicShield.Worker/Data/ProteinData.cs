using System;
using System.Collections.Generic;
using System.Text;

namespace PandemicShield.Worker.Data
{
    public class ProteinData
    {
        public string Sequence { get; private set; }
        public int GlobalPosition { get; private set; }
        public ProteinData(string sequence, int globalPosition)
        {
            Sequence = sequence;
            GlobalPosition = globalPosition;
        }
    }
}
