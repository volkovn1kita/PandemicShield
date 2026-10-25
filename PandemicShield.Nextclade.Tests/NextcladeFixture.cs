using PandemicShield.Nextclade.Results;

namespace PandemicShield.Nextclade.Tests
{
    /// <summary>
    /// Справжній вихід Nextclade 3.24.0 (набір nextstrain/sars-cov-2/wuhan-hu-1/orfs) для Data/sars-cov-2-examples.fasta:
    /// сім геномів із прикладів набору даних Nextstrain і одна закоротка послідовність.
    /// Поля, які PandemicShield не читає, з файлу прибрано, щоб він був меншим.
    /// </summary>
    internal static class NextcladeFixture
    {
        public const string Dataset = "nextstrain/sars-cov-2/wuhan-hu-1/orfs";

        public const string Alpha = "USA/MA-CDCBI-CRSP_EFLUYAQE2ICW2C6E/2021";
        public const string Beta = "OU205023";
        public const string Delta = "USA/OKPHLCOV0000666/2021";
        public const string Ba2 = "OP523232";
        public const string Jn = "OY754632";
        public const string Eg51 = "USA/CA-LACPHL-AY03267/2023";
        public const string Jf4 = "USA/CA-LACPHL-AY03247/2023";
        public const string TooShort = "too_short";

        public static readonly IReadOnlyList<SequenceAnalysis> Sequences = Load();

        public static SequenceAnalysis Get(string seqName) => Sequences.Single(s => s.SeqName == seqName);

        private static List<SequenceAnalysis> Load()
        {
            using var reader = new StreamReader(Path.Combine(AppContext.BaseDirectory, "Data", "sars-cov-2-examples.nextclade.ndjson"));
            return NextcladeNdjsonReader.Read(reader).ToList();
        }
    }
}
