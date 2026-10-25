namespace PandemicShield.Bio
{
    /// <summary>
    /// Робота з нуклеотидами в кодах IUPAC.
    /// A, C, G, T — звичайні основи; R, Y, S, W, K, M, B, D, H, V, N — неоднозначні
    /// (наприклад, R = A або G, N = будь-яка). U (РНК) зберігається як T.
    /// </summary>
    public static class Nucleotides
    {
        public const char Skip = '\0';
        public const char Invalid = '!';

        // Кожна основа як набір можливих варіантів: A=1, C=2, G=4, T=8.
        private static readonly byte[] MaskByChar = BuildMasks();
        private static readonly char[] ComplementByChar = BuildComplements();
        private static readonly char[] NormalizedByByte = BuildNormalization();

        /// <summary>
        /// Перетворює сирий символ з FASTA на нормалізовану основу (верхній регістр, U→T).
        /// Повертає <see cref="Skip"/> для пробілів, цифр і символів вирівнювання ('-', '.'),
        /// та <see cref="Invalid"/> для всього, що не є нуклеотидом.
        /// </summary>
        public static char Normalize(byte raw) => NormalizedByByte[raw];

        public static bool IsGap(byte raw) => raw == (byte)'-' || raw == (byte)'.';

        /// <summary>Бітова маска можливих основ (A=1, C=2, G=4, T=8); 0 для невідомого символу.</summary>
        public static int Mask(char c) => c < 128 ? MaskByChar[c] : 0;

        public static char Complement(char c) => c < 128 ? ComplementByChar[c] : 'N';

        /// <summary>
        /// Зворотний комплемент: другий ланцюг ДНК, прочитаний у своєму напрямку 5'→3'.
        /// </summary>
        public static void ReverseComplement(ReadOnlySpan<char> source, Span<char> destination)
        {
            if (destination.Length < source.Length)
                throw new ArgumentException("Destination is shorter than source.", nameof(destination));

            int last = source.Length - 1;
            for (int i = 0; i <= last; i++)
            {
                destination[last - i] = Complement(source[i]);
            }
        }

        public static string ReverseComplement(string sequence)
        {
            return string.Create(sequence.Length, sequence, (span, src) => ReverseComplement(src, span));
        }

        private static byte[] BuildMasks()
        {
            var masks = new byte[128];
            void Set(char c, int mask) { masks[c] = (byte)mask; }

            Set('A', 1); Set('C', 2); Set('G', 4); Set('T', 8);
            Set('R', 1 | 4); Set('Y', 2 | 8); Set('S', 2 | 4); Set('W', 1 | 8);
            Set('K', 4 | 8); Set('M', 1 | 2);
            Set('B', 2 | 4 | 8); Set('D', 1 | 4 | 8); Set('H', 1 | 2 | 8); Set('V', 1 | 2 | 4);
            Set('N', 15);
            return masks;
        }

        private static char[] BuildComplements()
        {
            var table = new char[128];
            Array.Fill(table, 'N');
            const string from = "ACGTRYSWKMBDHVN";
            const string to = "TGCAYRSWMKVHDBN";
            for (int i = 0; i < from.Length; i++)
            {
                table[from[i]] = to[i];
            }
            return table;
        }

        private static char[] BuildNormalization()
        {
            var table = new char[256];
            Array.Fill(table, Invalid);

            for (int b = 0; b < 256; b++)
            {
                char c = (char)b;
                if (char.IsWhiteSpace(c) || char.IsAsciiDigit(c) || IsGap((byte)b))
                {
                    table[b] = Skip;
                    continue;
                }

                char upper = char.ToUpperInvariant(c);
                if (upper == 'U') upper = 'T';
                if (upper < 128 && MaskByChar[upper] != 0)
                {
                    table[b] = upper;
                }
            }
            return table;
        }
    }
}
