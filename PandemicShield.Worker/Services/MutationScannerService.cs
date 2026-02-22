using PandemicShield.Worker.Data;
using PandemicShield.Contracts;
using System;
using System.Collections.Generic;
using System.Text;

namespace PandemicShield.Worker.Services
{
    public class MutationScannerService
    {
        public static List<ThreatReport> ScanProtein (ProteinData protein)
        {
            List<ThreatReport> threatReports = new List<ThreatReport>();

            foreach (string threatSequence in ThreatDatabase.KnownMutations.Keys)
            {
                int index = protein.Sequence.IndexOf(threatSequence);
                if (index != -1)
                {
                    ThreatReport report = new ThreatReport
                    (
                        threatName: ThreatDatabase.KnownMutations[threatSequence],
                        proteinSequence: protein.Sequence,
                        globalPosition: protein.GlobalPosition + index * 3,
                        category: ThreatCategory.Virus
                    );

                    threatReports.Add(report);
                }
            }
            return threatReports;
        }
    }
}
