using Microsoft.EntityFrameworkCore;
using PandemicShield.Api.Extensions;
using PandemicShield.Contracts;
using PandemicShield.DataAccess.Data;
using PandemicShield.DataAccess.Entities;

namespace PandemicShield.Api.Endpoints
{
    /// <summary>
    /// Нагляд за антимікробною резистентністю на відкритих даних NCBI Pathogen Detection: які гени стійкості
    /// трапляються в ізолятах з обраної країни, за роками, регіонами й джерелами.
    /// </summary>
    public static class SurveillanceEndpoints
    {
        /// <summary>Групи Pathogen Detection, найважливіші для лікарняних інфекцій (ESKAPE) і харчових спалахів.</summary>
        public static readonly string[] Taxgroups =
        [
            "Klebsiella", "Escherichia_coli_Shigella", "Acinetobacter", "Pseudomonas_aeruginosa", "Enterobacter_hormaechei",
            "Staphylococcus_aureus", "Enterococcus_faecium", "Salmonella"
        ];

        public sealed record SyncRequest(string Taxgroup, string? Countries);

        public static void MapSurveillanceEndpoints(this IEndpointRouteBuilder app)
        {
            app.MapGet("/api/surveillance/taxgroups", () => Results.Ok(Taxgroups));

            app.MapGet("/api/surveillance/syncs", async (PandemicDbContext db) =>
                Results.Ok(await db.SurveillanceSyncs.OrderByDescending(s => s.CreatedAt).Take(20).ToListAsync()));

            app.MapPost("/api/surveillance/sync", async (SyncRequest request, PandemicDbContext db, JobPublisher jobs) =>
            {
                if (!Taxgroups.Contains(request.Taxgroup))
                    return Results.BadRequest($"Невідома група: {request.Taxgroup}. Доступні: {string.Join(", ", Taxgroups)}");

                var countries = (request.Countries ?? "Ukraine")
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (countries.Count is 0 or > 10)
                    return Results.BadRequest("Вкажіть від 1 до 10 країн англійською, як у NCBI: Ukraine, Poland.");

                if (await db.SurveillanceSyncs.AnyAsync(s => s.Taxgroup == request.Taxgroup &&
                                                           (s.Status == ImportStatus.Queued || s.Status == ImportStatus.Running)))
                    return Results.Conflict("Ця група вже оновлюється.");

                var sync = new SurveillanceSyncEntity(request.Taxgroup, string.Join(',', countries));
                db.SurveillanceSyncs.Add(sync);
                await db.SaveChangesAsync();

                await jobs.PublishAsync(JobPublisher.SurveillanceQueue, new SurveillanceJobMessage { SyncId = sync.Id });
                return Results.Accepted("/api/surveillance/syncs", sync);
            });

            app.MapGet("/api/surveillance/summary", async (string taxgroup, string? country, PandemicDbContext db) =>
            {
                var countries = await db.SurveillanceIsolates
                    .Where(i => i.Taxgroup == taxgroup)
                    .GroupBy(i => i.Country)
                    .Select(g => new { Name = g.Key, Isolates = g.Count() })
                    .OrderByDescending(c => c.Isolates)
                    .ToListAsync();
                // Фокус проєкту — Україна: якщо вона є серед завантажених країн, показуємо її першою.
                string? selected = country
                    ?? countries.FirstOrDefault(c => c.Name == "Ukraine")?.Name
                    ?? countries.FirstOrDefault()?.Name;

                var isolates = await db.SurveillanceIsolates
                    .Where(i => i.Taxgroup == taxgroup && i.Country == selected)
                    .ToListAsync();
                var ids = isolates.Select(i => i.Id).ToList();
                var genes = await db.SurveillanceGenes.Where(g => ids.Contains(g.IsolateId)).ToListAsync();
                var byIsolate = genes.ToLookup(g => g.IsolateId);
                var yearOf = isolates.ToDictionary(i => i.Id, i => i.CollectionYear);

                bool Has(SurveillanceIsolateEntity i, Func<SurveillanceGeneEntity, bool> predicate) => byIsolate[i.Id].Any(predicate);
                var years = isolates.Where(i => i.CollectionYear != null).Select(i => i.CollectionYear!.Value).ToList();

                // Родини генів: скільки ізолятів несуть хоча б один алель (blaOXA-48 і blaOXA-232 — одна родина).
                var families = genes
                    .Where(g => g.Qualifier != "POINT")
                    .GroupBy(g => g.Family)
                    .Select(f => new
                    {
                        Family = f.Key,
                        f.First().Class,
                        f.First().Subclass,
                        Isolates = f.Select(g => g.IsolateId).Distinct().Count(),
                        Carbapenemase = IsCarbapenemase(f.First()),
                        Years = f.Select(g => g.IsolateId).Distinct()
                            .Select(id => yearOf[id])
                            .Where(y => y != null)
                            .GroupBy(y => y!.Value)
                            .ToDictionary(y => y.Key.ToString(), y => y.Count())
                    })
                    .OrderByDescending(f => f.Isolates)
                    .ToList();

                return Results.Ok(new
                {
                    Taxgroup = taxgroup,
                    Country = selected,
                    Countries = countries,
                    Isolates = isolates.Count,
                    WithAssembly = isolates.Count(i => i.AssemblyAccession != null),
                    FirstYear = years.Count == 0 ? (int?)null : years.Min(),
                    LastYear = years.Count == 0 ? (int?)null : years.Max(),
                    PdgVersion = isolates.Select(i => i.PdgVersion).FirstOrDefault(),
                    AmrFinderVersions = isolates.Select(i => i.AmrFinderVersion).Where(v => v != null).Distinct().OrderBy(v => v).ToList(),
                    DatabaseVersions = isolates.Select(i => i.DatabaseVersion).Where(v => v != null).Distinct().OrderBy(v => v).ToList(),
                    CarbapenemaseIsolates = isolates.Count(i => Has(i, IsCarbapenemase)),
                    ColistinIsolates = isolates.Count(i => Has(i, g => HasSubclass(g, "COLISTIN"))),
                    CephalosporinaseIsolates = isolates.Count(i => Has(i, IsCephalosporinase)),
                    Years = years.Distinct().OrderBy(y => y).Select(y => new
                    {
                        Year = y,
                        Isolates = isolates.Count(i => i.CollectionYear == y),
                        Carbapenemase = isolates.Count(i => i.CollectionYear == y && Has(i, IsCarbapenemase))
                    }),
                    UnknownYear = isolates.Count(i => i.CollectionYear == null),
                    Carbapenemases = families.Where(f => f.Carbapenemase),
                    TopGenes = families.Where(f => !f.Carbapenemase && f.Class != null).Take(15),
                    Species = Top(isolates.Select(i => Species(i.Organism))),
                    Regions = Top(isolates.Select(i => Region(i.Region) ?? "—")),
                    Sources = Top(isolates.Select(i => i.IsolationSource ?? "—"))
                });
            });

            app.MapGet("/api/surveillance/isolates", async (string taxgroup, string country, string? family, int? year, PandemicDbContext db) =>
            {
                var query = db.SurveillanceIsolates.Where(i => i.Taxgroup == taxgroup && i.Country == country);
                if (year != null) query = query.Where(i => i.CollectionYear == year);
                if (!string.IsNullOrWhiteSpace(family))
                    query = query.Where(i => db.SurveillanceGenes.Any(g => g.IsolateId == i.Id && g.Family == family));

                var isolates = await query
                    .OrderByDescending(i => i.CollectionYear).ThenBy(i => i.TargetAccession)
                    .Take(1000)
                    .ToListAsync();
                var ids = isolates.Select(i => i.Id).ToList();
                var genes = (await db.SurveillanceGenes.Where(g => ids.Contains(g.IsolateId)).ToListAsync()).ToLookup(g => g.IsolateId);

                // Чи цей ізолят уже проаналізовано в PandemicShield (імпортом збірки з NCBI)?
                var assemblies = isolates.Select(i => i.AssemblyAccession).Where(a => a != null).ToList();
                var runs = await db.Runs
                    .Where(r => r.SourceAccession != null && assemblies.Contains(r.SourceAccession))
                    .OrderByDescending(r => r.StartedAt)
                    .Select(r => new { r.SourceAccession, r.Id })
                    .ToListAsync();
                var runByAssembly = runs.GroupBy(r => r.SourceAccession!).ToDictionary(g => g.Key, g => g.First().Id);

                return Results.Ok(isolates.Select(i => new
                {
                    i.Id,
                    i.TargetAccession,
                    i.AssemblyAccession,
                    i.BioSample,
                    Species = Species(i.Organism),
                    i.Strain,
                    Region = Region(i.Region),
                    i.CollectionDate,
                    i.CollectionYear,
                    i.IsolationSource,
                    i.Host,
                    i.AmrGeneCount,
                    Carbapenemases = genes[i.Id].Where(IsCarbapenemase).Select(g => g.Symbol).Distinct().ToList(),
                    Cephalosporinases = genes[i.Id].Where(IsCephalosporinase).Select(g => g.Symbol).Distinct().ToList(),
                    Colistin = genes[i.Id].Where(g => HasSubclass(g, "COLISTIN")).Select(g => g.Symbol).Distinct().ToList(),
                    RunId = i.AssemblyAccession != null && runByAssembly.TryGetValue(i.AssemblyAccession, out var runId) ? runId : (Guid?)null
                }));
            });
        }

