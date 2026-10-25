using System;

namespace PandemicShield.Contracts
{
    /// <summary>
    /// Завдання для Nextclade-воркера: проаналізувати весь FASTA-файл запуску одним набором даних (dataset).
    /// Файл лежить у спільному томі, доступному API і воркеру.
    /// </summary>
    public class NextcladeJobMessage
    {
        public Guid RunId { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public string Dataset { get; set; } = string.Empty;
    }
}
