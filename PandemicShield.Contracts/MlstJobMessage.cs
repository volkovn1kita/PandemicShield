using System;

namespace PandemicShield.Contracts
{
    /// <summary>
    /// Завдання для воркера MLST: визначити тип штаму (sequence type) бактеріальної збірки.
    /// Organism (вид AMRFinderPlus, наприклад Klebsiella_pneumoniae) задає схему PubMLST; без нього схема визначається автоматично.
    /// </summary>
    public class MlstJobMessage
    {
        public Guid RunId { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public string? Organism { get; set; }
    }
}
