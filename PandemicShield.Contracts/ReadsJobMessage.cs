using System;

namespace PandemicShield.Contracts
{
    /// <summary>
    /// Завдання для воркера Reads: перетворити сирі прочитання (FASTQ) пакета імпорту на FASTA —
    /// консенсус вірусу або збірку бактерії. Файли лежать у {DATA_DIR}/imports/{ImportId}/reads.
    /// </summary>
    public class ReadsJobMessage
    {
        public Guid ImportId { get; set; }
    }
}
