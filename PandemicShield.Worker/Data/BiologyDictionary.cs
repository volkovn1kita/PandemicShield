using System;
using System.Collections.Generic;
using System.Text;

namespace PandemicShield.Worker.Data
{
    public static class BiologyDictionary
    {
        // Словник трансляції кодонів ДНК в амінокислоти
        public static readonly Dictionary<string, char> CodonTable = new Dictionary<string, char>
        {
            {"ATT", 'I'}, {"ATC", 'I'}, {"ATA", 'I'}, // Isoleucine
            {"CTT", 'L'}, {"CTC", 'L'}, {"CTA", 'L'}, {"CTG", 'L'}, {"TTA", 'L'}, {"TTG", 'L'}, // Leucine
            {"GTT", 'V'}, {"GTC", 'V'}, {"GTA", 'V'}, {"GTG", 'V'}, // Valine
            {"TTT", 'F'}, {"TTC", 'F'}, // Phenylalanine
            {"ATG", 'M'}, // Methionine (START)
            {"TGT", 'C'}, {"TGC", 'C'}, // Cysteine
            {"GCT", 'A'}, {"GCC", 'A'}, {"GCA", 'A'}, {"GCG", 'A'}, // Alanine
            {"GGT", 'G'}, {"GGC", 'G'}, {"GGA", 'G'}, {"GGG", 'G'}, // Glycine
            {"CCT", 'P'}, {"CCC", 'P'}, {"CCA", 'P'}, {"CCG", 'P'}, // Proline
            {"ACT", 'T'}, {"ACC", 'T'}, {"ACA", 'T'}, {"ACG", 'T'}, // Threonine
            {"TCT", 'S'}, {"TCC", 'S'}, {"TCA", 'S'}, {"TCG", 'S'}, {"AGT", 'S'}, {"AGC", 'S'}, // Serine
            {"TAT", 'Y'}, {"TAC", 'Y'}, // Tyrosine
            {"TGG", 'W'}, // Tryptophan
            {"CAA", 'Q'}, {"CAG", 'Q'}, // Glutamine
            {"AAT", 'N'}, {"AAC", 'N'}, // Asparagine
            {"CAT", 'H'}, {"CAC", 'H'}, // Histidine
            {"GAA", 'E'}, {"GAG", 'E'}, // Glutamic acid
            {"GAT", 'D'}, {"GAC", 'D'}, // Aspartic acid
            {"AAA", 'K'}, {"AAG", 'K'}, // Lysine
            {"CGT", 'R'}, {"CGC", 'R'}, {"CGA", 'R'}, {"CGG", 'R'}, {"AGA", 'R'}, {"AGG", 'R'}, // Arginine
            {"TAA", '*'}, {"TAG", '*'}, {"TGA", '*'}  // STOP кодони
        };
    }
}
