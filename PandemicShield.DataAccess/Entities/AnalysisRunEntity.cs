using PandemicShield.Contracts;
using System;

namespace PandemicShield.DataAccess.Entities
{
    public enum AnalysisRunStatus
    {
        Streaming,
        Queued,
        Failed
    }

    /// <summary>Стан аналізу зовнішнім інструментом (Nextclade, AMRFinderPlus). У БД зберігається рядком.</summary>
    public enum ToolStatus
    {
        NotRequested,
        Queued,
        Running,
        Completed,
        Failed
    }

    /// <summary>
    /// Один запуск аналізу одного файлу. Прив'язує знахідки до зразка, з якого вони прийшли.
    /// </summary>
    public class AnalysisRunEntity
    {
        public Guid Id { get; private set; }
        public string FileName { get; private set; }
        public ThreatCategory Category { get; private set; }
        public AnalysisRunStatus Status { get; private set; }
        public DateTime StartedAt { get; private set; }
        public DateTime? StreamedAt { get; private set; }
        public int RecordCount { get; private set; }
        public long BaseCount { get; private set; }
        public long InvalidSymbolCount { get; private set; }
        public int ChunkCount { get; private set; }
        public string? Message { get; private set; }

        public ToolStatus NextcladeStatus { get; private set; }
        public string? NextcladeDataset { get; private set; }
        public string? NextcladeMessage { get; private set; }
        public DateTime? NextcladeCompletedAt { get; private set; }
        public int SequencesAnalyzed { get; private set; }

        public ToolStatus AmrStatus { get; private set; }
        public string? AmrOrganism { get; private set; }
        public string? AmrDatabaseVersion { get; private set; }
        public string? AmrMessage { get; private set; }
        public DateTime? AmrCompletedAt { get; private set; }

        public AnalysisRunEntity(string fileName, ThreatCategory category)
        {
            Id = Guid.NewGuid();
            FileName = fileName;
            Category = category;
            Status = AnalysisRunStatus.Streaming;
            StartedAt = DateTime.UtcNow;
        }

        public void MarkStreamed(int recordCount, long baseCount, long invalidSymbolCount, int chunkCount, string message)
        {
            Status = AnalysisRunStatus.Queued;
            StreamedAt = DateTime.UtcNow;
            RecordCount = recordCount;
            BaseCount = baseCount;
            InvalidSymbolCount = invalidSymbolCount;
            ChunkCount = chunkCount;
            Message = message;
        }

        public void RequestNextclade(string dataset)
        {
            NextcladeStatus = ToolStatus.Queued;
            NextcladeDataset = dataset;
        }

        public void MarkNextcladeRunning() => NextcladeStatus = ToolStatus.Running;

        public void MarkNextcladeCompleted(int sequencesAnalyzed, string message)
        {
            NextcladeStatus = ToolStatus.Completed;
            NextcladeCompletedAt = DateTime.UtcNow;
            SequencesAnalyzed = sequencesAnalyzed;
            NextcladeMessage = message;
        }

        public void MarkNextcladeFailed(string message)
        {
            NextcladeStatus = ToolStatus.Failed;
            NextcladeCompletedAt = DateTime.UtcNow;
            NextcladeMessage = message;
        }

        public void RequestAmr(string? organism)
        {
            AmrStatus = ToolStatus.Queued;
            AmrOrganism = organism;
        }

        public void MarkAmrRunning() => AmrStatus = ToolStatus.Running;

        public void MarkAmrCompleted(string? databaseVersion, string message)
        {
            AmrStatus = ToolStatus.Completed;
            AmrCompletedAt = DateTime.UtcNow;
            AmrDatabaseVersion = databaseVersion;
            AmrMessage = message;
        }

        public void MarkAmrFailed(string message)
        {
            AmrStatus = ToolStatus.Failed;
            AmrCompletedAt = DateTime.UtcNow;
            AmrMessage = message;
        }

        public void MarkFailed(string message)
        {
            Status = AnalysisRunStatus.Failed;
            Message = message;
        }
    }
}
