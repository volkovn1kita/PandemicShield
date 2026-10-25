FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["PandemicShield.Nextclade/PandemicShield.Nextclade.csproj", "PandemicShield.Nextclade/"]
COPY ["PandemicShield.Bio/PandemicShield.Bio.csproj", "PandemicShield.Bio/"]
COPY ["PandemicShield.Contracts/PandemicShield.Contracts.csproj", "PandemicShield.Contracts/"]
COPY ["PandemicShield.DataAccess/PandemicShield.DataAccess.csproj", "PandemicShield.DataAccess/"]

RUN dotnet restore "PandemicShield.Nextclade/PandemicShield.Nextclade.csproj"

COPY . .
WORKDIR "/src/PandemicShield.Nextclade"
RUN dotnet build "PandemicShield.Nextclade.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "PandemicShield.Nextclade.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Nextclade CLI — статичний бінарник з офіційного образу Nextstrain.
FROM nextstrain/nextclade:3.24.0 AS nextclade

# Набори даних завантажуються під час збирання образу, тож аналіз працює без інтернету.
# Список має збігатися з NEXTCLADE_DATASETS в API (docker-compose.yaml).
FROM debian:bookworm-slim AS datasets
ARG NEXTCLADE_DATASETS="nextstrain/sars-cov-2/wuhan-hu-1/orfs nextstrain/flu/h1n1pdm/ha/CY121680 nextstrain/flu/h3n2/ha/EPI1857216 nextstrain/rsv/a/EPI_ISL_412866 nextstrain/rsv/b/EPI_ISL_1653999 nextstrain/mpox/all-clades"
RUN apt-get update && apt-get install -y --no-install-recommends ca-certificates && rm -rf /var/lib/apt/lists/*
COPY --from=nextclade /usr/bin/nextclade /usr/bin/nextclade
RUN for dataset in $NEXTCLADE_DATASETS; do \
        nextclade dataset get --name "$dataset" --output-dir "/datasets/$dataset" || exit 1; \
    done

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app
COPY --from=nextclade /usr/bin/nextclade /usr/bin/nextclade
COPY --from=datasets /datasets /datasets
ENV NEXTCLADE_BIN=/usr/bin/nextclade
ENV NEXTCLADE_DATASETS_DIR=/datasets
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "PandemicShield.Nextclade.dll"]
