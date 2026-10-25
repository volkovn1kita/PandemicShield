using System.Buffers;
using System.Text;

namespace PandemicShield.Bio
{
    public enum Strand
    {
        Forward,
        Reverse
    }

    /// <summary>
    /// Відкрита рамка зчитування (ORF): від старт-кодона до стоп-кодона в одній рамці.
    /// <see cref="Start"/> і <see cref="End"/> — координати на прямому ланцюзі запису (0-based, кінець
    /// не включається) незалежно від ланцюга; стоп-кодон входить у діапазон, якщо ORF повний.
    /// <see cref="Protein"/> не містить символу стоп-кодона.
    /// </summary>
    public sealed record Orf(
        string RecordId,
        Strand Strand,
        long Start,
        long End,
        string Protein,
        bool IsComplete)
    {
        public long Length => End - Start;

        /// <summary>Перша основа старт-кодона з урахуванням напрямку ланцюга.</summary>
        public long Anchor => Strand == Strand.Forward ? Start : End - 1;

        /// <summary>
        /// Координати (0-based, кінець не включається) на прямому ланцюзі для амінокислот
        /// [aaIndex, aaIndex + aaLength) цього білка.
        /// </summary>
        public (long Start, long End) AminoAcidsToNucleotides(int aaIndex, int aaLength)
        {
            return Strand == Strand.Forward
                ? (Start + aaIndex * 3L, Start + (aaIndex + aaLength) * 3L)
                : (End - (aaIndex + aaLength) * 3L, End - aaIndex * 3L);
        }
    }

    public sealed record OrfFinderOptions
    {
        /// <summary>Мінімальна довжина ORF у нуклеотидах разом зі стоп-кодоном (як у NCBI ORFfinder; типово 75).</summary>
        public int MinNucleotideLength { get; init; } = 75;

        public GeneticCode GeneticCode { get; init; } = GeneticCode.Standard;

        /// <summary>Дозволити альтернативні старт-кодони таблиці (GTG, TTG тощо), а не лише ATG.</summary>
        public bool AllowAlternativeStarts { get; init; }

        /// <summary>Шукати й на зворотному ланцюзі (6 рамок замість 3).</summary>
        public bool BothStrands { get; init; } = true;

        /// <summary>
        /// Повертати ORF без стоп-кодона (обірвані кінцем запису або вікна) з <see cref="Orf.IsComplete"/> = false.
        /// </summary>
        public bool IncludePartial { get; init; }
    }

    /// <summary>
    /// Пошук ORF у 6 рамках зчитування (3 на прямому ланцюзі + 3 на зворотно-комплементарному).
    /// <para>
    /// Для кожного стоп-кодона повертається найдовший ORF: від першого старт-кодона після попереднього
    /// стоп-кодона в тій самій рамці (так за замовчуванням працює NCBI ORFfinder). ORF в інших рамках,
    /// що перекриваються з ним або вкладені в нього (як ORF9b всередині гена N у SARS-CoV-2), теж знаходяться.
    /// </para>
    /// </summary>
    public static class OrfFinder
    {
        public static List<Orf> Find(string recordId, string sequence, OrfFinderOptions? options = null)
        {
            return Find(SequenceWindow.WholeRecord(recordId, sequence), options);
        }

        /// <summary>
        /// Шукає ORF у вікні й повертає ті, чий старт-кодон лежить у ядрі вікна.
        /// Якщо обробити всі вікна запису, кожен ORF буде знайдено рівно один раз
        /// (за умови, що він коротший за фланг вікна).
        /// </summary>
        public static List<Orf> Find(SequenceWindow window, OrfFinderOptions? options = null)
        {
            options ??= new OrfFinderOptions();
            var result = new List<Orf>();
            string sequence = window.Sequence;

            ScanStrand(sequence, Strand.Forward, trustedLeftEdge: window.AtRecordStart, window, options, result);

            if (options.BothStrands)
            {
                char[] rented = ArrayPool<char>.Shared.Rent(sequence.Length);
                try
                {
                    Span<char> reverse = rented.AsSpan(0, sequence.Length);
                    Nucleotides.ReverseComplement(sequence, reverse);
                    // На зворотному ланцюзі читання починається з правого кінця вікна.
                    ScanStrand(reverse, Strand.Reverse, trustedLeftEdge: window.AtRecordEnd, window, options, result);
                }
                finally
                {
                    ArrayPool<char>.Shared.Return(rented);
                }
            }

            result.Sort((a, b) => a.Start != b.Start ? a.Start.CompareTo(b.Start) : a.Strand.CompareTo(b.Strand));
            return result;
        }

        private static void ScanStrand(
            ReadOnlySpan<char> s,
            Strand strand,
            bool trustedLeftEdge,
            SequenceWindow window,
            OrfFinderOptions options,
            List<Orf> result)
        {
            GeneticCode code = options.GeneticCode;

            for (int frame = 0; frame < 3; frame++)
            {
                // Поки в рамці не трапився стоп-кодон, невідомо, чи не було старт-кодона раніше,
                // за лівим краєм вікна. Тоді ORF починати не можна: його знайде попереднє вікно.
                bool armed = trustedLeftEdge;
                int orfStart = -1;
                int i = frame;

                for (; i + 3 <= s.Length; i += 3)
                {
                    char b1 = s[i], b2 = s[i + 1], b3 = s[i + 2];

                    if (code.IsStop(b1, b2, b3))
                    {
                        if (orfStart >= 0)
                        {
                            Emit(s, orfStart, i + 3, isComplete: true, strand, window, options, result);
                        }
                        orfStart = -1;
                        armed = true;
                    }
                    else if (armed && orfStart < 0 && code.IsStart(b1, b2, b3, options.AllowAlternativeStarts))
                    {
                        orfStart = i;
                    }
                }

                if (orfStart >= 0 && options.IncludePartial)
                {
                    Emit(s, orfStart, i, isComplete: false, strand, window, options, result);
                }
            }
        }

        private static void Emit(
            ReadOnlySpan<char> s,
            int from,
            int to,
            bool isComplete,
            Strand strand,
            SequenceWindow window,
            OrfFinderOptions options,
            List<Orf> result)
        {
            if (to - from < options.MinNucleotideLength) return;

            long start, end;
            if (strand == Strand.Forward)
            {
                start = window.WindowStart + from;
                end = window.WindowStart + to;
            }
            else
            {
                start = window.WindowEnd - to;
                end = window.WindowEnd - from;
            }

            long anchor = strand == Strand.Forward ? start : end - 1;
            if (anchor < window.CoreStart || anchor >= window.CoreEnd) return;

            int proteinEnd = isComplete ? to - 3 : to;
            var protein = new StringBuilder((proteinEnd - from) / 3);
            // Старт-кодон завжди дає метіонін, навіть якщо це альтернативний старт (GTG, TTG).
            protein.Append('M');
            for (int i = from + 3; i + 3 <= proteinEnd; i += 3)
            {
                protein.Append(options.GeneticCode.Translate(s[i], s[i + 1], s[i + 2]));
            }

            result.Add(new Orf(window.RecordId, strand, start, end, protein.ToString(), isComplete));
        }
    }
}
