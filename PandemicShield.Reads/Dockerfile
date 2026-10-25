FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["PandemicShield.Reads/PandemicShield.Reads.csproj", "PandemicShield.Reads/"]
COPY ["PandemicShield.Bio/PandemicShield.Bio.csproj", "PandemicShield.Bio/"]
COPY ["PandemicShield.Contracts/PandemicShield.Contracts.csproj", "PandemicShield.Contracts/"]
COPY ["PandemicShield.DataAccess/PandemicShield.DataAccess.csproj", "PandemicShield.DataAccess/"]

RUN dotnet restore "PandemicShield.Reads/PandemicShield.Reads.csproj" -r linux-x64

COPY . .
WORKDIR "/src/PandemicShield.Reads"

# Самодостатня збірка: в образі з інструментами немає .NET.
RUN dotnet publish "PandemicShield.Reads.csproj" -c Release -r linux-x64 --self-contained true -o /app/publish

# Панель референсів для вірусних прочитань: reference.fasta тих самих наборів Nextclade, що й у воркері Nextclade.
FROM nextstrain/nextclade:3.24.0 AS nextclade
FROM debian:bookworm-slim AS references
ARG NEXTCLADE_DATASETS="nextstrain/sars-cov-2/wuhan-hu-1/orfs nextstrain/flu/h1n1pdm/ha/CY121680 nextstrain/flu/h3n2/ha/EPI1857216 nextstrain/rsv/a/EPI_ISL_412866 nextstrain/rsv/b/EPI_ISL_1653999 nextstrain/mpox/all-clades"
RUN apt-get update && apt-get install -y --no-install-recommends ca-certificates && rm -rf /var/lib/apt/lists/*
COPY --from=nextclade /usr/bin/nextclade /usr/bin/nextclade
RUN mkdir -p /references && for dataset in $NEXTCLADE_DATASETS; do \
        nextclade dataset get --name "$dataset" --output-dir "/tmp/$dataset" || exit 1; \
        name=$(head -1 "/tmp/$dataset/reference.fasta" | cut -c2- | cut -d' ' -f1); \
        printf '%s\t%s\n' "$dataset" "$name" >> /references/references.tsv; \
        cat "/tmp/$dataset/reference.fasta" >> /references/all.fasta; \
    done

# Інструменти з bioconda, версії зафіксовано: minimap2 (картування), samtools (консенсус, глибина),
# fastp (обрізання), SKESA (збирання Illumina, як у NCBI Pathogen Detection), Flye (збирання Nanopore), seqkit (статистика).
FROM mambaorg/micromamba:2.3.3 AS final
RUN micromamba install -y -n base -c conda-forge -c bioconda \
        minimap2=2.28 samtools=1.21 skesa=2.5.1 flye=2.9.5 fastp=0.24.0 seqkit=2.9.0 \
    && micromamba clean -a -y
# Racon (полірування збірок Nanopore) — окремим шаром: кілька МБ, і великий шар вище не перезбирається.
RUN micromamba install -y -n base -c conda-forge -c bioconda racon=1.5.0     && micromamba clean -a -y
# Спільний том /data належить root, як і в інших воркерах; власна точка входу оминає активацію conda, тож PATH — явно.
USER root
ENV PATH=/opt/conda/bin:$PATH
WORKDIR /app
COPY --from=references /references /references
COPY --from=build /app/publish .
ENV READS_REFERENCES_DIR=/references
ENTRYPOINT ["/app/PandemicShield.Reads"]
