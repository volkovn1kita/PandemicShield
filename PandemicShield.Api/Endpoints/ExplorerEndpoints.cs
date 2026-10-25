using Microsoft.AspNetCore.Mvc;
using PandemicShield.Api.Explorer;
using PandemicShield.Bio;
using PandemicShield.Bio.Alignment;
using PandemicShield.Bio.Formats;

namespace PandemicShield.Api.Endpoints
{
    /// <summary>
    /// Explorer — навчальний режим «від ДНК до білка»: відкрити FASTA/GenBank, карта ORF у 6 рамках поруч з анотованими генами,
    /// білок, порівняння двох послідовностей, 3D-структура. Нічого не зберігається в БД.
    /// </summary>
    public static class ExplorerEndpoints
    {
        public sealed record CompareRequest(Guid? Token, int? RecordA, int? RecordB, string? SequenceA, string? SequenceB, bool Protein);

        public sealed record StructureRequest(string Protein);

        public static void MapExplorerEndpoints(this IEndpointRouteBuilder app)
        {
            app.MapPost("/api/explorer/files", async ([FromForm(Name = "file")] IFormFile? file, [FromForm] string? text, ExplorerService explorer) =>
            {
                if (file == null && string.IsNullOrWhiteSpace(text)) return Results.BadRequest("Оберіть файл FASTA чи GenBank або вставте послідовність.");
                if (file != null && file.Length > ExplorerService.MaxFileBytes)
                    return Results.BadRequest($"Файл більший за {ExplorerService.MaxFileBytes / 1024 / 1024} МБ: Explorer — для одного геному чи плазміди.");

                try
                {
                    using var reader = file != null ? new StreamReader(file.OpenReadStream()) : new StreamReader(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(Pasted(text!))));
                    var (token, records) = explorer.Open(reader);
                    return Results.Ok(new
                    {
                        Token = token,
                        FileName = file?.FileName,
                        Records = records.Select((r, i) => new
                        {
                            Index = i,
                            r.Id,
                            r.Description,
                            Length = r.Sequence.Length,
                            r.Circular,
                            Gc = Math.Round(r.GcPercent, 1),
                            Features = r.Features
                                .Where(f => f.Type != "gene" && f.Segments.Count > 0)
                                .Select(f => new
                                {
                                    f.Type,
                                    f.Label,
                                    f.Gene,
                                    f.Product,
                                    Strand = f.Strand == Strand.Forward ? '+' : '-',
                                    f.Start,
                                    f.End,
                                    Segments = f.Segments.Select(s => new[] { s.Start, s.End }),
                                    f.Partial
                                })
                        })
                    });
                }
                catch (FormatException ex)
                {
                    return Results.BadRequest(ex.Message);
                }
            })
            .DisableAntiforgery();

            app.MapGet("/api/explorer/{token:guid}/{index:int}/orfs", (Guid token, int index, int? minLength, int? code, bool? altStarts, bool? circular,
                ExplorerService explorer) =>
            {
                var record = explorer.Record(token, index);
                if (record == null) return Results.NotFound("Сесію Explorer не знайдено: відкрийте файл ще раз.");

                bool isCircular = circular ?? record.Circular;
                var options = new OrfFinderOptions
                {
                    MinNucleotideLength = Math.Max(30, minLength ?? 75),
                    GeneticCode = GeneticCode.FromId(code ?? 1),
                    AllowAlternativeStarts = altStarts ?? false
                };
                var (orfs, truncated) = ExplorerService.Orfs(record, options, isCircular);
                return Results.Ok(new { Length = record.Sequence.Length, Circular = isCircular, Truncated = truncated, Orfs = orfs });
            });

