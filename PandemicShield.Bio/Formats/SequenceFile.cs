using System.Text;

namespace PandemicShield.Bio.Formats
{
    /// <summary>Ділянка ознаки GenBank: координати 1-based включно на прямому ланцюзі.</summary>
    public readonly record struct Segment(long Start, long End)
    {
        public long Length => End - Start + 1;
    }

    /// <summary>
    /// Анотована ознака GenBank (CDS, gene, rRNA…). Ділянки в порядку транскрипції: для <c>join(266..13468,13468..21555)</c>
    /// (рамковий зсув ORF1ab коронавірусу) — дві ділянки, що перекриваються на одному нуклеотиді.
    /// </summary>
    public sealed record Feature(
        string Type,
        Strand Strand,
        IReadOnlyList<Segment> Segments,
        string? Gene,
        string? Product,
        string? LocusTag,
        string? Translation,
        bool Partial,
        int? TranslationTable = null)
    {
        public long Start => Segments.Min(s => s.Start);
        public long End => Segments.Max(s => s.End);

        /// <summary>Назва для підпису: ген, locus_tag або продукт.</summary>
        public string Label => Gene ?? LocusTag ?? Product ?? Type;
    }

    /// <summary>Послідовність з файлу: FASTA (лише послідовність) або GenBank (з анотацією і топологією).</summary>
    public sealed record SequenceRecord(string Id, string Description, string Sequence, bool Circular, IReadOnlyList<Feature> Features)
    {
        public double GcPercent
        {
            get
            {
                long gc = 0, acgt = 0;
                foreach (char c in Sequence)
                {
                    if (c is 'G' or 'C') { gc++; acgt++; }
                    else if (c is 'A' or 'T') acgt++;
                }
                return acgt == 0 ? 0 : 100.0 * gc / acgt;
            }
        }
    }

    /// <summary>
    /// Читає файл послідовностей для Explorer: формат визначається за першим рядком
    /// (<c>LOCUS</c> — GenBank, <c>&gt;</c> — FASTA). Послідовність нормалізується: верхній регістр, U → T,
    /// без пробілів і цифр. На відміну від <see cref="FastaStreamReader"/>, файл читається повністю — для одного геному чи плазміди.
    /// </summary>
    public static class SequenceFile
    {
        public static List<SequenceRecord> Read(TextReader reader)
        {
            string? first;
            while ((first = reader.ReadLine()) != null && first.Trim().Length == 0) { }
            if (first == null) throw new FormatException("Файл порожній.");

            if (first.StartsWith("LOCUS", StringComparison.Ordinal)) return GenBankReader.Read(reader, first);
            if (first.StartsWith('>')) return ReadFasta(reader, first);
            throw new FormatException("Невідомий формат: очікується FASTA (рядок «>…») або GenBank (рядок «LOCUS…»).");
        }

        private static List<SequenceRecord> ReadFasta(TextReader reader, string firstHeader)
        {
            var records = new List<SequenceRecord>();
            string header = firstHeader;
            var sequence = new StringBuilder();
            string? line;
            while (true)
            {
                line = reader.ReadLine();
                if (line == null || line.StartsWith('>'))
                {
                    string text = header[1..].Trim();
                    int space = text.IndexOf(' ');
                    records.Add(new SequenceRecord(space < 0 ? text : text[..space], space < 0 ? "" : text[(space + 1)..].Trim(),
                        sequence.ToString(), false, []));
                    if (line == null) break;
                    header = line;
                    sequence.Clear();
                }
                else
                {
                    AppendBases(sequence, line);
                }
            }
            return records;
        }

        internal static void AppendBases(StringBuilder sequence, string line)
        {
            foreach (char c in line)
            {
                if (char.IsLetter(c))
                {
                    char upper = char.ToUpperInvariant(c);
                    sequence.Append(upper == 'U' ? 'T' : upper);
                }
                else if (c is '-' or '*')
                {
                    sequence.Append('N');
                }
            }
        }
    }

    /// <summary>Розбір GenBank (flat file NCBI): LOCUS, DEFINITION, VERSION, таблиця ознак і ORIGIN.</summary>
    public static class GenBankReader
    {
        private const int QualifierColumn = 21;
        private static readonly HashSet<string> KeptTypes = new(StringComparer.Ordinal) { "CDS", "gene", "rRNA", "tRNA", "mat_peptide" };

        public static List<SequenceRecord> Read(TextReader reader, string locusLine)
        {
            var records = new List<SequenceRecord>();
            string? line = locusLine;
            while (line != null)
            {
                if (line.StartsWith("LOCUS", StringComparison.Ordinal)) records.Add(ReadRecord(reader, line));
                line = reader.ReadLine();
            }
            return records;
        }

