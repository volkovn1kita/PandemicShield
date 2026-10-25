using System;

namespace PandemicShield.DataAccess.Entities
{
    /// <summary>
    /// Мутація послідовності відносно референсу набору даних (позиції 1-based).
    /// Kind: NucleotideSubstitution, NucleotideDeletion, NucleotideInsertion, AminoAcidSubstitution,
    /// AminoAcidDeletion, FrameShift. Label — позначення як у Nextclade: A23403G, S:D614G, S:F157-.
    /// </summary>
    public class MutationEntity
    {
        public Guid Id { get; private set; }
        public Guid SequenceResultId { get; private set; }
        public Guid RunId { get; private set; }
        public string Kind { get; private set; }
        public string? Gene { get; private set; }
        public int Position { get; private set; }
        public int? EndPosition { get; private set; }
        public string? Ref { get; private set; }
        public string? Alt { get; private set; }
        public string Label { get; private set; }

        public MutationEntity(
            Guid sequenceResultId,
            Guid runId,
            string kind,
            string? gene,
            int position,
            int? endPosition,
            string? @ref,
            string? alt,
            string label)
        {
            Id = Guid.NewGuid();
            SequenceResultId = sequenceResultId;
            RunId = runId;
            Kind = kind;
            Gene = gene;
            Position = position;
            EndPosition = endPosition;
            Ref = @ref;
            Alt = alt;
            Label = label;
        }
    }
}