            // Ділянка: "21563-25384" або "266-13468,13468-21555" (join), ланцюг + чи −.
            app.MapGet("/api/explorer/{token:guid}/{index:int}/region", (Guid token, int index, string segments, string? strand, int? code,
                ExplorerService explorer) =>
            {
                var record = explorer.Record(token, index);
                if (record == null) return Results.NotFound("Сесію Explorer не знайдено: відкрийте файл ще раз.");

                var parsed = new List<Segment>();
                foreach (string part in segments.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    string[] ends = part.Split('-');
                    if (ends.Length != 2 || !long.TryParse(ends[0], out long s) || !long.TryParse(ends[1], out long e) || s < 1 || e < s ||
                        e - s > 2_000_000)
                        return Results.BadRequest($"Некоректна ділянка «{part}».");
                    parsed.Add(new Segment(s, e));
                }
                var (nucleotides, protein) = ExplorerService.Region(record, parsed, strand == "-" ? Strand.Reverse : Strand.Forward,
                    GeneticCode.FromId(code ?? 1));
                return Results.Ok(new { Nucleotides = nucleotides, Protein = protein });
            });

            // Порівняння: два записи відкритого файлу (нуклеотиди) або дві послідовності (нуклеотиди чи білки).
            app.MapPost("/api/explorer/compare", (CompareRequest request, ExplorerService explorer) =>
            {
                string? a = request.SequenceA, b = request.SequenceB;
                if (request.Token is Guid token && request.RecordA is int ia && request.RecordB is int ib)
                {
                    a = explorer.Record(token, ia)?.Sequence;
                    b = explorer.Record(token, ib)?.Sequence;
                    if (a == null || b == null) return Results.NotFound("Сесію Explorer не знайдено: відкрийте файл ще раз.");
                }
                if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return Results.BadRequest("Потрібні дві послідовності.");

                try
                {
                    var alignment = ExplorerService.Compare(Clean(a), Clean(b), request.Protein);
                    return Results.Ok(new
                    {
                        LengthA = a.Length,
                        LengthB = b.Length,
                        alignment.Identity,
                        alignment.Matches,
                        alignment.Mismatches,
                        alignment.Unknown,
                        alignment.AlignedColumns,
                        Substitutions = alignment.Differences.Count(d => d.Kind == DifferenceKind.Substitution),
                        Insertions = alignment.Differences.Count(d => d.Kind == DifferenceKind.Insertion),
                        Deletions = alignment.Differences.Count(d => d.Kind == DifferenceKind.Deletion),
                        Differences = alignment.Differences.Take(2000),
                        // Вирівняні рядки — для білків і коротких ділянок (для геномів їх ніхто не читатиме посимвольно).
                        AlignedA = alignment.AlignedA.Length <= 20_000 ? alignment.AlignedA : null,
                        AlignedB = alignment.AlignedB.Length <= 20_000 ? alignment.AlignedB : null
                    });
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(ex.Message);
                }
            });

            app.MapPost("/api/explorer/structures", async (StructureRequest request, StructureSearch search, CancellationToken cancellationToken) =>
            {
                string protein = new string(request.Protein.Where(char.IsLetter).Select(char.ToUpperInvariant).ToArray());
                if (protein.Length < 20) return Results.BadRequest("Для пошуку структури потрібен білок щонайменше з 20 амінокислот.");
                if (protein.Length > 5000) return Results.BadRequest("Білок задовгий для пошуку (понад 5000 амінокислот).");
                try
                {
                    var (hits, alphaFold) = await search.SearchAsync(protein, cancellationToken);
                    return Results.Ok(new { Hits = hits, AlphaFold = alphaFold });
                }
                catch (HttpRequestException ex)
                {
                    return Results.Problem($"RCSB PDB недоступний (потрібен інтернет): {ex.Message}", statusCode: 502);
                }
            });
        }

        // Вставлений текст без заголовка «>» — одна послідовність без назви.
        private static string Pasted(string text)
        {
            string trimmed = text.TrimStart();
            return trimmed.StartsWith('>') || trimmed.StartsWith("LOCUS", StringComparison.Ordinal) ? trimmed : $">pasted\n{trimmed}";
        }

        private static string Clean(string sequence) =>
            new string(sequence.Where(char.IsLetter).Select(c => char.ToUpperInvariant(c) == 'U' ? 'T' : char.ToUpperInvariant(c)).ToArray());
    }
}
