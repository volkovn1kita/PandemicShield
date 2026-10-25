using PandemicShield.DataAccess.Data;
using PandemicShield.DataAccess.Entities;
using PandemicShield.Nextclade.Results;
using PandemicShield.Nextclade.Rules;

namespace PandemicShield.Nextclade
{
    public sealed record StoreSummary(int Sequences, int Failed, int Mutations, int Alerts);

    /// <summary>
    /// Зберігає результати Nextclade (послідовності, мутації, сповіщення) порціями,
    /// щоб великий запуск не тримав у пам'яті всі об'єкти EF одночасно.
    /// </summary>
    public static class NextcladeResultStore
    {
        private const int BatchSize = 100;

        public static async Task<StoreSummary> SaveAsync(
            Guid runId,
            string dataset,
            string? datasetTag,
            IEnumerable<SequenceAnalysis> sequences,
            IReadOnlyList<VariantRule> rules,
            Func<PandemicDbContext> createContext,
            CancellationToken cancellationToken = default)
        {
            int total = 0, failed = 0, mutationCount = 0, alertCount = 0;

            foreach (var batch in sequences.Chunk(BatchSize))
            {
                using var db = createContext();

                foreach (SequenceAnalysis sequence in batch)
                {
                    total++;
                    var entity = new SequenceResultEntity(runId, sequence.SeqName, dataset, datasetTag);

                    if (sequence.HasErrors)
                    {
                        failed++;
                        entity.SetErrors(string.Join("\n", sequence.Errors));
                        db.SequenceResults.Add(entity);
                        continue;
                    }

                    entity.SetSummary(
                        sequence.Clade, sequence.CladeDisplay, sequence.Lineage,
                        sequence.QcStatus, sequence.QcScore, sequence.Coverage, sequence.IsReverseComplement,
                        sequence.TotalSubstitutions, sequence.TotalDeletions, sequence.TotalInsertions,
                        sequence.TotalMissing, sequence.TotalFrameShifts,
                        sequence.Warnings.Count > 0 ? string.Join("\n", sequence.Warnings) : null);
                    db.SequenceResults.Add(entity);

                    foreach (Mutation m in sequence.Mutations)
                    {
                        db.Mutations.Add(new MutationEntity(entity.Id, runId, m.Kind.ToString(), m.Gene,
                            m.Position, m.EndPosition, m.Ref, m.Alt, m.Label));
                        mutationCount++;
                    }

                    foreach (VariantMatch match in VariantMatcher.Match(sequence, rules, dataset))
                    {
                        db.Threats.Add(ToAlert(runId, entity.Id, match));
                        alertCount++;
                    }
                }

                await db.SaveChangesAsync(cancellationToken);
            }

            return new StoreSummary(total, failed, mutationCount, alertCount);
        }

        private static ThreatAlertEntity ToAlert(Guid runId, Guid sequenceResultId, VariantMatch match)
        {
            Mutation? m = match.Mutation;
            long ntStart = 0, ntEnd = 0;
            int aaPosition = 0;

            if (m is not null)
            {
                switch (m.Kind)
                {
                    case MutationKind.AminoAcidSubstitution:
                    case MutationKind.AminoAcidDeletion:
                        aaPosition = m.Position;
                        if (m.NucleotidePosition is int codonStart)
                        {
                            ntStart = codonStart;
                            ntEnd = codonStart + 2;
                        }
                        break;
                    default:
                        ntStart = m.Position;
                        ntEnd = m.EndPosition ?? m.Position;
                        break;
                }
            }

            string variant = m?.Label ?? match.Rule.Pattern.ToString();
            if (match.Mutation is null && match.Sequence.Lineage is not null)
            {
                variant += $" ({match.Sequence.CladeDisplay ?? match.Sequence.Clade}, {match.Sequence.Lineage})";
            }

            return ThreatAlertEntity.FromNextclade(runId, sequenceResultId, match.Rule.Name, match.Sequence.SeqName,
                variant, ntStart, ntEnd, aaPosition);
        }
    }
}
