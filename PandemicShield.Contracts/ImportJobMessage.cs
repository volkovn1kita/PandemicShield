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
    }
}
