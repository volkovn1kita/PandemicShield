using System.IO.Compression;
using System.Text;
using PandemicShield.Api.Extensions;
using PandemicShield.Api.Imports;

namespace PandemicShield.Api.Tests
{
    /// <summary>
    /// Імпорт з NCBI без мережі: розбір списку номерів, архівів NCBI Datasets (будуються в пам'яті за тією ж структурою,
    /// що й справжні архіви), заголовків FASTA і зіставлення виду з NCBI з видом AMRFinderPlus.
    /// </summary>
    public class ImportTests
    {
        [Fact]
        public void Parses_mixed_accession_list()
        {
            var parsed = NcbiAccessions.Parse("MN908947.3, oq291490.1\nGCA_009887825.1;GCF_000240185.1  NC_045512.2\tNZ_CP012345.1 MN908947.3");

            Assert.Equal(["GCA_009887825.1", "GCF_000240185.1"], parsed.Assemblies);
            Assert.Equal(["MN908947.3", "OQ291490.1", "NC_045512.2", "NZ_CP012345.1"], parsed.Nucleotides);
            Assert.Empty(parsed.Invalid);
            Assert.Equal(6, parsed.Total);
            // Порядок, у якому вставили, зберігається; повтор MN908947.3 прибрано.
            Assert.Equal(["MN908947.3", "OQ291490.1", "GCA_009887825.1", "GCF_000240185.1", "NC_045512.2", "NZ_CP012345.1"], parsed.All);
        }

        [Theory]
        [InlineData("GCA_12345")]
        [InlineData("hello")]
        [InlineData("S:E484K")]
        [InlineData("https://www.ncbi.nlm.nih.gov/nuccore/OQ291490.1")]
        public void Rejects_things_that_are_not_accessions(string text)
        {
            var parsed = NcbiAccessions.Parse(text);
            Assert.Equal(0, parsed.Total);
            Assert.Single(parsed.Invalid);
        }

        [Fact]
        public void Accession_version_is_ignored_when_comparing()
        {
            Assert.Equal("OQ291490", NcbiAccessions.WithoutVersion("OQ291490.1"));
            Assert.Equal("NC_045512", NcbiAccessions.WithoutVersion("NC_045512.2"));
            Assert.Equal("OQ291490", NcbiAccessions.WithoutVersion("OQ291490"));
        }

        [Fact]
        public void Reads_assembly_archive_with_organisms_and_skips_cds_and_rna()
        {
            using var zip = Archive(
                ("README.md", "NCBI Datasets"),
                ("ncbi_dataset/data/assembly_data_report.jsonl",
                    """{"accession":"GCA_009887825.1","organism":{"taxId":573,"organismName":"Klebsiella pneumoniae"}}""" + "\n" +
                    """{"accession":"GCF_000240185.1","organism":{"taxId":1125630,"organismName":"Klebsiella pneumoniae subsp. pneumoniae HS11286"}}"""),
                ("ncbi_dataset/data/GCA_009887825.1/GCA_009887825.1_ASM988782v1_genomic.fna", ">contig_1\nACGT\n"),
                ("ncbi_dataset/data/GCA_009887825.1/cds_from_genomic.fna", ">cds\nATG\n"),
                ("ncbi_dataset/data/GCF_000240185.1/GCF_000240185.1_ASM24018v2_genomic.fna", ">NC_016845.1\nACGT"),
                ("ncbi_dataset/data/GCF_000240185.1/rna_from_genomic.fna", ">rna\nACG\n"));

            var assemblies = NcbiArchiveReader.ReadAssemblies(zip);

            Assert.Equal(["GCA_009887825.1", "GCF_000240185.1"], assemblies.Select(a => a.Accession));
            Assert.Equal("Klebsiella pneumoniae", assemblies[0].OrganismName);
            Assert.Equal("Klebsiella pneumoniae subsp. pneumoniae HS11286", assemblies[1].OrganismName);
            Assert.All(assemblies, a => Assert.Single(a.FastaEntries));
            Assert.EndsWith("_genomic.fna", assemblies[0].FastaEntries[0].Name);
        }

        [Fact]
        public async Task Reads_virus_archive_and_concatenates_with_newlines()
        {
            using var zip = Archive(
                ("ncbi_dataset/data/data_report.jsonl", "{}"),
                ("ncbi_dataset/data/genomic.fna", ">MN908947.3 SARS-CoV-2\nACGT"),
                ("ncbi_dataset/extra/genomic.fna", ">OQ291490.1 SARS-CoV-2\nTTGA\n"));

            var entries = NcbiArchiveReader.GenomicEntries(zip);
            Assert.Equal(2, entries.Count);
            Assert.Empty(NcbiArchiveReader.ReadAssemblies(zip));

            using var output = new MemoryStream();
            await NcbiArchiveReader.ConcatenateAsync(entries, output, CancellationToken.None);
            string fasta = Encoding.UTF8.GetString(output.ToArray());

            // Перший файл не закінчувався переносом рядка — без нього другий заголовок приклеївся б до послідовності.
            Assert.Equal(">MN908947.3 SARS-CoV-2\nACGT\n>OQ291490.1 SARS-CoV-2\nTTGA\n", fasta);
            Assert.Equal(["MN908947.3", "OQ291490.1"], FastaHeaders.Read(new StringReader(fasta)).Select(h => h.Id));
        }

        [Fact]
        public void Fasta_headers_give_ids_descriptions_and_run_names()
        {
            var headers = FastaHeaders.Read(new StringReader(
                ">CP003200.1 Klebsiella pneumoniae subsp. pneumoniae HS11286, complete genome\nACGT\n>noDescription\nAC\n"));

            Assert.Equal("CP003200.1", headers[0].Id);
            Assert.Equal("Klebsiella pneumoniae", FastaHeaders.Organism(headers[0].Description));
            Assert.Equal(("noDescription", ""), (headers[1].Id, headers[1].Description));
            Assert.Null(FastaHeaders.Organism(headers[1].Description));

            Assert.Equal("NCBI: A1.1", FastaHeaders.RunName(["A1.1"]));
            Assert.Equal("NCBI: A1.1, B2.1 +3", FastaHeaders.RunName(["A1.1", "B2.1", "C3.1", "D4.1", "E5.1"]));
        }

        [Theory]
        [InlineData("Klebsiella pneumoniae subsp. pneumoniae HS11286", "Klebsiella_pneumoniae")]
        [InlineData("Klebsiella pneumoniae", "Klebsiella_pneumoniae")]
        [InlineData("Klebsiella oxytoca", "Klebsiella_oxytoca")]
        [InlineData("Escherichia coli O157:H7", "Escherichia")]
        [InlineData("Shigella flexneri 2a", "Escherichia")]
        [InlineData("Acinetobacter baumannii ATCC 17978", "Acinetobacter_baumannii")]
        [InlineData("Salmonella enterica subsp. enterica serovar Typhimurium", "Salmonella")]
        [InlineData("Klebsiella variicola", null)]
        [InlineData("Enterobacter hormaechei", null)]
        [InlineData(null, null)]
        public void Maps_ncbi_organism_to_amrfinder_organism(string? scientificName, string? expected)
        {
            Assert.Equal(expected, new AnalysisOptions().MatchOrganism(scientificName));
        }

        private static ZipArchive Archive(params (string Path, string Content)[] files)
        {
            var stream = new MemoryStream();
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var (path, content) in files)
                {
                    using var writer = new StreamWriter(zip.CreateEntry(path).Open());
                    writer.Write(content);
                }
            }
            stream.Position = 0;
            return new ZipArchive(stream, ZipArchiveMode.Read);
        }
    }
}
