using System.Text.RegularExpressions;

namespace PandemicShield.Bio.Variants
{
    public enum VariantKind
    {
        NucleotideSubstitution,
        NucleotideDeletion,
        AminoAcidSubstitution,
        AminoAcidDeletion,
        Clade,
        Lineage,

        // Гени й класи резистентності з AMRFinderPlus (бактерії).
        AmrGene,
        AmrClass,
        AmrSubclass,

        // Тип штаму за MLST (sequence type): ST147, ST11.
        SequenceType
    }

    /// <summary>
    /// Шаблон варіанта в позначеннях, як у Nextclade/Nextstrain. Позиції 1-based відносно референсу набору даних.
    /// <list type="bullet">
    /// <item><c>A23403G</c> — заміна нуклеотиду (реф. основу можна не вказувати: <c>23403G</c>)</item>
    /// <item><c>A23403*</c> або <c>23403</c> — будь-яка заміна в цій позиції</item>
    /// <item><c>22029-</c> — делеція, що зачіпає позицію 22029</item>
    /// <item><c>S:D614G</c> — заміна амінокислоти в білку (CDS) S; <c>S:E484</c> — будь-яка заміна в позиції 484</item>
    /// <item><c>S:F157-</c> — делеція амінокислоти</item>
    /// <item><c>clade:21J</c>, <c>clade:Delta</c> — клада Nextstrain або назва ВООЗ</item>
    /// <item><c>lineage:BA.2.86*</c> — лінія Pango; <c>*</c> означає «разом із нащадками»</item>
    /// <item><c>gene:blaKPC*</c> — ген резистентності AMRFinderPlus (символ; <c>*</c> — будь-який алель, наприклад blaKPC-2)</item>
    /// <item><c>class:AMINOGLYCOSIDE</c>, <c>subclass:CARBAPENEM</c> — клас або підклас антибіотиків, до яких передбачено стійкість</item>
    /// <item><c>st:klebsiella/147</c> — тип штаму за MLST у схемі PubMLST; <c>st:147</c> — у будь-якій схемі
    /// (номери ST повторюються між схемами: ST131 у E. coli і в Klebsiella — різні штами)</item>
    /// </list>
    /// </summary>
    public sealed partial record VariantPattern(
        VariantKind Kind,
        string? Gene,
        int Position,
        char? Ref,
        char? Alt,
        string? Name)
    {
        // Будь-яка заміна (амінокислоти чи нуклеотиду) у цій позиції.
        public bool AnyAlt => Alt is null;

        public bool IncludesDescendants => Kind is VariantKind.Lineage or VariantKind.AmrGene && Name!.EndsWith('*');

        public bool IsAmr => Kind is VariantKind.AmrGene or VariantKind.AmrClass or VariantKind.AmrSubclass;

        public bool IsMlst => Kind is VariantKind.SequenceType;

        /// <summary>Чи цей тип штаму підпадає під правило st: (схема перевіряється, лише якщо її вказано).</summary>
        public bool MatchesSequenceType(string scheme, string sequenceType)
        {
            if (Kind != VariantKind.SequenceType) return false;
            int slash = Name!.IndexOf('/');
            return slash < 0
                ? Name == sequenceType
                : Name[(slash + 1)..] == sequenceType && Name[..slash].Equals(scheme, StringComparison.OrdinalIgnoreCase);
        }

        public override string ToString() => Kind switch
        {
            VariantKind.NucleotideSubstitution => $"{Ref}{Position}{(AnyAlt ? "*" : Alt.ToString())}",
            VariantKind.NucleotideDeletion => $"{Ref}{Position}-",
            VariantKind.AminoAcidSubstitution => $"{Gene}:{Ref}{Position}{Alt}",
            VariantKind.AminoAcidDeletion => $"{Gene}:{Ref}{Position}-",
            VariantKind.Clade => $"clade:{Name}",
            VariantKind.Lineage => $"lineage:{Name}",
            VariantKind.AmrGene => $"gene:{Name}",
            VariantKind.AmrClass => $"class:{Name}",
            VariantKind.AmrSubclass => $"subclass:{Name}",
            VariantKind.SequenceType => $"st:{Name}",
            _ => throw new InvalidOperationException()
        };

        public static VariantPattern Parse(string text) =>
            TryParse(text, out var pattern, out string error) ? pattern : throw new FormatException(error);

        public static bool TryParse(string? text, out VariantPattern pattern, out string error)
        {
            pattern = null!;
            error = string.Empty;
            string value = (text ?? string.Empty).Trim();

            if (value.Length == 0)
            {
                error = "Порожнє позначення варіанта.";
                return false;
            }

            int colon = value.IndexOf(':');
            if (colon > 0)
            {
                string prefix = value[..colon].Trim();
                string rest = value[(colon + 1)..].Trim();

                if (prefix.Equals("clade", StringComparison.OrdinalIgnoreCase) && rest.Length > 0)
                {
                    pattern = new VariantPattern(VariantKind.Clade, null, 0, null, null, rest);
                    return true;
                }

                if (prefix.Equals("lineage", StringComparison.OrdinalIgnoreCase) && LineageRegex().IsMatch(rest))
                {
                    pattern = new VariantPattern(VariantKind.Lineage, null, 0, null, null, rest.ToUpperInvariant());
                    return true;
                }

                if (prefix.Equals("st", StringComparison.OrdinalIgnoreCase))
                {
                    // "st:147" і "st:ST147" — одне й те саме; mlst повертає ST без префікса. Схема — як у mlst (klebsiella).
                    var st = SequenceTypeRegex().Match(rest);
                    if (!st.Success)
                    {
                        error = $"Тип штаму — це число, можна зі схемою: st:147 або st:klebsiella/147, а не «{rest}».";
                        return false;
                    }
                    string number = int.Parse(st.Groups["st"].Value).ToString();
                    string name = st.Groups["scheme"].Success ? $"{st.Groups["scheme"].Value.ToLowerInvariant()}/{number}" : number;
                    pattern = new VariantPattern(VariantKind.SequenceType, null, 0, null, null, name);
                    return true;
                }

                if (prefix.Equals("gene", StringComparison.OrdinalIgnoreCase) && AmrGeneRegex().IsMatch(rest))
                {
                    // Символи генів чутливі до регістру (blaKPC, mecA), тож залишаємо як є.
                    pattern = new VariantPattern(VariantKind.AmrGene, null, 0, null, null, rest);
                    return true;
                }

                if ((prefix.Equals("class", StringComparison.OrdinalIgnoreCase) || prefix.Equals("subclass", StringComparison.OrdinalIgnoreCase))
                    && AmrClassRegex().IsMatch(rest))
                {
                    var kind = prefix.Equals("class", StringComparison.OrdinalIgnoreCase) ? VariantKind.AmrClass : VariantKind.AmrSubclass;
                    // AMRFinderPlus пише класи великими літерами з підкресленнями: BETA-LACTAM, QUATERNARY AMMONIUM.
                    pattern = new VariantPattern(kind, null, 0, null, null, rest.ToUpperInvariant().Replace('_', ' '));
                    return true;
                }

                var aa = AminoAcidRegex().Match(rest.ToUpperInvariant());
                if (GeneRegex().IsMatch(prefix) && aa.Success)
                {
                    char? reference = aa.Groups["ref"].Success ? aa.Groups["ref"].Value[0] : null;
                    int position = int.Parse(aa.Groups["pos"].Value);
                    string alt = aa.Groups["alt"].Value;

                    pattern = alt == "-"
                        ? new VariantPattern(VariantKind.AminoAcidDeletion, prefix, position, reference, '-', null)
                        : new VariantPattern(VariantKind.AminoAcidSubstitution, prefix, position, reference,
                            alt.Length == 0 ? null : alt[0], null);
                    return Validate(pattern, out error);
                }

                error = $"Не вдалося розібрати «{value}». Приклади: S:D614G, S:F157-, clade:Delta, lineage:BA.2.86*.";
                return false;
            }

            var nuc = NucleotideRegex().Match(value.ToUpperInvariant());
            if (nuc.Success)
            {
                char? reference = nuc.Groups["ref"].Success ? nuc.Groups["ref"].Value[0] : null;
                int position = int.Parse(nuc.Groups["pos"].Value);
                string alt = nuc.Groups["alt"].Value;

                pattern = alt switch
                {
                    "-" => new VariantPattern(VariantKind.NucleotideDeletion, null, position, reference, '-', null),
                    "" or "*" => new VariantPattern(VariantKind.NucleotideSubstitution, null, position, reference, null, null),
                    _ => new VariantPattern(VariantKind.NucleotideSubstitution, null, position, reference, alt[0], null)
                };
                return Validate(pattern, out error);
            }

            error = $"Не вдалося розібрати «{value}». Приклади: A23403G, 22029-, S:D614G, clade:21J.";
            return false;
        }

        private static bool Validate(VariantPattern pattern, out string error)
        {
            error = string.Empty;
            if (pattern.Position <= 0)
            {
                error = "Позиція рахується з 1.";
                return false;
            }
            if (pattern.Ref is not null && pattern.Ref == pattern.Alt)
            {
                error = "Референсна й нова основа (амінокислота) однакові: це не мутація.";
                return false;
            }
            return true;
        }

        [GeneratedRegex(@"^(?<ref>[ACGT])?(?<pos>\d+)(?<alt>[ACGT]|\*|-)?$")]
        private static partial Regex NucleotideRegex();

        // '*' в амінокислотному позначенні — це стоп-кодон (наприклад, ORF8:Q27*), а не «будь-яка».
        [GeneratedRegex(@"^(?<ref>[A-Z*])?(?<pos>\d+)(?<alt>[A-Z*]|-)?$")]
        private static partial Regex AminoAcidRegex();

        [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9_.\-]*$")]
        private static partial Regex GeneRegex();

        [GeneratedRegex(@"^[A-Za-z]{1,3}(\.\d+)*\*?$")]
        private static partial Regex LineageRegex();

        [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9_.()'\-]*\*?$")]
        private static partial Regex AmrGeneRegex();

        [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9 _\-]*$")]
        private static partial Regex AmrClassRegex();

        [GeneratedRegex(@"^(?:(?<scheme>[A-Za-z][A-Za-z0-9_]*)/)?(?:ST)?(?<st>\d{1,6})$", RegexOptions.IgnoreCase)]
        private static partial Regex SequenceTypeRegex();
    }
}
