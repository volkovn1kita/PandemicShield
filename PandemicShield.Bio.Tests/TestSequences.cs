using System.Text;

namespace PandemicShield.Bio.Tests
{
    internal static class TestSequences
    {
        public const int ApiChunkSize = 32 * 1024;

        public static string DataPath(string fileName) => Path.Combine(AppContext.BaseDirectory, "Data", fileName);

        /// <summary>Читає FASTA так само, як це робить конвеєр: шматками по 32 KB через <see cref="FastaStreamReader"/>.</summary>
        public static List<(string Id, string Description, string Sequence)> ReadFasta(byte[] bytes, int chunkSize = ApiChunkSize)
        {
            var sink = new CollectingSink();
            var reader = new FastaStreamReader(sink);
            for (int offset = 0; offset < bytes.Length; offset += chunkSize)
            {
                reader.Feed(bytes.AsSpan(offset, Math.Min(chunkSize, bytes.Length - offset)));
            }
            reader.Complete();
            return sink.Records;
        }

        public static string SarsCov2Reference() => ReadFasta(File.ReadAllBytes(DataPath("MN908947.fasta"))).Single().Sequence;

        /// <summary>Розбиває запис на вікна і збирає ORF з усіх вікон.</summary>
        public static List<Orf> FindOrfsWindowed(string id, string sequence, int coreSize, int flankSize, OrfFinderOptions options, int feedSize = 997)
        {
            var windower = new SequenceWindower(coreSize, flankSize);
            windower.OnRecordStart(id, string.Empty);
            for (int offset = 0; offset < sequence.Length; offset += feedSize)
            {
                windower.OnBases(sequence.AsSpan(offset, Math.Min(feedSize, sequence.Length - offset)));
            }
            windower.OnRecordEnd();

            return windower.TakeReady()
                .SelectMany(w => OrfFinder.Find(w, options))
                .OrderBy(o => o.Start).ThenBy(o => o.Strand)
                .ToList();
        }

        public static string RandomDna(int length, int seed)
        {
            var random = new Random(seed);
            var sb = new StringBuilder(length);
            for (int i = 0; i < length; i++) sb.Append("ACGT"[random.Next(4)]);
            return sb.ToString();
        }

        public sealed class CollectingSink : IFastaSink
        {
            private StringBuilder? _current;
            private string _id = string.Empty;
            private string _description = string.Empty;

            public List<(string Id, string Description, string Sequence)> Records { get; } = new();

            public void OnRecordStart(string id, string description)
            {
                _id = id;
                _description = description;
                _current = new StringBuilder();
            }

            public void OnBases(ReadOnlySpan<char> bases) => _current!.Append(bases);

            public void OnRecordEnd() => Records.Add((_id, _description, _current!.ToString()));
        }
    }
}
