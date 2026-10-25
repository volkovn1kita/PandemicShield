using System.Text;
using Microsoft.Extensions.Caching.Memory;
using PandemicShield.Bio;
using PandemicShield.Bio.Alignment;
using PandemicShield.Bio.Formats;

namespace PandemicShield.Api.Explorer
{
    /// <summary>ORF для карти Explorer: координати 1-based включно; для ORF через «нуль» кільцевої молекули End більший за довжину.</summary>
    public sealed record ExplorerOrf(
        int Id, char Strand, int Frame, long Start, long End, long Length, int AaLength, bool Wraps,
        string? MatchesCds, bool SameStartAsCds);

    /// <summary>
    /// Навчальний режим: відкритий файл (FASTA чи GenBank) тримається в пам'яті API кілька годин під токеном,
    /// а ORF, переклади й порівняння рахуються бібліотекою PandemicShield.Bio — тією самою, що й у конвеєрі аналізу.
    /// </summary>
    public sealed class ExplorerService
    {
        public const int MaxFileBytes = 30 * 1024 * 1024;
        public const int MaxOrfs = 20_000;
        private static readonly TimeSpan Lifetime = TimeSpan.FromHours(4);

        private readonly IMemoryCache _cache;

        public ExplorerService(IMemoryCache cache) => _cache = cache;

        public (Guid Token, List<SequenceRecord> Records) Open(TextReader reader)
        {
            var records = SequenceFile.Read(reader);
            if (records.Count == 0 || records.All(r => r.Sequence.Length == 0))
                throw new FormatException("У файлі немає жодної послідовності.");
            var token = Guid.NewGuid();
            _cache.Set(token, records, new MemoryCacheEntryOptions { SlidingExpiration = Lifetime });
            return (token, records);
        }

        public SequenceRecord? Record(Guid token, int index) =>
            _cache.TryGetValue(token, out List<SequenceRecord>? records) && index >= 0 && index < records!.Count ? records[index] : null;

        /// <summary>ORF у 6 рамках; кожен позначається, якщо він закінчується тим самим стоп-кодоном, що й анотований CDS.</summary>
        public static (List<ExplorerOrf> Orfs, bool Truncated) Orfs(SequenceRecord record, OrfFinderOptions options, bool circular)
        {
            var found = circular ? OrfFinder.FindCircular(record.Id, record.Sequence, options) : OrfFinder.Find(record.Id, record.Sequence, options);
            int length = record.Sequence.Length;
            var cds = record.Features.Where(f => f.Type == "CDS" && f.Segments.Count > 0).ToList();

            bool truncated = found.Count > MaxOrfs;
            // Забагато ORF (бактеріальний геном з малим порогом довжини): лишаємо найдовші.
            var kept = truncated ? found.OrderByDescending(o => o.Length).Take(MaxOrfs).OrderBy(o => o.Start).ToList() : found;

            var result = new List<ExplorerOrf>(kept.Count);
            for (int i = 0; i < kept.Count; i++)
            {
                var orf = kept[i];
                char strand = orf.Strand == Strand.Forward ? '+' : '-';
                var match = MatchingCds(orf, cds, length);
                result.Add(new ExplorerOrf(i, strand, Frame(orf, length), orf.Start + 1, orf.End, orf.Length, orf.Protein.Length,
                    orf.End > length, match?.Label, match != null && SameStart(orf, match, length)));
            }
            return (result, truncated);
        }

        /// <summary>Рамка: +1, +2, +3 за початком на прямому ланцюзі; −1, −2, −3 — від кінця запису на зворотному.</summary>
        public static int Frame(Orf orf, int length) => orf.Strand == Strand.Forward
            ? (int)(orf.Start % 3) + 1
            : -(int)(((length - orf.End) % 3 + 3) % 3 + 1);

        // Той самий стоп-кодон і ланцюг = той самий ген (старт ORF може бути раніше: «найдовший ORF» бере перший ATG).
        private static Feature? MatchingCds(Orf orf, IEnumerable<Feature> cds, int length)
        {
            foreach (var feature in cds)
            {
                if ((feature.Strand == Strand.Forward) != (orf.Strand == Strand.Forward)) continue;
                long stop = orf.Strand == Strand.Forward ? orf.End : orf.Start + 1;
                long featureStop = orf.Strand == Strand.Forward ? feature.Segments[^1].End : feature.Segments[^1].Start;
                if (Mod(stop, length) == Mod(featureStop, length)) return feature;
            }
            return null;
        }

        private static bool SameStart(Orf orf, Feature feature, int length)
        {
            long start = orf.Strand == Strand.Forward ? orf.Start + 1 : orf.End;
            long featureStart = orf.Strand == Strand.Forward ? feature.Segments[0].Start : feature.Segments[0].End;
            return Mod(start, length) == Mod(featureStart, length);
        }

        private static long Mod(long position, int length) => ((position - 1) % length + length) % length;

        /// <summary>
        /// Нуклеотиди й білок ділянки: сегменти 1-based включно в порядку транскрипції (для join — кілька), ланцюг, таблиця коду.
        /// На кільцевій молекулі сегмент може виходити за кінець — продовжується з початку.
        /// </summary>
        public static (string Nucleotides, string Protein) Region(SequenceRecord record, IReadOnlyList<Segment> segments, Strand strand, GeneticCode code)
        {
            string sequence = record.Sequence;
            int length = sequence.Length;
            var forward = new StringBuilder();
            foreach (var segment in strand == Strand.Forward ? segments : segments.Reverse())
            {
                for (long p = segment.Start; p <= segment.End; p++)
                    forward.Append(sequence[(int)Mod(p, length)]);
            }
            string nucleotides = strand == Strand.Forward ? forward.ToString() : Nucleotides.ReverseComplement(forward.ToString());

            var protein = new StringBuilder(nucleotides.Length / 3);
            for (int i = 0; i + 3 <= nucleotides.Length; i += 3)
            {
                char aa = code.Translate(nucleotides[i], nucleotides[i + 1], nucleotides[i + 2]);
                if (aa == '*' && i + 3 >= nucleotides.Length) break;
                protein.Append(aa);
            }
            return (nucleotides, protein.ToString());
        }

        public static PairwiseAlignment Compare(string a, string b, bool protein) =>
            PairwiseAligner.Align(a, b, protein ? ScoringScheme.Protein : ScoringScheme.Nucleotide);
    }
}
