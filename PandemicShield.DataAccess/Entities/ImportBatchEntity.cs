using PandemicShield.Contracts;
using System;

namespace PandemicShield.DataAccess.Entities
{
    public enum ImportStatus
    {
        Queued,
        Running,
        Completed,
        Failed
    }

    /// <summary>
    /// Масовий імпорт відкритих даних NCBI: список номерів (OQ291490.1, GCA_009887825.1) або архів NCBI Datasets.
    /// Віруси стають одним запуском з багатьма геномами, бактерії — окремим запуском на кожну збірку (ізолят).
    /// </summary>
    public class ImportBatchEntity
    {
        public const string AccessionsKind = "accessions";
        public const string ArchiveKind = "archive";
        public const string ReadsKind = "fastq";

        public Guid Id { get; private set; }

        /// <summary>Звідки дані: номери NCBI, архів NCBI Datasets або сирі прочитання FASTQ (обробляє воркер Reads).</summary>
        public string Kind { get; private set; } = AccessionsKind;

        /// <summary>Для FASTQ: illumina або nanopore.</summary>
        public string? Platform { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime? CompletedAt { get; private set; }
        public ThreatCategory Category { get; private set; }
        public string? NextcladeDataset { get; private set; }

        /// <summary>Вид для AMRFinderPlus; null — визначити за метаданими NCBI для кожної збірки.</summary>
        public string? AmrOrganism { get; private set; }

        /// <summary>Номери через пробіл (для імпорту з архіву — порожньо).</summary>
        public string Accessions { get; private set; }
        public string? ArchiveName { get; private set; }

        public ImportStatus Status { get; private set; }
        public int Total { get; private set; }
        public int Done { get; private set; }
        public int FailedCount { get; private set; }
        public string? Message { get; private set; }

        /// <summary>Що не вдалося імпортувати й чому, по рядку на номер.</summary>
        public string? Errors { get; private set; }

        public ImportBatchEntity(ThreatCategory category, string? nextcladeDataset, string? amrOrganism, string accessions, string? archiveName, int total)
        {
            Id = Guid.NewGuid();
            Kind = archiveName == null ? AccessionsKind : ArchiveKind;
            CreatedAt = DateTime.UtcNow;
            Category = category;
            NextcladeDataset = nextcladeDataset;
            AmrOrganism = amrOrganism;
            Accessions = accessions;
            ArchiveName = archiveName;
            Status = ImportStatus.Queued;
            Total = total;
        }

        /// <summary>Сирі прочитання: файли (R1, R2 або частини Nanopore) — у ArchiveName через кому.</summary>
        public static ImportBatchEntity ForReads(ThreatCategory category, string? nextcladeDataset, string? amrOrganism,
            string platform, IReadOnlyList<string> files)
        {
            var batch = new ImportBatchEntity(category, nextcladeDataset, amrOrganism, string.Empty, string.Join(", ", files), 1);
            batch.Kind = ReadsKind;
            batch.Platform = platform;
            return batch;
        }

        public void MarkRunning(int total)
        {
            Status = ImportStatus.Running;
            Total = total;
        }

        public void MarkProgress(int done, int failed, string? message)
        {
            Done = done;
            FailedCount = failed;
            Message = message;
        }

        public void AddError(string error) => Errors = string.IsNullOrEmpty(Errors) ? error : $"{Errors}\n{error}";

        public void MarkCompleted(string message)
        {
            Status = ImportStatus.Completed;
            CompletedAt = DateTime.UtcNow;
            Message = message;
        }

        public void MarkFailed(string message)
        {
            Status = ImportStatus.Failed;
            CompletedAt = DateTime.UtcNow;
            Message = message;
        }
    }
}
