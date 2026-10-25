using System;

namespace PandemicShield.DataAccess.Entities
{
    /// <summary>
    /// Результат Nextclade для однієї послідовності (запису FASTA) у запуску.
    /// </summary>
    public class SequenceResultEntity
    {
        public Guid Id { get; private set; }
        public Guid RunId { get; private set; }
        public string SeqName { get; private set; }
        public string Dataset { get; private set; }
        public string? DatasetTag { get; private set; }

        public string? Clade { get; private set; }
        public string? CladeDisplay { get; private set; }
        public string? Lineage { get; private set; }

        // good / mediocre / bad; QcScore: 0 — ідеально, 100+ — погано.
        public string? QcStatus { get; private set; }
        public double? QcScore { get; private set; }
        public double? Coverage { get; private set; }
        public bool IsReverseComplement { get; private set; }

        public int TotalSubstitutions { get; private set; }
        public int TotalDeletions { get; private set; }
        public int TotalInsertions { get; private set; }
        public int TotalMissing { get; private set; }
        public int TotalFrameShifts { get; private set; }

        public string? Errors { get; private set; }
        public string? Warnings { get; private set; }
        public DateTime AnalyzedAt { get; private set; }

        public SequenceResultEntity(Guid runId, string seqName, string dataset, string? datasetTag)
        {
            Id = Guid.NewGuid();
            RunId = runId;
            SeqName = seqName;
            Dataset = dataset;
            DatasetTag = datasetTag;
            AnalyzedAt = DateTime.UtcNow;
        }

        public void SetErrors(string errors) => Errors = errors;

        public void SetSummary(
            string? clade, string? cladeDisplay, string? lineage,
            string? qcStatus, double? qcScore, double? coverage, bool isReverseComplement,
            int totalSubstitutions, int totalDeletions, int totalInsertions, int totalMissing, int totalFrameShifts,
            string? warnings)
        {
            Clade = clade;
            CladeDisplay = cladeDisplay;
            Lineage = lineage;
            QcStatus = qcStatus;
            QcScore = qcScore;
            Coverage = coverage;
            IsReverseComplement = isReverseComplement;
            TotalSubstitutions = totalSubstitutions;
            TotalDeletions = totalDeletions;
            TotalInsertions = totalInsertions;
            TotalMissing = totalMissing;
            TotalFrameShifts = totalFrameShifts;
            Warnings = warnings;
        }
    }
}
