using System;

namespace PandemicShield.Contracts
{
    /// <summary>
    /// Завдання для воркера AMRFinderPlus: знайти гени й мутації резистентності в бактеріальній збірці.
    /// Organism вмикає пошук точкових мутацій для конкретного виду (наприклад, Klebsiella_pneumoniae).
    /// </summary>
    public class AmrJobMessage
    {
        public Guid RunId { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public string? Organism { get; set; }
    }
}
