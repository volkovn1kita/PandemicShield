using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PandemicShield.Api.Explorer
{
    public sealed record StructureHit(
        string PdbId, string Entity, string? Title, double Identity, double Evalue, int QueryStart, int QueryEnd,
        string ImageUrl, string ViewerUrl);

    public sealed record AlphaFoldModel(string Accession, string? Description, string? Organism, double? Plddt, string Url, string? PaeImageUrl);

    /// <summary>
    /// Пошук 3D-структур для білка ORF (онлайн, єдиний крок Explorer, якому потрібен інтернет):
    /// <list type="bullet">
    /// <item>RCSB PDB — пошук за послідовністю (MMseqs2), експериментальні структури;</item>
    /// <item>AlphaFold DB — передбачена модель для білка UniProt, на який вказує найкращий збіг PDB.
    /// Використовується нова схема API (modelEntityId, globalMetricValue; старі поля вимкнено у 2026 р.).</item>
    /// </list>
    /// </summary>
    public sealed class StructureSearch
    {
        private readonly HttpClient _http;

        public StructureSearch(HttpClient http) => _http = http;

        public async Task<(List<StructureHit> Hits, AlphaFoldModel? AlphaFold)> SearchAsync(string protein, CancellationToken cancellationToken)
        {
            var query = new JsonObject
            {
                ["query"] = new JsonObject
                {
                    ["type"] = "terminal",
                    ["service"] = "sequence",
                    ["parameters"] = new JsonObject
                    {
                        ["evalue_cutoff"] = 1e-5,
                        ["identity_cutoff"] = 0.3,
                        ["sequence_type"] = "protein",
                        ["value"] = protein
                    }
                },
                ["request_options"] = new JsonObject
                {
                    ["scoring_strategy"] = "sequence",
                    ["results_verbosity"] = "verbose",
                    ["paginate"] = new JsonObject { ["start"] = 0, ["rows"] = 6 }
                },
                ["return_type"] = "polymer_entity"
            };

            using var response = await _http.PostAsJsonAsync("https://search.rcsb.org/rcsbsearch/v2/query", query, cancellationToken);
            // 204 — нічого не знайдено.
            if (response.StatusCode == System.Net.HttpStatusCode.NoContent) return ([], null);
            response.EnsureSuccessStatusCode();
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));

            var hits = new List<StructureHit>();
            foreach (var result in json.RootElement.GetProperty("result_set").EnumerateArray())
            {
                string identifier = result.GetProperty("identifier").GetString()!;    // 6VXX_1
                string pdb = identifier.Split('_')[0], entity = identifier.Split('_')[1];
                var context = result.GetProperty("services")[0].GetProperty("nodes")[0].GetProperty("match_context")[0];
                hits.Add(new StructureHit(pdb, entity, null,
                    context.GetProperty("sequence_identity").GetDouble(),
                    context.GetProperty("evalue").GetDouble(),
                    context.GetProperty("query_beg").GetInt32(),
                    context.GetProperty("query_end").GetInt32(),
                    $"https://cdn.rcsb.org/images/structures/{pdb.ToLowerInvariant()}_assembly-1.jpeg",
                    $"https://www.rcsb.org/3d-view/{pdb}"));
            }

            // Назви й UniProt — з Data API RCSB (паралельно для всіх збігів).
            var details = await Task.WhenAll(hits.Select(h => EntityAsync(h.PdbId, h.Entity, cancellationToken)));
            hits = hits.Select((h, i) => h with { Title = details[i].Title }).ToList();

            string? uniprot = details.Select(d => d.Uniprot).FirstOrDefault(u => u != null);
            AlphaFoldModel? alphaFold = uniprot == null ? null : await AlphaFoldAsync(uniprot, cancellationToken);
            return (hits, alphaFold);
        }

        private async Task<(string? Title, string? Uniprot)> EntityAsync(string pdb, string entity, CancellationToken cancellationToken)
        {
            try
            {
                using var json = JsonDocument.Parse(await _http.GetStringAsync($"https://data.rcsb.org/rest/v1/core/polymer_entity/{pdb}/{entity}", cancellationToken));
                var root = json.RootElement;
                string? title = root.TryGetProperty("rcsb_polymer_entity", out var e) && e.TryGetProperty("pdbx_description", out var d) ? d.GetString() : null;
                string? uniprot = root.TryGetProperty("rcsb_polymer_entity_container_identifiers", out var ids) &&
                                  ids.TryGetProperty("uniprot_ids", out var u) && u.GetArrayLength() > 0
                    ? u[0].GetString()
                    : null;
                return (title, uniprot);
            }
            catch (HttpRequestException)
            {
                return (null, null);
            }
        }

        private async Task<AlphaFoldModel?> AlphaFoldAsync(string uniprot, CancellationToken cancellationToken)
        {
            try
            {
                using var response = await _http.GetAsync($"https://alphafold.ebi.ac.uk/api/prediction/{Uri.EscapeDataString(uniprot)}", cancellationToken);
                if (!response.IsSuccessStatusCode) return null;
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
                if (json.RootElement.GetArrayLength() == 0) return null;
                var model = json.RootElement[0];
                string? Text(string name) => model.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
                double? plddt = model.TryGetProperty("globalMetricValue", out var g) && g.ValueKind == JsonValueKind.Number ? g.GetDouble() : null;
                return new AlphaFoldModel(uniprot, Text("uniprotDescription"), Text("organismScientificName"), plddt,
                    $"https://alphafold.ebi.ac.uk/entry/{uniprot}", Text("paeImageUrl"));
            }
            catch (HttpRequestException)
            {
                return null;
            }
        }
    }
}
