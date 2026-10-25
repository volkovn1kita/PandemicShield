using PandemicShield.Nextclade.Sorting;

namespace PandemicShield.Nextclade.Tests
{
    /// <summary>
    /// Автовибір набору даних на справжньому виводі <c>nextclade sort --all-matches</c> (Nextclade 3.24.0, індекс мінімізаторів v3).
    /// <c>mixed.fasta</c> — 2 геноми SARS-CoV-2, 2 HA грипу H3N2 і 1 RSV-A з прикладів наборів Nextstrain плюс рядок-«сміття».
    /// </summary>
    public class DatasetSortTests
    {
        private static readonly string[] Installed =
        [
            "nextstrain/flu/h1n1pdm/ha/CY121680", "nextstrain/flu/h3n2/ha/EPI1857216", "nextstrain/mpox/all-clades",
            "nextstrain/rsv/a/EPI_ISL_412866", "nextstrain/rsv/b/EPI_ISL_1653999", "nextstrain/sars-cov-2/wuhan-hu-1/orfs"
        ];

        private static string DataPath(string name) => Path.Combine(AppContext.BaseDirectory, "Data", "sort", name);

        private static List<SortMatch> Matches()
        {
            using var reader = new StreamReader(DataPath("mixed.sort-all.tsv"));
            return DatasetSorter.Read(reader);
        }

        [Fact]
        public void Best_match_is_chosen_among_installed_datasets_only()
        {
            var assignment = DatasetSorter.Assign(Matches(), Installed);

            // Найкращий збіг SARS-CoV-2 — nextstrain/sars-cov-2/BA.2.86, але його немає в образі: обирається wuhan-hu-1/orfs.
            Assert.Equal("nextstrain/sars-cov-2/wuhan-hu-1/orfs", assignment[0].Dataset);
            Assert.Equal("nextstrain/sars-cov-2/wuhan-hu-1/orfs", assignment[1].Dataset);
            Assert.Equal("nextstrain/flu/h3n2/ha/EPI1857216", assignment[2].Dataset);
            Assert.Equal("nextstrain/flu/h3n2/ha/EPI1857216", assignment[3].Dataset);
            Assert.Equal("nextstrain/rsv/a/EPI_ISL_412866", assignment[4].Dataset);
            Assert.False(assignment.ContainsKey(5));
        }

        [Fact]
        public void Unrecognised_sequences_have_no_rows()
        {
            Assert.DoesNotContain(Matches(), m => m.SeqName == "junk");
            // 3 + 3 збіги двох геномів SARS-CoV-2, 2 грипу, 1 RSV-A.
            Assert.Equal(9, Matches().Count);
        }

        [Fact]
        public void Splits_records_by_dataset_and_keeps_unassigned_names()
        {
            var assignment = DatasetSorter.Assign(Matches(), Installed);
            var outputs = new Dictionary<string, StringWriter>();

            using var input = new StreamReader(DataPath("mixed.fasta"));
            var result = FastaSplitter.Split(input, assignment, dataset => outputs[dataset] = new StringWriter());

            Assert.Equal(2, result.RecordsPerDataset["nextstrain/sars-cov-2/wuhan-hu-1/orfs"]);
            Assert.Equal(2, result.RecordsPerDataset["nextstrain/flu/h3n2/ha/EPI1857216"]);
            Assert.Equal(1, result.RecordsPerDataset["nextstrain/rsv/a/EPI_ISL_412866"]);
            Assert.Equal(["junk"], result.Unassigned);

            // Записи переносяться цілком (заголовок і всі рядки послідовності) і лише у свій файл.
            string flu = outputs["nextstrain/flu/h3n2/ha/EPI1857216"].ToString();
            Assert.Equal([">A/Hawaii/54/2026", ">A/Ohio/46/2026"], flu.Split('\n').Where(l => l.StartsWith('>')).Select(l => l.TrimEnd('\r')));
            Assert.DoesNotContain("TTTTTTTTTTTTTGGGG", string.Concat(outputs.Values.Select(w => w.ToString())));
            int inputLines = File.ReadLines(DataPath("mixed.fasta")).Count();
            int outputLines = outputs.Values.Sum(w => w.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
            Assert.Equal(inputLines - 2, outputLines);
        }

        [Fact]
        public void Nothing_installed_means_nothing_assigned()
        {
            Assert.Empty(DatasetSorter.Assign(Matches(), []));
        }
    }
}
