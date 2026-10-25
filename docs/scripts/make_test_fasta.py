"""
Генерує тестовий multi-FASTA для перевірки PandemicShield наскрізно.

  python3 docs/scripts/make_test_fasta.py > mutant.fasta

Записи:
  mutant_fwd - референс SARS-CoV-2 (MN908947) з мутаціями Spike D614G (A23403G)
               і Delta E156G + del157-158; рядки по 70 символів, переноси CRLF;
  mutant_rc  - той самий геном як зворотний комплемент, малими літерами
               (маркери мають знайтися на ланцюзі '-').

Очікувані знахідки (категорія Virus), координати 1-based:
  mutant_fwd  +  21989-22054  Delta (aa 152 в ORF)
  mutant_fwd  +  23375-23419  D614G (aa 614 в ORF)
  mutant_rc   -   6479-6523   D614G
  mutant_rc   -   7844-7909   Delta
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REFERENCE = os.path.join(HERE, "..", "..", "PandemicShield.Bio.Tests", "Data", "MN908947.fasta")

genome = "".join(line.strip() for line in open(REFERENCE) if not line.startswith(">"))
assert len(genome) == 29903

g = list(genome)
assert g[23402] == "A"
g[23402] = "G"                                   # A23403G: GAT -> GGT, Spike D614G
genome = "".join(g)

codon156 = 21562 + 155 * 3                       # Spike починається з 21563 (1-based)
assert genome[codon156:codon156 + 9] == "GAGTTCAGA"  # E156, F157, R158
genome = genome[:codon156] + "GGA" + genome[codon156 + 9:]

complement = {"A": "T", "C": "G", "G": "C", "T": "A"}
reverse = "".join(complement[b] for b in reversed(genome)).lower()


def wrap(sequence, width=70):
    return "\r\n".join(sequence[i:i + width] for i in range(0, len(sequence), width))


sys.stdout.buffer.write((
    ">mutant_fwd SARS-CoV-2 MN908947 with Spike D614G and Delta E156G/del157-158\r\n" + wrap(genome) + "\r\n" +
    ">mutant_rc reverse complement of mutant_fwd, lowercase\r\n" + wrap(reverse) + "\r\n"
).encode("ascii"))