        private static bool HasSubclass(SurveillanceGeneEntity gene, string subclass) => gene.Subclass?.Split('/').Contains(subclass) == true;

        // Фермент-карбапенемаза, а не мутація порину (ompK36_D135DGD теж має підклас CARBAPENEM).
        private static bool IsCarbapenemase(SurveillanceGeneEntity gene) => gene.Qualifier != "POINT" && HasSubclass(gene, "CARBAPENEM");

        // ESBL та інші β-лактамази, що руйнують цефалоспорини 3-го покоління (CTX-M тощо).
        private static bool IsCephalosporinase(SurveillanceGeneEntity gene) => gene.Class == "BETA-LACTAM" && HasSubclass(gene, "CEPHALOSPORIN");

        // "Klebsiella pneumoniae subsp. pneumoniae NTUH-K2044" → "Klebsiella pneumoniae".
        private static string Species(string? organism)
        {
            if (string.IsNullOrWhiteSpace(organism)) return "—";
            var words = organism.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return words.Length >= 2 ? $"{words[0]} {words[1]}" : organism;
        }

        // Лабораторії пишуть ті самі міста по-різному (Vinnitsa і Vinnytsia в одному наборі): зводимо до офіційної
        // транслітерації, інакше один регіон рахувався б двічі.
        private static readonly Dictionary<string, string> RegionAliases = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Vinnitsa"] = "Vinnytsia", ["Kiev"] = "Kyiv", ["Lvov"] = "Lviv", ["Kharkov"] = "Kharkiv", ["Odessa"] = "Odesa",
            ["Dnepr"] = "Dnipro", ["Dnipropetrovsk"] = "Dnipro", ["Zaporozhye"] = "Zaporizhzhia", ["Nikolaev"] = "Mykolaiv",
            ["Chernigov"] = "Chernihiv", ["Rovno"] = "Rivne", ["Ivano-Frankovsk"] = "Ivano-Frankivsk"
        };

        private static string? Region(string? region) =>
            region == null ? null : RegionAliases.TryGetValue(region.Trim(), out string? official) ? official : region.Trim();

        private static IEnumerable<object> Top(IEnumerable<string> values) =>
            values.GroupBy(v => v).Select(g => new { Name = g.Key, Isolates = g.Count() }).OrderByDescending(g => g.Isolates).Take(8);
    }
}
