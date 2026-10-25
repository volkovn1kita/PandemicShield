using PandemicShield.Bio;
using PandemicShield.Contracts;
using PandemicShield.Worker.Data;

namespace PandemicShield.Worker.Services
{
    public class MutationScannerService
    {
        /// <summary>
        /// Шукає ORF у вікні (6 рамок) і маркери в їхніх білках. Кожна знахідка звітується рівно один раз:
        /// лише вікном, у ядрі якого лежить старт-кодон її ORF.
        /// </summary>
        public static List<ThreatReport> ScanChunk(DnaChunkMessage chunk, List<DiseaseMarker> diseases, OrfFinderOptions options)
        {
            var window = new SequenceWindow(
                chunk.RecordId,
                chunk.RecordIndex,
                chunk.Sequence,
                chunk.WindowStart,
                chunk.CoreStart,
                chunk.CoreEnd,
                chunk.AtRecordStart,
                chunk.AtRecordEnd);

            List<ThreatReport> threatReports = new List<ThreatReport>();

            foreach (Orf orf in OrfFinder.Find(window, options))
            {
                foreach (DiseaseMarker disease in diseases)
                {
                    foreach (MotifMatch match in ProteinMotifScanner.Scan(orf, disease.Sequence))
                    {
                        threatReports.Add(new ThreatReport
                        (
                            runId: chunk.RunId,
                            threatName: disease.Name,
                            recordId: chunk.RecordId,
                            strand: orf.Strand == Strand.Forward ? '+' : '-',
                            ntStart: match.NucleotideStart + 1,
                            ntEnd: match.NucleotideEnd,
                            aaPosition: match.AminoAcidIndex + 1,
                            proteinSequence: orf.Protein,
                            category: disease.Category
                        ));
                    }
                }
            }

            return threatReports;
        }
    }
}
