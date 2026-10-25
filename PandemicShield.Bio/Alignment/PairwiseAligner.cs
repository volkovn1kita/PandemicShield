using System.Text;

namespace PandemicShield.Bio.Alignment
{
    public enum DifferenceKind
    {
        Substitution,

        /// <summary>У послідовності B є залишки, яких немає в A.</summary>
        Insertion,

        /// <summary>Залишків A немає в B.</summary>
        Deletion
    }

    /// <summary>Відмінність B від A. Позиції 1-based: для вставки — позиція в A, після якої вона стоїть.</summary>
    public sealed record AlignmentDifference(DifferenceKind Kind, long PositionA, long PositionB, string A, string B);

    public sealed record PairwiseAlignment(
        string AlignedA,
        string AlignedB,
        int Score,
        int Matches,
        int Mismatches,
        int Unknown,
        int AlignedColumns,
        IReadOnlyList<AlignmentDifference> Differences)
    {
        /// <summary>Частка однакових залишків серед вирівняних позицій (без звисів на кінцях і невідомих N/X).</summary>
        public double Identity => Matches + Mismatches == 0 ? 0 : (double)Matches / (Matches + Mismatches);
    }

    /// <summary>Оцінки: заміна залежить від пари залишків; розрив коштує відкриття + подовження на кожен залишок (афінні штрафи).</summary>
    public sealed class ScoringScheme
    {
        public required Func<char, char, int> Substitution { get; init; }
        public required int GapOpen { get; init; }
        public required int GapExtend { get; init; }

        /// <summary>Невизначений залишок (N у ДНК, X у білку): не рахується ні збігом, ні заміною.</summary>
        public required char Unknown { get; init; }

        public static ScoringScheme Nucleotide { get; } = new()
        {
            Substitution = (a, b) => a == 'N' || b == 'N' ? 0 : a == b ? 2 : -3,
            GapOpen = -5,
            GapExtend = -2,
            Unknown = 'N'
        };

        public static ScoringScheme Protein { get; } = new()
        {
            Substitution = Blosum62.Score,
            GapOpen = -11,
            GapExtend = -1,
            Unknown = 'X'
        };
    }

    /// <summary>
    /// Глобальне вирівнювання двох послідовностей (Ґотох, афінні розриви) у смузі навколо діагоналі.
    /// Смуга робить порівняння двох геномів SARS-CoV-2 (по ~30 тис. нт) швидким і не потребує гігабайтів пам'яті;
    /// для близьких послідовностей (варіанти одного вірусу, алелі гена) результат такий самий, як без смуги.
    /// Розриви на кінцях безкоштовні: геноми часто обрізані по-різному, і це не мутації.
    /// </summary>
    public static class PairwiseAligner
    {
        public const long MaxCells = 80_000_000;
        private const int NegInf = int.MinValue / 4;

        /// <summary>Ширина смуги за замовчуванням: різниця довжин плюс запас на інделі (2% довжини, від 64 до 600).</summary>
        public static int DefaultBand(int n, int m) => Math.Abs(n - m) + Math.Clamp(Math.Max(n, m) / 50, 64, 600);

