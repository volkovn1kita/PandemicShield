using System;
using System.Collections.Generic;
using System.Text;

namespace PandemicShield.Worker.Services
{
    public class TranslationService
    {
        public static List<string> FindProteins(string dna)
        {
            List<string> proteins = new List<string>();
            int i = 0;
            while (i + 3 <= dna.Length)
            {
                string codon = dna.Substring(i, 3);

                BiologyDictionary.CodonTable.TryGetValue(codon, out char aminoAcid);

                if (aminoAcid == 'M')
                {

                    StringBuilder stringBuilder = new StringBuilder();
                    stringBuilder.Append(aminoAcid);
                    i += 3;
                    while (i + 3 <= dna.Length)
                    {
                        string codonInProtein = dna.Substring(i, 3);

                        if (!BiologyDictionary.CodonTable.TryGetValue(codonInProtein, out char aminoAcidInProtein))
                        {
                            stringBuilder.Append('?');
                            i += 3;
                            continue;
                        }

                        stringBuilder.Append(aminoAcidInProtein);
                        i += 3;
                        if (aminoAcidInProtein == '*')
                        {
                            proteins.Add(stringBuilder.ToString());
                            break;
                        }
                    }

                }
                else
                {
                    i++;
                }
            }

            return proteins;
        }
    }
}
