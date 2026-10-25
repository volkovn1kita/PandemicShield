using PandemicShield.AmrFinder.Report;
using PandemicShield.Bio.Variants;

namespace PandemicShield.AmrFinder.Rules
{
    public sealed record AmrRule(Guid Id, string Name, VariantPattern Pattern);

    public sealed record AmrMatch(AmrRule Rule, AmrHit Hit);

    /// <summary>
    /// Правила для бактерій: <c>gene:blaKPC*</c> (символ гена, «*» — будь-який алель),
    /// <c>class:AMINOGLYCOSIDE</c>, <c>subclass:CARBAPENEM</c>.
    /// </summary>
    public static class AmrRuleMatcher
    {
        public static IEnumerable<AmrMatch> Match(IReadOnlyList<AmrHit> hits, IEnumerable<AmrRule> rules)
        {
            foreach (AmrRule rule in rules.Where(r => r.Pattern.IsAmr))
            {
                // Один ген у тому самому контигу дає одне сповіщення, навіть якщо звіт містить кілька рядків.
                foreach (AmrHit hit in hits.Where(h => Matches(h, rule.Pattern)).DistinctBy(h => (h.ElementSymbol, h.ContigId)))
                {
                    yield return new AmrMatch(rule, hit);
                }
            }
        }

        public static bool Matches(AmrHit hit, VariantPattern pattern)
        {
            string name = pattern.Name!;
            return pattern.Kind switch
            {
                VariantKind.AmrGene => pattern.IncludesDescendants
                    ? hit.ElementSymbol.StartsWith(name[..^1], StringComparison.Ordinal)
                    : hit.ElementSymbol.Equals(name, StringComparison.Ordinal),
                VariantKind.AmrClass => hit.Class.Replace('_', ' ').Equals(name, StringComparison.OrdinalIgnoreCase),
                VariantKind.AmrSubclass => hit.Subclasses.Contains(name, StringComparer.OrdinalIgnoreCase),
                _ => false
            };
        }
    }
}
