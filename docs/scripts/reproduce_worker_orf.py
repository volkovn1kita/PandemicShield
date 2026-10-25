"""
Відтворення поведінки PandemicShield.Worker (TranslationService.FindProteins)
на справжньому референсному геномі SARS-CoV-2 (Wuhan-Hu-1, MN908947).

Запуск:
  curl -sSfo ref.fasta https://raw.githubusercontent.com/nextstrain/ncov/master/defaults/reference_seq.fasta
  python3 reproduce_worker_orf.py ref.fasta

Скрипт порівнює два режими:
  RAW   - файл як є (з заголовком '>' і переносами рядків) - саме так його
          надсилає Parser у RabbitMQ;
  CLEAN - лише нуклеотиди.
І перевіряє маркери з DbSeeder.cs проти правильно транслюваного білка Spike.
"""
import sys
bases="TCAG"; aa="FFLLSSSSYY**CC*WLLLLPPPPHHQQRRRRIIIMTTTTNNKKSSRRVVVVAAAADDEEGGGG"
TABLE={a+b+c:aa[16*i+4*j+k] for i,a in enumerate(bases) for j,b in enumerate(bases) for k,c in enumerate(bases)}
MARKERS={"Spike D614G (as labelled)":"VYYHKNNKSWMESGVYSSANNC","H1 HA":"DTLCIGYHANNSTDTVD"}

def find_proteins(dna, off=0):   # faithful port of TranslationService.FindProteins
    res=[]; i=0
    while i+3<=len(dna):
        if TABLE.get(dna[i:i+3])=='M':
            start=i; sb=['M']; i+=3
            while i+3<=len(dna):
                c=TABLE.get(dna[i:i+3])
                if c is None: sb.append('?'); i+=3; continue
                sb.append(c); i+=3
                if c=='*': res.append((''.join(sb),off+start)); break
        else: i+=1
    return res

raw=open(sys.argv[1]).read()
clean=''.join(l.strip() for l in raw.splitlines() if not l.startswith('>'))
print("raw bytes",len(raw),"genome nt",len(clean),"-> fits one 32KB chunk:",len(raw)<=32768)
for name,src in [("RAW (as the system sends it)",raw),("CLEAN (header+newlines stripped)",clean)]:
    ps=find_proteins(src)
    good=[p for p in ps if '?' not in p[0]]
    print(f"\n{name}: ORFs={len(ps)} with '?'={len(ps)-len(good)} longest={max(len(p[0]) for p in ps)} aa")
    print("  ORFs >=100 aa:",sum(len(p[0])>=100 for p in ps), " clean >=100 aa:",sum(len(p[0])>=100 for p in good))
# proper spike CDS 21563..25384 (1-based)
spike=''.join(TABLE[clean[i:i+3]] for i in range(21562,25384,3))
print("\nSpike len",len(spike), "ends with stop:",spike.endswith('*'))
print("Spike 140-166:",spike[139:166])
print("Spike 610-618:",spike[609:618], "res614=",spike[613])
print("marker in WT spike:","VYYHKNNKSWMESGVYSSANNC" in spike, "| WT window present:", "VYYHKNNKSWMESEFRVYSSANNC" in spike)
# Delta mutation E156G + del157-158 applied
delta=spike[:155]+'G'+spike[158:]
print("marker in spike with E156G+del157-158 (Delta):","VYYHKNNKSWMESGVYSSANNC" in delta)
# is spike found as an ORF by the pipeline?
for name,src in [("RAW",raw),("CLEAN",clean)]:
    ps=find_proteins(src)
    print(f"{name}: full spike recovered as an ORF:", any(spike.rstrip('*') in p[0] for p in ps),
          "| spike N-term 'MFVFLVLLPLVSSQ' found:", any('MFVFLVLLPLVSSQ' in p[0] for p in ps))
# reverse strand & overlapping ORF: ORF9b lies inside N (+1 frame) at 28284..28577
orf9b=''.join(TABLE[clean[i:i+3]] for i in range(28283,28577,3))
print("ORF9b:",orf9b[:20],"... found by pipeline (clean):",any(orf9b.rstrip('*')==p[0].rstrip('*') for p in find_proteins(clean)))
# ORF1ab frameshift
ps=find_proteins(clean); top=sorted(ps,key=lambda p:-len(p[0]))[:3]
print("top ORFs (clean):",[(len(p[0]),p[1]+1) for p in top], "-> pp1ab should be 7096 aa (needs -1 PRF at 13468)")
