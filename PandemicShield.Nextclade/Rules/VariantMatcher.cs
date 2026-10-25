using PandemicShield.Bio.Variants;
using PandemicShield.Nextclade.Results;

namespace PandemicShield.Nextclade.Rules
{
    /// <summary>Правило сповіщення: «повідом, якщо в зразку є такий варіант».</summary>
    /// <param name="Dataset">Набір даних Nextclade, для якого діє правило; null — для будь-якого.</param>
    public sealed record VariantRule(Guid Id, string Name, VariantPattern Pattern, string? Dataset);

    /// <param name="Mutation">Мутація, що спрацювала; null для правил на кладу чи лінію.</param>
    public sealed record VariantMatch(VariantRule Rule, SequenceAnalysis Sequence, Mutation? Mutation);

    public static class VariantMatcher
    {
        public static IEnumerable<VariantMatch> Match(SequenceAnalysis sequence, IEnumerable<VariantRule> rules, string dataset)
        {
            if (sequence.HasErrors) yield break;

            foreach (VariantRule rule in rules)
            {
                if (rule.Dataset is not null && !string.Equals(rule.Dataset, dataset, StringComparison.OrdinalIgnoreCase))
                    continue;

                VariantPattern pattern = rule.Pattern;
                switch (pattern.Kind)
                {
                    case VariantKind.Clade:
                        if (MatchesClade(sequence, pattern.Name!)) yield return new VariantMatch(rule, sequence, null);
                        break;

                    case VariantKind.Lineage:
                        if (MatchesLineage(sequence, pattern)) yield return new VariantMatch(rule, sequence, null);
                        break;

                    default:
                        Mutation? mutation = sequence.Mutations.FirstOrDefault(m => Matches(m, pattern));
                        if (mutation is not null) yield return new VariantMatch(rule, sequence, mutation);
                        break;
                }
            }
        }

        public static bool Matches(Mutation mutation, VariantPattern pattern)
        {
            return pattern.Kind switch
            {
                VariantKind.NucleotideSubstitution =>
                    mutation.Kind == MutationKind.NucleotideSubstitution
                    && mutation.Position == pattern.Position
                    && SameSymbol(mutation.Ref, pattern.Ref)
                    && SameSymbol(mutation.Alt, pattern.Alt),

                // Делеція в Nextclade — це діапазон; шаблон спрацьовує, якщо позиція в нього потрапляє.
                VariantKind.NucleotideDeletion =>
                    mutation.Kind == MutationKind.NucleotideDeletion
                    && mutation.Position <= pattern.Position
                    && pattern.Position <= mutation.EndPosition,

                VariantKind.AminoAcidSubstitution =>
                    mutation.Kind == MutationKind.AminoAcidSubstitution
                    && string.Equals(mutation.Gene, pattern.Gene, StringComparison.OrdinalIgnoreCase)
                    && mutation.Position == pattern.Position
                    && SameSymbol(mutation.Ref, pattern.Ref)
                    && SameSymbol(mutation.Alt, pattern.Alt),

                VariantKind.AminoAcidDeletion =>
                    mutation.Kind == MutationKind.AminoAcidDeletion
                    && string.Equals(mutation.Gene, pattern.Gene, StringComparison.OrdinalIgnoreCase)
                    && mutation.Position == pattern.Position
                    && SameSymbol(mutation.Ref, pattern.Ref),

                _ => false
            };
        }

        /// <summary>
        /// Клада збігається за кодом Nextstrain («21J»), за повною назвою («21J (Delta)») або за назвою
        /// в дужках чи атрибутом clade_who («Delta» спрацює для 21A, 21I і 21J).
        /// </summary>
        public static bool MatchesClade(SequenceAnalysis sequence, string name)
        {
            var candidates = new List<string?>
            {
                sequence.Clade,
                sequence.CladeDisplay,
                sequence.CustomNodeAttributes.GetValueOrDefault("clade_nextstrain"),
                sequence.CustomNodeAttributes.GetValueOrDefault("clade_who"),
                sequence.CustomNodeAttributes.GetValueOrDefault("subclade")
            };

            string? display = sequence.CladeDisplay;
            int open = display?.IndexOf('(') ?? -1;
            if (open >= 0 && display!.EndsWith(')'))
            {
                candidates.Add(display[(open + 1)..^1]);
            }

            return candidates.Any(c => string.Equals(c?.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Лінія Pango. З «*» на кінці збігаються й нащадки: «BA.2.86*» спрацює для JN.1, бо його повна назва —
        /// BA.2.86.1.1 (поле partiallyAliased у Nextclade).
        /// </summary>
        public static bool MatchesLineage(SequenceAnalysis sequence, VariantPattern pattern)
        {
            string name = pattern.Name!.TrimEnd('*');
            foreach (string? lineage in new[] { sequence.Lineage, sequence.PartiallyAliasedLineage })
            {
                if (lineage is null) continue;
                if (string.Equals(lineage, name, StringComparison.OrdinalIgnoreCase)) return true;
                if (pattern.IncludesDescendants && lineage.StartsWith(name + ".", StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static bool SameSymbol(string? actual, char? expected) =>
            expected is null || (actual is { Length: 1 } && char.ToUpperInvariant(actual[0]) == char.ToUpperInvariant(expected.Value));
    }
}
