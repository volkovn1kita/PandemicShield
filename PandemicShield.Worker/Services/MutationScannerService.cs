using PandemicShield.Worker.Data;
using PandemicShield.Contracts;
using System;
using System.Collections.Generic;
using System.Text;

namespace PandemicShield.Worker.Services
{
    public class MutationScannerService
    {
        public static List<ThreatReport> ScanProtein (ProteinData protein, List<DiseaseMarker> diseases)
        {
            List<ThreatReport> threatReports = new List<ThreatReport>();

            foreach (DiseaseMarker disease in diseases)
            {
                int index = protein.Sequence.IndexOf(disease.Sequence);
                if (index != -1)
                {
                    ThreatReport report = new ThreatReport
                    (
                        threatName: disease.Name,
                        proteinSequence: protein.Sequence,
                        globalPosition: protein.GlobalPosition + index * 3,
                        category: disease.Category
                    );

                    threatReports.Add(report);
                }
            }
            return threatReports;
        }
    }
}
