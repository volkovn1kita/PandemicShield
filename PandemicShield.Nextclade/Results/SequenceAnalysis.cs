namespace PandemicShield.Nextclade.Results
{
    public enum MutationKind
    {
        NucleotideSubstitution,
        NucleotideDeletion,
        NucleotideInsertion,
        AminoAcidSubstitution,
        AminoAcidDeletion,
        FrameShift
    }

    /// <summary>
    /// Одна мутація відносно референсу набору даних. Позиції 1-based (Nextclade віддає 0-based).
    /// </summary>
    /// <param name="Gene">Назва CDS для амінокислотних мутацій і зсувів рамки; null для нуклеотидних.</param>
    /// <param name="EndPosition">Остання позиція (включно) для делецій і зсувів рамки.</param>
    /// <param name="NucleotidePosition">Перша основа кодона в координатах референсу (для амінокислотних мутацій).</param>
    public sealed record Mutation(
        MutationKind Kind,
        string? Gene,
        int Position,
        int? EndPosition,
        string? Ref,
        string? Alt,
        int? NucleotidePosition = null)
    {
        public string Label => Kind switch
        {
            MutationKind.NucleotideSubstitution => $"{Ref}{Position}{Alt}",
            MutationKind.NucleotideDeletion => $"del:{Position}-{EndPosition}",
            MutationKind.NucleotideInsertion => $"ins:{Position}:{Alt}",
            MutationKind.AminoAcidSubstitution => $"{Gene}:{Ref}{Position}{Alt}",
            MutationKind.AminoAcidDeletion => $"{Gene}:{Ref}{Position}-",
            MutationKind.FrameShift => $"{Gene}:fs{Position}-{EndPosition}",
            _ => throw new InvalidOperationException()
        };
    }

    /// <summary>
    /// Результат Nextclade для однієї послідовності: лише поля, які використовує PandemicShield.
    /// </summary>
    public sealed record SequenceAnalysis
    {
        public int Index { get; init; }
        public string SeqName { get; init; } = string.Empty;

        /// <summary>Помилки (наприклад, «послідовність закоротка»): тоді решта полів порожні.</summary>
        public IReadOnlyList<string> Errors { get; init; } = [];
        public IReadOnlyList<string> Warnings { get; init; } = [];

        public string? Clade { get; init; }

        /// <summary>Додаткові атрибути вузла дерева: Nextclade_pango, clade_display, clade_who тощо.</summary>
        public IReadOnlyDictionary<string, string> CustomNodeAttributes { get; init; } = new Dictionary<string, string>();

        public string? QcStatus { get; init; }
        public double? QcScore { get; init; }
        public double? Coverage { get; init; }
        public bool IsReverseComplement { get; init; }

        public int TotalSubstitutions { get; init; }
        public int TotalDeletions { get; init; }
        public int TotalInsertions { get; init; }
        public int TotalMissing { get; init; }
        public int TotalFrameShifts { get; init; }

        public IReadOnlyList<Mutation> Mutations { get; init; } = [];

        public bool HasErrors => Errors.Count > 0;

        public string? Lineage => CustomNodeAttributes.GetValueOrDefault("Nextclade_pango");

        /// <summary>Повна (розгорнута) назва лінії: EG.5.1.13 → XBB.1.9.2.5.1.13. Потрібна для пошуку нащадків.</summary>
        public string? PartiallyAliasedLineage => CustomNodeAttributes.GetValueOrDefault("partiallyAliased");

        public string? CladeDisplay => CustomNodeAttributes.GetValueOrDefault("clade_display");
    }
}
