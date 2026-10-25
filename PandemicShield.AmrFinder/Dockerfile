FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["PandemicShield.AmrFinder/PandemicShield.AmrFinder.csproj", "PandemicShield.AmrFinder/"]
COPY ["PandemicShield.Bio/PandemicShield.Bio.csproj", "PandemicShield.Bio/"]
COPY ["PandemicShield.Contracts/PandemicShield.Contracts.csproj", "PandemicShield.Contracts/"]
COPY ["PandemicShield.DataAccess/PandemicShield.DataAccess.csproj", "PandemicShield.DataAccess/"]

RUN dotnet restore "PandemicShield.AmrFinder/PandemicShield.AmrFinder.csproj" -r linux-x64

COPY . .
WORKDIR "/src/PandemicShield.AmrFinder"

# Самодостатня збірка: в образі ncbi/amr немає .NET, а так він і не потрібен.
RUN dotnet publish "PandemicShield.AmrFinder.csproj" -c Release -r linux-x64 --self-contained true -o /app/publish

# Офіційний образ NCBI: AMRFinderPlus 4.2.7, BLAST, HMMER і база даних 2026-08-07.1 вже всередині, тож аналіз працює офлайн.
FROM ncbi/amr:4.2.7-2026-08-07.1 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV AMRFINDER_BIN=amrfinder
ENTRYPOINT ["/app/PandemicShield.AmrFinder"]
