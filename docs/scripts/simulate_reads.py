#!/usr/bin/env python3
"""Симулює прочитання секвенатора з відомого геному, щоб перевірити конвеєр FASTQ без справжнього секвенування.

Illumina: парні прочитання 2×150 з фрагментів ~350 нт, ~0,3% замін, якість як у NovaSeq (біни Q37/Q25).
Nanopore: довгі прочитання (логнормальний розподіл довжин, медіана ~8 тис. нт), ~5% помилок (заміни, вставки, делеції).
Прочитання беруться з обох ланцюгів; генератор детермінований (--seed).

Приклади:
  python3 simulate_reads.py genome.fasta out --platform illumina --depth 200
      → out_R1.fastq.gz, out_R2.fastq.gz
  python3 simulate_reads.py genome.fasta out --platform nanopore --depth 30
      → out.fastq.gz
Консенсус з таких прочитань має відтворити вихідний геном, а збірка — його гени (і тип штаму MLST).
"""
import argparse
import gzip
import math
import random
import sys

# Консоль Windows (cp1251) не друкує «×» і «→»: виводимо UTF-8 незалежно від консолі.
sys.stdout.reconfigure(encoding="utf-8")

COMPLEMENT = str.maketrans("ACGTN", "TGCAN")


def read_fasta(path):
    records, name, seq = [], None, []
    with open(path) as f:
        for line in f:
            line = line.strip()
            if line.startswith(">"):
                if name:
                    records.append((name, "".join(seq).upper()))
                name, seq = line[1:].split()[0], []
            elif line:
                seq.append(line)
    if name:
        records.append((name, "".join(seq).upper()))
    return records


def revcomp(s):
    return s.translate(COMPLEMENT)[::-1]


def mutate_illumina(rng, s, rate):
    out = list(s)
    for i in range(len(out)):
        if rng.random() < rate:
            out[i] = rng.choice([b for b in "ACGT" if b != out[i]])
    return "".join(out)


def mutate_nanopore(rng, s, rate):
    out = []
    for base in s:
        r = rng.random()
        if r < rate * 0.4:            # заміна
            out.append(rng.choice([b for b in "ACGT" if b != base]))
        elif r < rate * 0.7:          # делеція
            continue
        elif r < rate:                # вставка
            out.append(base)
            out.append(rng.choice("ACGT"))
        else:
            out.append(base)
    return "".join(out)


def pick(rng, records, weights, length):
    """Випадкова ділянка довжиною length з випадкового запису (пропорційно довжині), з будь-якого ланцюга."""
    name, seq, offset = rng.choices(records, weights=weights)[0]
    length = min(length, len(seq))
    start = rng.randrange(0, len(seq) - length + 1)
    fragment = seq[start:start + length]
    return name, offset + start, (fragment if rng.random() < 0.5 else revcomp(fragment))


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("genome")
    parser.add_argument("prefix")
    parser.add_argument("--platform", choices=["illumina", "nanopore"], default="illumina")
    parser.add_argument("--depth", type=float, default=50)
    parser.add_argument("--seed", type=int, default=1)
    args = parser.parse_args()

    rng = random.Random(args.seed)
    # Невизначені нуклеотиди (N) не можна просто викинути: це зсунуло б геном і створило штучні делеції.
    # Прочитання беруться лише з ділянок без N — як у справжньому секвенуванні, де там немає покриття.
    records = []
    for name, seq in read_fasta(args.genome):
        offset = 0
        for segment in seq.split("N"):
            if len(segment) >= 200:
                records.append((name, segment, offset))
            offset += len(segment) + 1
    genome_length = sum(len(s) for _, s, _ in records)
    weights = [len(s) for _, s, _ in records]

    if args.platform == "illumina":
        read_length, fragment = 150, 350
        pairs = int(args.depth * genome_length / (2 * read_length))
        with gzip.open(f"{args.prefix}_R1.fastq.gz", "wt") as r1, gzip.open(f"{args.prefix}_R2.fastq.gz", "wt") as r2:
            for i in range(pairs):
                name, start, frag = pick(rng, records, weights, max(read_length, int(rng.gauss(fragment, 30))))
                a = mutate_illumina(rng, frag[:read_length], 0.003)
                b = mutate_illumina(rng, revcomp(frag)[:read_length], 0.003)
                for out, seq, mate in ((r1, a, 1), (r2, b, 2)):
                    # Якість як у NovaSeq: біни F (Q37) і зрідка «:» (Q25). Сирі Q30–Q38 давали б «@» (Q31) на початку
                    # рядка якості — а fastp, ділячи файл на частини, шукає межі записів саме за «@».
                    quality = "".join("F" if rng.random() < 0.95 else ":" for _ in seq)
                    out.write(f"@sim{i}/{mate} {name}:{start}\n{seq}\n+\n{quality}\n")
        print(f"{pairs} пар прочитань 2×{read_length} ({args.depth}×) → {args.prefix}_R1/R2.fastq.gz")
    else:
        total, i = 0, 0
        target = args.depth * genome_length
        with gzip.open(f"{args.prefix}.fastq.gz", "wt") as out:
            while total < target:
                length = max(500, int(rng.lognormvariate(math.log(8000), 0.6)))
                name, start, frag = pick(rng, records, weights, length)
                seq = mutate_nanopore(rng, frag, 0.05)
                # Q12–Q20 («-»…«5»): без «+» (Q10) на початку рядка якості, з тієї ж причини, що й «@» вище.
                quality = "".join(chr(33 + rng.randint(12, 20)) for _ in seq)
                out.write(f"@ont{i} {name}:{start}\n{seq}\n+\n{quality}\n")
                total += len(seq)
                i += 1
        print(f"{i} довгих прочитань, {total} нт ({args.depth}×) → {args.prefix}.fastq.gz")


if __name__ == "__main__":
    main()
