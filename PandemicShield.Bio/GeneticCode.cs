namespace PandemicShield.Bio
{
    /// <summary>
    /// Таблиця генетичного коду NCBI (https://www.ncbi.nlm.nih.gov/Taxonomy/Utils/wprintgc.cgi).
    /// Таблиці записані так само, як у NCBI: 64 символи, порядок кодонів TTT, TTC, TTA, TTG, TCT, ... GGG.
    /// </summary>
    public sealed class GeneticCode
    {
        private const string Bases = "TCAG";

        // Індекс: (маска1 << 8) | (маска2 << 4) | маска3, де маска — набір можливих основ (Nucleotides.Mask).
        private readonly char[] _aminoAcid = new char[4096];
        private readonly bool[] _isStop = new bool[4096];
        private readonly bool[] _isStart = new bool[4096];
        private readonly bool[] _isAtg = new bool[4096];

        public int Id { get; }
        public string Name { get; }

        public static GeneticCode Standard { get; } = new(1, "Standard",
            "FFLLSSSSYY**CC*WLLLLPPPPHHQQRRRRIIIMTTTTNNKKSSRRVVVVAAAADDEEGGGG",
            "---M------**--*----M---------------M----------------------------");

        public static GeneticCode VertebrateMitochondrial { get; } = new(2, "Vertebrate Mitochondrial",
            "FFLLSSSSYY**CCWWLLLLPPPPHHQQRRRRIIMMTTTTNNKKSS**VVVVAAAADDEEGGGG",
            "----------**--------------------MMMM----------**---M------------");

        public static GeneticCode MycoplasmaSpiroplasma { get; } = new(4, "Mold, Protozoan, Coelenterate Mitochondrial and Mycoplasma/Spiroplasma",
            "FFLLSSSSYY**CCWWLLLLPPPPHHQQRRRRIIIMTTTTNNKKSSRRVVVVAAAADDEEGGGG",
            "--MM------**-------M------------MMMM---------------M------------");

        public static GeneticCode BacterialArchaealPlastid { get; } = new(11, "Bacterial, Archaeal and Plant Plastid",
            "FFLLSSSSYY**CC*WLLLLPPPPHHQQRRRRIIIMTTTTNNKKSSRRVVVVAAAADDEEGGGG",
            "---M------**--*----M------------MMMM---------------M------------");

        public static IReadOnlyList<GeneticCode> All { get; } =
            [Standard, VertebrateMitochondrial, MycoplasmaSpiroplasma, BacterialArchaealPlastid];

        public static GeneticCode FromId(int id) =>
            All.FirstOrDefault(c => c.Id == id)
            ?? throw new ArgumentOutOfRangeException(nameof(id), id, "Unsupported NCBI genetic code table.");

        private GeneticCode(int id, string name, string aminoAcids, string starts)
        {
            Id = id;
            Name = name;
            Build(aminoAcids, starts);
        }

        /// <summary>
        /// Амінокислота для кодону. Для неоднозначних кодонів (наприклад, GCN) повертає амінокислоту,
        /// якщо всі варіанти дають одну й ту саму, інакше 'X'.
        /// </summary>
        public char Translate(char b1, char b2, char b3) => _aminoAcid[Index(b1, b2, b3)];

        /// <summary>Кодон напевно є стоп-кодоном (усі його варіанти — стоп).</summary>
        public bool IsStop(char b1, char b2, char b3) => _isStop[Index(b1, b2, b3)];

        /// <summary>Кодон напевно є старт-кодоном цієї таблиці (ATG, а також альтернативні, як-от GTG чи TTG).</summary>
        public bool IsStart(char b1, char b2, char b3, bool allowAlternative) =>
            allowAlternative ? _isStart[Index(b1, b2, b3)] : _isAtg[Index(b1, b2, b3)];

        private static int Index(char b1, char b2, char b3) =>
            (Nucleotides.Mask(b1) << 8) | (Nucleotides.Mask(b2) << 4) | Nucleotides.Mask(b3);

        private void Build(string aminoAcids, string starts)
        {
            // Кожна з 4096 комбінацій масок розгортається в усі конкретні кодони.
            for (int m1 = 0; m1 < 16; m1++)
            for (int m2 = 0; m2 < 16; m2++)
            for (int m3 = 0; m3 < 16; m3++)
            {
                int index = (m1 << 8) | (m2 << 4) | m3;
                char aa = '\0';
                bool allStop = true, allStart = true, allAtg = true, any = false;

                for (int i = 0; i < 4; i++)
                {
                    if ((m1 & MaskOf(Bases[i])) == 0) continue;
                    for (int j = 0; j < 4; j++)
                    {
                        if ((m2 & MaskOf(Bases[j])) == 0) continue;
                        for (int k = 0; k < 4; k++)
                        {
                            if ((m3 & MaskOf(Bases[k])) == 0) continue;

                            int codon = i * 16 + j * 4 + k;
                            char current = aminoAcids[codon];
                            aa = !any ? current : (aa == current ? aa : 'X');
                            any = true;
                            allStop &= current == '*';
                            allStart &= starts[codon] == 'M';
                            allAtg &= Bases[i] == 'A' && Bases[j] == 'T' && Bases[k] == 'G';
                        }
                    }
                }

                _aminoAcid[index] = any ? aa : 'X';
                _isStop[index] = any && allStop;
                _isStart[index] = any && allStart;
                _isAtg[index] = any && allAtg;
            }
        }

        private static int MaskOf(char b) => Nucleotides.Mask(b);
    }
}