        public static PairwiseAlignment Align(string a, string b, ScoringScheme scheme, int? band = null)
        {
            int n = a.Length, m = b.Length;
            if (n == 0 || m == 0) throw new ArgumentException("Обидві послідовності мають бути непорожні.");
            int w = band ?? DefaultBand(n, m);

            // Межі смуги для кожного рядка: навколо «масштабованої» діагоналі j ≈ i·m/n.
            var lo = new int[n + 1];
            var hi = new int[n + 1];
            var rowBase = new long[n + 2];
            for (int i = 0; i <= n; i++)
            {
                int center = (int)((long)i * m / n);
                lo[i] = Math.Max(0, center - w);
                hi[i] = Math.Min(m, center + w);
                rowBase[i + 1] = rowBase[i] + (hi[i] - lo[i] + 1);
            }
            if (rowBase[n + 1] > MaxCells)
                throw new InvalidOperationException("Послідовності надто довгі або надто різні для порівняння (обмеження пам'яті).");

            // Траса: 2 біти на кожну з трьох матриць (звідки прийшли M, X, Y).
            var trace = new byte[rowBase[n + 1]];
            int width = m + 1;
            int[] mPrev = Filled(width), xPrev = Filled(width), yPrev = Filled(width);
            int[] mCur = Filled(width), xCur = Filled(width), yCur = Filled(width);
            int open = scheme.GapOpen + scheme.GapExtend, extend = scheme.GapExtend;

            // Рядок 0: розриви на початку безкоштовні.
            for (int j = lo[0]; j <= hi[0]; j++)
            {
                mPrev[j] = j == 0 ? 0 : NegInf;
                yPrev[j] = j == 0 ? NegInf : 0;
                trace[rowBase[0] + j - lo[0]] = (byte)(2 << 4);
            }

            int bestScore = NegInf, bestI = 0, bestJ = 0, bestMatrix = 0;
            void Consider(int score, int i, int j, int matrix)
            {
                if (score > bestScore) (bestScore, bestI, bestJ, bestMatrix) = (score, i, j, matrix);
            }

            for (int i = 1; i <= n; i++)
            {
                Array.Fill(mCur, NegInf, lo[i], hi[i] - lo[i] + 1);
                Array.Fill(xCur, NegInf, lo[i], hi[i] - lo[i] + 1);
                Array.Fill(yCur, NegInf, lo[i], hi[i] - lo[i] + 1);
                char ai = a[i - 1];
                for (int j = lo[i]; j <= hi[i]; j++)
                {
                    byte t = 0;
                    if (j == 0)
                    {
                        // Колонка 0: розрив на початку B безкоштовний.
                        xCur[0] = 0;
                        t = 1 << 2;
                    }
                    else
                    {
                        bool diag = j - 1 >= lo[i - 1] && j - 1 <= hi[i - 1];
                        if (diag)
                        {
                            int s = scheme.Substitution(ai, b[j - 1]);
                            var (best, from) = Max3(mPrev[j - 1], xPrev[j - 1], yPrev[j - 1]);
                            if (best > NegInf) mCur[j] = best + s;
                            t |= (byte)from;
                        }

                        bool up = j >= lo[i - 1] && j <= hi[i - 1];
                        if (up)
                        {
                            var (best, from) = Max3(mPrev[j] + open, xPrev[j] + extend, yPrev[j] + open);
                            xCur[j] = best;
                            t |= (byte)(from << 2);
                        }

                        if (j - 1 >= lo[i])
                        {
                            var (best, from) = Max3(mCur[j - 1] + open, xCur[j - 1] + open, yCur[j - 1] + extend);
                            yCur[j] = best;
                            t |= (byte)(from << 4);
                        }
                    }
                    trace[rowBase[i] + j - lo[i]] = t;
                }

                // Кінець B досягнуто раніше за кінець A: решта A — безкоштовний звис.
                if (hi[i] == m)
                {
                    Consider(mCur[m], i, m, 0);
                    Consider(xCur[m], i, m, 1);
                    Consider(yCur[m], i, m, 2);
                }

                (mPrev, mCur) = (mCur, mPrev);
                (xPrev, xCur) = (xCur, xPrev);
                (yPrev, yCur) = (yCur, yPrev);
                Array.Fill(mCur, NegInf, lo[i], hi[i] - lo[i] + 1);
                Array.Fill(xCur, NegInf, lo[i], hi[i] - lo[i] + 1);
                Array.Fill(yCur, NegInf, lo[i], hi[i] - lo[i] + 1);
            }
            // Останній рядок: кінець A досягнуто, решта B — звис.
            for (int j = lo[n]; j <= hi[n]; j++)
            {
                Consider(mPrev[j], n, j, 0);
                Consider(xPrev[j], n, j, 1);
                Consider(yPrev[j], n, j, 2);
            }

            return Traceback(a, b, scheme, trace, lo, rowBase, bestI, bestJ, bestMatrix, bestScore);
        }

        private static PairwiseAlignment Traceback(string a, string b, ScoringScheme scheme, byte[] trace, int[] lo, long[] rowBase,
            int i, int j, int matrix, int score)
        {
            int n = a.Length, m = b.Length;
            var alignedA = new StringBuilder();
            var alignedB = new StringBuilder();

            // Звиси після кінця вирівнювання (вони безкоштовні).
            for (int k = n; k > i; k--) { alignedA.Append(a[k - 1]); alignedB.Append('-'); }
            for (int k = m; k > j; k--) { alignedA.Append('-'); alignedB.Append(b[k - 1]); }

            while (i > 0 || j > 0)
            {
                if (i == 0) { alignedA.Append('-'); alignedB.Append(b[--j]); continue; }
                if (j == 0) { alignedA.Append(a[--i]); alignedB.Append('-'); continue; }

                byte t = trace[rowBase[i] + j - lo[i]];
                switch (matrix)
                {
                    case 0:
                        matrix = t & 3;
                        alignedA.Append(a[--i]);
                        alignedB.Append(b[--j]);
                        break;
                    case 1:
                        matrix = (t >> 2) & 3;
                        alignedA.Append(a[--i]);
                        alignedB.Append('-');
                        break;
                    default:
                        matrix = (t >> 4) & 3;
                        alignedA.Append('-');
                        alignedB.Append(b[--j]);
                        break;
                }
            }

            string ra = Reverse(alignedA), rb = Reverse(alignedB);
            return Summarize(ra, rb, scheme, score);
        }

