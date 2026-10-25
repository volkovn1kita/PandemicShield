using System;

namespace PandemicShield.Contracts
{
    /// <summary>
    /// Завдання на масовий імпорт з NCBI. Параметри (номери, тип зразка, набір даних) зберігаються в пакеті імпорту в БД;
    /// ArchivePath задано, якщо користувач завантажив архів NCBI Datasets замість списку номерів.
    /// </summary>
    public class ImportJobMessage
    {
        public Guid ImportId { get; set; }
        public string? ArchivePath { get; set; }

        /// <summary>
        /// FASTA, вже готова воркером Reads з сирих прочитань (консенсус вірусу або збірка бактерії):
        /// API лише ставить її на аналіз як звичайний запуск.
        /// </summary>
        public string? FastaPath { get; set; }

        /// <summary>Для консенсусу вірусу — набір Nextclade, визначений за картуванням (замість «auto» пакета).</summary>
        public string? Dataset { get; set; }

        public string? RunName { get; set; }
        public string? ReadsSummary { get; set; }
    }
}