        private static SequenceRecord ReadRecord(TextReader reader, string locus)
        {
            string[] locusParts = locus.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string id = locusParts.Length > 1 ? locusParts[1] : "record";
            bool circular = locusParts.Contains("circular", StringComparer.OrdinalIgnoreCase);
            var definition = new StringBuilder();
            var sequence = new StringBuilder();
            var features = new List<Feature>();
            string section = "";

            // Поточна ознака таблиці FEATURES: тип, рядок розташування і кваліфікатори (можуть займати кілька рядків).
            string? type = null;
            var location = new StringBuilder();
            var qualifiers = new List<(string Key, StringBuilder Value)>();

            void FlushFeature()
            {
                if (type != null && KeptTypes.Contains(type)) features.Add(BuildFeature(type, location.ToString(), qualifiers));
                type = null;
                location.Clear();
                qualifiers.Clear();
            }

            string? line;
            while ((line = reader.ReadLine()) != null && !line.StartsWith("//", StringComparison.Ordinal))
            {
                if (line.Length > 0 && line[0] != ' ')
                {
                    FlushFeature();
                    int space = line.IndexOf(' ');
                    section = space < 0 ? line : line[..space];
                    string rest = space < 0 ? "" : line[space..].Trim();
                    if (section == "DEFINITION") definition.Append(rest);
                    else if (section == "VERSION" && rest.Length > 0) id = rest.Split(' ')[0];
                    continue;
                }

                switch (section)
                {
                    case "DEFINITION":
                        definition.Append(' ').Append(line.Trim());
                        break;
                    case "FEATURES":
                        if (line.Length > 5 && line[5] != ' ')
                        {
                            FlushFeature();
                            string body = line.Trim();
                            int space = body.IndexOf(' ');
                            type = body[..space];
                            location.Append(body[space..].Trim());
                        }
                        else if (type != null && line.Length > QualifierColumn)
                        {
                            string body = line[QualifierColumn..];
                            if (body.StartsWith('/'))
                            {
                                int eq = body.IndexOf('=');
                                qualifiers.Add(eq < 0 ? (body[1..], new StringBuilder()) : (body[1..eq], new StringBuilder(body[(eq + 1)..])));
                            }
                            else if (qualifiers.Count == 0)
                            {
                                location.Append(body.Trim());
                            }
                            else
                            {
                                // Продовження значення: у /translation рядки склеюються без пробілу, в описах — через пробіл.
                                var (key, value) = qualifiers[^1];
                                if (key != "translation") value.Append(' ');
                                value.Append(body.Trim());
                            }
                        }
                        break;
                    case "ORIGIN":
                        SequenceFile.AppendBases(sequence, line);
                        break;
                }
            }
            FlushFeature();

            return new SequenceRecord(id, definition.ToString().Trim().TrimEnd('.'), sequence.ToString(), circular, features);
        }

        private static Feature BuildFeature(string type, string location, List<(string Key, StringBuilder Value)> qualifiers)
        {
            string? Get(string key)
            {
                var found = qualifiers.FirstOrDefault(q => q.Key == key);
                if (found.Value == null) return null;
                string value = found.Value.ToString().Trim();
                return value.Length >= 2 && value[0] == '"' && value[^1] == '"' ? value[1..^1].Replace("\"\"", "\"") : value;
            }

            var (segments, strand, partial) = ParseLocation(location);
            // /transl_table: таблиця генетичного коду NCBI (2 — мітохондрії хребетних, 11 — бактерії); без нього — стандартна (1).
            int? table = int.TryParse(Get("transl_table"), out int t) ? t : null;
            return new Feature(type, strand, segments, Get("gene"), Get("product"), Get("locus_tag"), Get("translation")?.Replace(" ", ""), partial, table);
        }

        /// <summary>
        /// Розташування ознаки: <c>123..456</c>, <c>&lt;1..&gt;100</c> (неповна), <c>complement(…)</c>, <c>join(…)</c>/<c>order(…)</c>
        /// і їхні комбінації. Ділянки з інших записів (<c>J00194.1:100..202</c>) і точки між нуклеотидами (<c>123^124</c>) пропускаються.
        /// </summary>
        public static (List<Segment> Segments, Strand Strand, bool Partial) ParseLocation(string location)
        {
            string text = string.Concat(location.Where(c => !char.IsWhiteSpace(c)));

            if (Wrapped(text, "complement", out string inner))
            {
                var (segments, strand, partial) = ParseLocation(inner);
                segments.Reverse();
                return (segments, strand == Strand.Forward ? Strand.Reverse : Strand.Forward, partial);
            }

            if (Wrapped(text, "join", out inner) || Wrapped(text, "order", out inner))
            {
                var all = new List<Segment>();
                var strands = new List<Strand>();
                bool partial = false;
                foreach (string part in SplitTopLevel(inner))
                {
                    var (segments, strand, p) = ParseLocation(part);
                    all.AddRange(segments);
                    if (segments.Count > 0) strands.Add(strand);
                    partial |= p;
                }
                return (all, strands.Count > 0 && strands.All(s => s == Strand.Reverse) ? Strand.Reverse : Strand.Forward, partial);
            }

            bool isPartial = text.Contains('<') || text.Contains('>');
            text = text.Replace("<", "").Replace(">", "");
            if (text.Contains(':') || text.Contains('^')) return ([], Strand.Forward, isPartial);

            int dots = text.IndexOf("..", StringComparison.Ordinal);
            if (dots < 0)
                return long.TryParse(text, out long single) ? ([new Segment(single, single)], Strand.Forward, isPartial) : ([], Strand.Forward, isPartial);
            return long.TryParse(text[..dots], out long start) && long.TryParse(text[(dots + 2)..], out long end)
                ? ([new Segment(start, end)], Strand.Forward, isPartial)
                : ([], Strand.Forward, isPartial);
        }

        private static bool Wrapped(string text, string function, out string inner)
        {
            inner = "";
            if (!text.StartsWith(function + "(", StringComparison.Ordinal) || !text.EndsWith(')')) return false;
            inner = text[(function.Length + 1)..^1];
            return true;
        }

        private static IEnumerable<string> SplitTopLevel(string text)
        {
            int depth = 0, start = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '(') depth++;
                else if (text[i] == ')') depth--;
                else if (text[i] == ',' && depth == 0)
                {
                    yield return text[start..i];
                    start = i + 1;
                }
            }
            yield return text[start..];
        }
    }
}