        private static PairwiseAlignment Summarize(string alignedA, string alignedB, ScoringScheme scheme, int score)
        {
            // Звиси на кінцях (розрив від краю до першої спільної позиції) — не мутації.
            int first = 0, last = alignedA.Length - 1;
            while (first <= last && (alignedA[first] == '-' || alignedB[first] == '-')) first++;
            while (last >= first && (alignedA[last] == '-' || alignedB[last] == '-')) last--;

            var differences = new List<AlignmentDifference>();
            int matches = 0, mismatches = 0, unknown = 0;
            long posA = 0, posB = 0;
            for (int k = 0; k < first; k++)
            {
                if (alignedA[k] != '-') posA++;
                if (alignedB[k] != '-') posB++;
            }

            for (int k = first; k <= last; k++)
            {
                char x = alignedA[k], y = alignedB[k];
                if (x != '-' && y != '-')
                {
                    posA++;
                    posB++;
                    if (x == scheme.Unknown || y == scheme.Unknown) unknown++;
                    else if (x == y) matches++;
                    else
                    {
                        mismatches++;
                        differences.Add(new AlignmentDifference(DifferenceKind.Substitution, posA, posB, x.ToString(), y.ToString()));
                    }
                    continue;
                }

                // Суцільний розрив — одна відмінність (делеція чи вставка кількох залишків).
                int end = k;
                bool gapInB = y == '-';
                while (end + 1 <= last && (gapInB ? alignedB[end + 1] == '-' && alignedA[end + 1] != '-' : alignedA[end + 1] == '-' && alignedB[end + 1] != '-')) end++;
                string a = gapInB ? alignedA[k..(end + 1)] : "";
                string b = gapInB ? "" : alignedB[k..(end + 1)];
                if (gapInB)
                {
                    differences.Add(new AlignmentDifference(DifferenceKind.Deletion, posA + 1, posB, a, ""));
                    posA += a.Length;
                }
                else
                {
                    differences.Add(new AlignmentDifference(DifferenceKind.Insertion, posA, posB + 1, "", b));
                    posB += b.Length;
                }
                k = end;
            }

            return new PairwiseAlignment(alignedA, alignedB, score, matches, mismatches, unknown, Math.Max(0, last - first + 1), differences);
        }

        private static (int Score, int From) Max3(int m, int x, int y)
        {
            if (m >= x && m >= y) return (m, 0);
            return x >= y ? (x, 1) : (y, 2);
        }

        private static int[] Filled(int length)
        {
            var array = new int[length];
            Array.Fill(array, NegInf);
            return array;
        }

        private static string Reverse(StringBuilder builder)
        {
            var chars = new char[builder.Length];
            for (int k = 0; k < chars.Length; k++) chars[k] = builder[builder.Length - 1 - k];
            return new string(chars);
        }
    }

    /// <summary>BLOSUM62 — стандартна матриця замін амінокислот (NCBI BLAST).</summary>
    public static class Blosum62
    {
        private const string Order = "ARNDCQEGHILKMFPSTWYVBZX*";

