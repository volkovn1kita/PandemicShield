using System.IO.Compression;
using System.Text.Json;

namespace PandemicShield.Api.Imports
{
    /// <summary>
    /// Читає архіви NCBI Datasets (те, що дає кнопка Download на сайті NCBI або <c>datasets download</c>):
    /// <list type="bullet">
    /// <item>збірки: <c>ncbi_dataset/data/GCA_…/GCA_…_genomic.fna</c> і <c>assembly_data_report.jsonl</c> з видом;</item>
    /// <item>віруси: <c>ncbi_dataset/data/genomic.fna</c> з усіма геномами в одному файлі.</item>
    /// </list>
    /// </summary>
    public static class NcbiArchiveReader
    {
        public sealed record Assembly(string Accession, string? OrganismName, IReadOnlyList<ZipArchiveEntry> FastaEntries);

        /// <summary>Збірки в архіві: по одній на теку GCA_/GCF_, з видом зі звіту, якщо він є.</summary>
        public static List<Assembly> ReadAssemblies(ZipArchive zip)
        {
            var organisms = ReadOrganisms(zip);
            return zip.Entries
                .Where(IsGenomicFasta)
                .Select(e => (Entry: e, Accession: ParentDirectory(e.FullName)))
                .Where(x => x.Accession != null && NcbiAccessions.IsAssembly(x.Accession))
                .GroupBy(x => x.Accession!, StringComparer.Ordinal)
                .OrderBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => new Assembly(g.Key, organisms.GetValueOrDefault(g.Key), g.Select(x => x.Entry).ToList()))
                .ToList();
        }

        /// <summary>Усі файли з нуклеотидними послідовностями геномів (без CDS і РНК).</summary>
        public static List<ZipArchiveEntry> GenomicEntries(ZipArchive zip) =>
            zip.Entries.Where(IsGenomicFasta).OrderBy(e => e.FullName, StringComparer.Ordinal).ToList();

        /// <summary>Вид кожної збірки з <c>assembly_data_report.jsonl</c> (рядок JSON на збірку).</summary>
        public static Dictionary<string, string> ReadOrganisms(ZipArchive zip)
        {
            var organisms = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var entry in zip.Entries.Where(e => e.Name == "assembly_data_report.jsonl"))
            {
                using var reader = new StreamReader(entry.Open());
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    using var json = JsonDocument.Parse(line);
                    var root = json.RootElement;
                    if (root.TryGetProperty("accession", out var accession) &&
                        root.TryGetProperty("organism", out var organism) &&
                        organism.TryGetProperty("organismName", out var name) &&
                        accession.GetString() is { } acc && name.GetString() is { } organismName)
                    {
                        organisms[acc] = organismName;
                    }
                }
            }
            return organisms;
        }

        /// <summary>Записує кілька FASTA-файлів архіву в один потік, гарантуючи перенос рядка між ними.</summary>
        public static async Task<long> ConcatenateAsync(IEnumerable<ZipArchiveEntry> entries, Stream output, CancellationToken cancellationToken)
        {
            long bytes = 0;
            byte[] buffer = new byte[81920];
            foreach (var entry in entries)
            {
                await using var input = entry.Open();
                int read;
                byte last = (byte)'\n';
                while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    last = buffer[read - 1];
                    bytes += read;
                }
                if (last != (byte)'\n') await output.WriteAsync("\n"u8.ToArray(), cancellationToken);
            }
            return bytes;
        }

        // Геномна послідовність — це genomic.fna (віруси) або *_genomic.fna (збірки),
        // але не cds_from_genomic.fna чи rna_from_genomic.fna (білок-кодувальні ділянки й РНК окремо).
        private static bool IsGenomicFasta(ZipArchiveEntry entry)
        {
            string name = entry.Name;
            if (name.StartsWith("cds_from_", StringComparison.Ordinal) || name.StartsWith("rna_from_", StringComparison.Ordinal)) return false;
            return name == "genomic.fna" || name.EndsWith("_genomic.fna", StringComparison.Ordinal);
        }

        private static string? ParentDirectory(string fullName)
        {
            string[] parts = fullName.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length >= 2 ? parts[^2] : null;
        }
    }
}
