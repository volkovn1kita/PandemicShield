namespace PandemicShield.Api.Imports
{
    /// <summary>Заголовки записів FASTA: номер (перше слово після «&gt;») і опис.</summary>
    public static class FastaHeaders
    {
        public sealed record Header(string Id, string Description);

        public static List<Header> Read(TextReader reader)
        {
            var headers = new List<Header>();
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.Length == 0 || line[0] != '>') continue;
                string text = line[1..].Trim();
                int space = text.IndexOf(' ');
                headers.Add(space < 0 ? new Header(text, string.Empty) : new Header(text[..space], text[(space + 1)..].Trim()));
            }
            return headers;
        }

        public static List<Header> Read(string path)
        {
            using var reader = new StreamReader(path);
            return Read(reader);
        }

        /// <summary>Назва запуску з кількох номерів: «NCBI: OQ291490.1, MN908947.3 +23».</summary>
        public static string RunName(IReadOnlyList<string> ids)
        {
            const int shown = 2;
            string head = string.Join(", ", ids.Take(shown));
            return ids.Count > shown ? $"NCBI: {head} +{ids.Count - shown}" : $"NCBI: {head}";
        }

        /// <summary>Вид з опису запису GenBank: "Klebsiella pneumoniae strain KP1 chromosome, complete genome" → перші два слова.</summary>
        public static string? Organism(string description)
        {
            string[] words = description.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return words.Length >= 2 ? $"{words[0]} {words[1]}" : null;
        }
    }
}