        private static readonly sbyte[,] Matrix =
        {
            { 4, -1, -2, -2, 0, -1, -1, 0, -2, -1, -1, -1, -1, -2, -1, 1, 0, -3, -2, 0, -2, -1, 0, -4 },
            { -1, 5, 0, -2, -3, 1, 0, -2, 0, -3, -2, 2, -1, -3, -2, -1, -1, -3, -2, -3, -1, 0, -1, -4 },
            { -2, 0, 6, 1, -3, 0, 0, 0, 1, -3, -3, 0, -2, -3, -2, 1, 0, -4, -2, -3, 3, 0, -1, -4 },
            { -2, -2, 1, 6, -3, 0, 2, -1, -1, -3, -4, -1, -3, -3, -1, 0, -1, -4, -3, -3, 4, 1, -1, -4 },
            { 0, -3, -3, -3, 9, -3, -4, -3, -3, -1, -1, -3, -1, -2, -3, -1, -1, -2, -2, -1, -3, -3, -2, -4 },
            { -1, 1, 0, 0, -3, 5, 2, -2, 0, -3, -2, 1, 0, -3, -1, 0, -1, -2, -1, -2, 0, 3, -1, -4 },
            { -1, 0, 0, 2, -4, 2, 5, -2, 0, -3, -3, 1, -2, -3, -1, 0, -1, -3, -2, -2, 1, 4, -1, -4 },
            { 0, -2, 0, -1, -3, -2, -2, 6, -2, -4, -4, -2, -3, -3, -2, 0, -2, -2, -3, -3, -1, -2, -1, -4 },
            { -2, 0, 1, -1, -3, 0, 0, -2, 8, -3, -3, -1, -2, -1, -2, -1, -2, -2, 2, -3, 0, 0, -1, -4 },
            { -1, -3, -3, -3, -1, -3, -3, -4, -3, 4, 2, -3, 1, 0, -3, -2, -1, -3, -1, 3, -3, -3, -1, -4 },
            { -1, -2, -3, -4, -1, -2, -3, -4, -3, 2, 4, -2, 2, 0, -3, -2, -1, -2, -1, 1, -4, -3, -1, -4 },
            { -1, 2, 0, -1, -3, 1, 1, -2, -1, -3, -2, 5, -1, -3, -1, 0, -1, -3, -2, -2, 0, 1, -1, -4 },
            { -1, -1, -2, -3, -1, 0, -2, -3, -2, 1, 2, -1, 5, 0, -2, -1, -1, -1, -1, 1, -3, -1, -1, -4 },
            { -2, -3, -3, -3, -2, -3, -3, -3, -1, 0, 0, -3, 0, 6, -4, -2, -2, 1, 3, -1, -3, -3, -1, -4 },
            { -1, -2, -2, -1, -3, -1, -1, -2, -2, -3, -3, -1, -2, -4, 7, -1, -1, -4, -3, -2, -2, -1, -2, -4 },
            { 1, -1, 1, 0, -1, 0, 0, 0, -1, -2, -2, 0, -1, -2, -1, 4, 1, -3, -2, -2, 0, 0, 0, -4 },
            { 0, -1, 0, -1, -1, -1, -1, -2, -2, -1, -1, -1, -1, -2, -1, 1, 5, -2, -2, 0, -1, -1, 0, -4 },
            { -3, -3, -4, -4, -2, -2, -3, -2, -2, -3, -2, -3, -1, 1, -4, -3, -2, 11, 2, -3, -4, -3, -2, -4 },
            { -2, -2, -2, -3, -2, -1, -2, -3, 2, -1, -1, -2, -1, 3, -3, -2, -2, 2, 7, -1, -3, -2, -1, -4 },
            { 0, -3, -3, -3, -1, -2, -2, -3, -3, 3, 1, -2, 1, -1, -2, -2, 0, -3, -1, 4, -3, -2, -1, -4 },
            { -2, -1, 3, 4, -3, 0, 1, -1, 0, -3, -4, 0, -3, -3, -2, 0, -1, -4, -3, -3, 4, 1, -1, -4 },
            { -1, 0, 0, 1, -3, 3, 4, -2, 0, -3, -3, 1, -1, -3, -1, 0, -1, -3, -2, -2, 1, 4, -1, -4 },
            { 0, -1, -1, -1, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -2, 0, 0, -2, -1, -1, -1, -1, -1, -4 },
            { -4, -4, -4, -4, -4, -4, -4, -4, -4, -4, -4, -4, -4, -4, -4, -4, -4, -4, -4, -4, -4, -4, -4, 1 }
        };

        private static readonly int[] IndexByChar = BuildIndex();

        private static int[] BuildIndex()
        {
            var index = new int[128];
            Array.Fill(index, Order.IndexOf('X'));
            for (int k = 0; k < Order.Length; k++)
            {
                index[Order[k]] = k;
                index[char.ToLowerInvariant(Order[k])] = k;
            }
            return index;
        }

        public static int Score(char a, char b) =>
            Matrix[a < 128 ? IndexByChar[a] : IndexByChar['X'], b < 128 ? IndexByChar[b] : IndexByChar['X']];
    }
}
