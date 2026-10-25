using System.Text.Json;

namespace PandemicShield.Nextclade.Results
{
    /// <summary>
    /// Читає вихід <c>nextclade run --output-ndjson</c>: один JSON-об'єкт на рядок, по одному на послідовність.
    /// Рядки читаються по одному, тож пам'ять не залежить від кількості послідовностей.
    /// </summary>
    public static class NextcladeNdjsonReader
    {
        public static IEnumerable<SequenceAnalysis> Read(TextReader reader)
        {
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                yield return ParseLine(line);
            }
        }

        public static SequenceAnalysis ParseLine(string json)
        {
            using var document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;

            var errors = Strings(root, "errors");
            if (errors.Count > 0)
            {
                return new SequenceAnalysis
                {
                    Index = Int(root, "index"),
                    SeqName = String(root, "seqName") ?? string.Empty,
                    Errors = errors
                };
            }

            JsonElement qc = root.TryGetProperty("qc", out var q) ? q : default;

            return new SequenceAnalysis
            {
                Index = Int(root, "index"),
                SeqName = String(root, "seqName") ?? string.Empty,
                Warnings = Strings(root, "warnings"),
                Clade = String(root, "clade"),
                CustomNodeAttributes = Attributes(root),
                QcStatus = qc.ValueKind == JsonValueKind.Object ? String(qc, "overallStatus") : null,
                QcScore = qc.ValueKind == JsonValueKind.Object ? Double(qc, "overallScore") : null,
                Coverage = Double(root, "coverage"),
                IsReverseComplement = root.TryGetProperty("isReverseComplement", out var rc) && rc.ValueKind == JsonValueKind.True,
                TotalSubstitutions = Int(root, "totalSubstitutions"),
                TotalDeletions = Int(root, "totalDeletions"),
                TotalInsertions = Int(root, "totalInsertions"),
                TotalMissing = Int(root, "totalMissing"),
                TotalFrameShifts = Int(root, "totalFrameShifts"),
                Mutations = Mutations(root)
            };
        }

        private static List<Mutation> Mutations(JsonElement root)
        {
            var mutations = new List<Mutation>();
            var codonStarts = CodonStarts(root);

            foreach (var s in Array(root, "substitutions"))
            {
                mutations.Add(new Mutation(MutationKind.NucleotideSubstitution, null,
                    Int(s, "pos") + 1, null, String(s, "refNuc"), String(s, "qryNuc")));
            }

            foreach (var d in Array(root, "deletions"))
            {
                JsonElement range = d.GetProperty("range");
                // range: 0-based, кінець не включається → 1-based включно.
                mutations.Add(new Mutation(MutationKind.NucleotideDeletion, null,
                    Int(range, "begin") + 1, Int(range, "end"), null, "-"));
            }

            foreach (var i in Array(root, "insertions"))
            {
                // Вставка після позиції pos (0-based) → після позиції pos + 1 (1-based).
                mutations.Add(new Mutation(MutationKind.NucleotideInsertion, null,
                    Int(i, "pos") + 1, null, null, String(i, "ins")));
            }

            foreach (var a in Array(root, "aaSubstitutions"))
            {
                string cds = String(a, "cdsName")!;
                int pos = Int(a, "pos");
                mutations.Add(new Mutation(MutationKind.AminoAcidSubstitution, cds, pos + 1, null,
                    String(a, "refAa"), String(a, "qryAa"), codonStarts.GetValueOrDefault((cds, pos))));
            }

            foreach (var a in Array(root, "aaDeletions"))
            {
                string cds = String(a, "cdsName")!;
                int pos = Int(a, "pos");
                mutations.Add(new Mutation(MutationKind.AminoAcidDeletion, cds, pos + 1, null,
                    String(a, "refAa"), "-", codonStarts.GetValueOrDefault((cds, pos))));
            }

            foreach (var f in Array(root, "frameShifts"))
            {
                JsonElement codon = f.GetProperty("codon");
                mutations.Add(new Mutation(MutationKind.FrameShift, String(f, "cdsName"),
                    Int(codon, "begin") + 1, Int(codon, "end"), null, null));
            }

            return mutations;
        }

        // aaChangesGroups містить для кожної зміненої амінокислоти позицію першої основи кодона в референсі (nucPos).
        private static Dictionary<(string Cds, int Pos), int?> CodonStarts(JsonElement root)
        {
            var result = new Dictionary<(string, int), int?>();
            foreach (var group in Array(root, "aaChangesGroups"))
            {
                foreach (var change in Array(group, "changes"))
                {
                    if (change.TryGetProperty("nucPos", out var nucPos) && nucPos.ValueKind == JsonValueKind.Number)
                    {
                        result[(String(change, "cdsName")!, Int(change, "pos"))] = nucPos.GetInt32() + 1;
                    }
                }
            }
            return result;
        }

        private static Dictionary<string, string> Attributes(JsonElement root)
        {
            var result = new Dictionary<string, string>();
            if (root.TryGetProperty("customNodeAttributes", out var attributes) && attributes.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in attributes.EnumerateObject())
                {
                    if (property.Value.ValueKind == JsonValueKind.String)
                    {
                        result[property.Name] = property.Value.GetString()!;
                    }
                }
            }
            return result;
        }

        private static IEnumerable<JsonElement> Array(JsonElement element, string name) =>
            element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
                ? value.EnumerateArray()
                : Enumerable.Empty<JsonElement>();

        // Попередження бувають рядками або об'єктами {"cdsName": ..., "warning": ...}.
        private static List<string> Strings(JsonElement element, string name) =>
            Array(element, name)
                .Select(e => e.ValueKind == JsonValueKind.String
                    ? e.GetString()!
                    : e.TryGetProperty("warning", out var w) ? w.ToString() : e.ToString())
                .ToList();

        private static string? String(JsonElement element, string name) =>
            element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

        private static int Int(JsonElement element, string name) =>
            element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetInt32() : 0;

        private static double? Double(JsonElement element, string name) =>
            element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetDouble() : null;
    }
}
