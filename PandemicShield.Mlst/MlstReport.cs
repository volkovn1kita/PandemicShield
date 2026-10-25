using System.Text.Json;

namespace PandemicShield.Mlst
{
    /// <summary>
    /// Результат <c>mlst --json</c> для однієї збірки: схема PubMLST, тип штаму (ST) і алелі семи генів.
    /// Позначення алелів (з README mlst): <c>3</c> — точний відомий алель, <c>~3</c> — новий повнорозмірний алель,
    /// схожий на 3, <c>3?</c> — частковий збіг, <c>3,4</c> — кілька копій, <c>-</c> — ген не знайдено.
    /// ST <c>-</c> означає, що профіль новий або неповний.
    /// </summary>
    public sealed record MlstResult(string Scheme, string SequenceType, IReadOnlyList<(string Gene, string Allele)> Alleles)
    {
        public bool HasSequenceType => SequenceType != "-" && SequenceType.All(char.IsDigit);

        /// <summary>Схему не визначено: жоден ген жодної схеми не знайдено.</summary>
        public bool NoScheme => Scheme == "-";

        public int ExactAlleles => Alleles.Count(a => a.Allele.All(char.IsDigit) && a.Allele.Length > 0);

        public int MissingAlleles => Alleles.Count(a => a.Allele == "-");

        /// <summary>Новий алель (~) або частковий збіг (?): збірка може бути неповною, а штам — новим.</summary>
        public int UncertainAlleles => Alleles.Count(a => a.Allele.StartsWith('~') || a.Allele.EndsWith('?') || a.Allele.Contains(','));

        /// <summary>Як у таблицях PubMLST: "gapA(3) infB(3) mdh(1) …", гени за абеткою.</summary>
        public string AlleleProfile => string.Join(' ', Alleles.Select(a => $"{a.Gene}({a.Allele})"));
    }

    public static class MlstReport
    {
        public static MlstResult Read(string json)
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            // mlst пише масив (по елементу на вхідний файл); ми завжди передаємо один файл.
            var entry = root.ValueKind == JsonValueKind.Array
                ? root.EnumerateArray().FirstOrDefault()
                : root;
            if (entry.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("mlst повернув порожній результат.");

            string scheme = entry.TryGetProperty("scheme", out var s) ? s.GetString() ?? "-" : "-";
            string st = entry.TryGetProperty("sequence_type", out var t) ? t.GetString() ?? "-" : "-";
            var alleles = new List<(string, string)>();
            if (entry.TryGetProperty("alleles", out var a) && a.ValueKind == JsonValueKind.Object)
            {
                foreach (var allele in a.EnumerateObject())
                    alleles.Add((allele.Name, allele.Value.GetString() ?? "-"));
            }
            alleles.Sort((x, y) => string.CompareOrdinal(x.Item1, y.Item1));
            return new MlstResult(scheme, st, alleles);
        }

        /// <summary>Підсумок для користувача українською.</summary>
        public static string Describe(MlstResult result)
        {
            if (result.NoScheme) return "Схему MLST не визначено: у збірці не знайдено генів жодної схеми PubMLST.";
            if (result.HasSequenceType)
                return result.UncertainAlleles == 0
                    ? $"ST{result.SequenceType} ({result.Scheme})"
                    : $"ST{result.SequenceType} ({result.Scheme}), неточних алелів: {result.UncertainAlleles}";
            if (result.MissingAlleles == result.Alleles.Count)
                return $"Тип штаму не визначено: генів схеми {result.Scheme} у збірці немає (можливо, збірка неповна або це інший вид).";
            return $"Новий або неповний профіль ({result.Scheme}): відомих алелів {result.ExactAlleles} з {result.Alleles.Count}.";
        }
    }
}
