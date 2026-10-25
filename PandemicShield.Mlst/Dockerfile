FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["PandemicShield.Mlst/PandemicShield.Mlst.csproj", "PandemicShield.Mlst/"]
COPY ["PandemicShield.Bio/PandemicShield.Bio.csproj", "PandemicShield.Bio/"]
COPY ["PandemicShield.Contracts/PandemicShield.Contracts.csproj", "PandemicShield.Contracts/"]
COPY ["PandemicShield.DataAccess/PandemicShield.DataAccess.csproj", "PandemicShield.DataAccess/"]

RUN dotnet restore "PandemicShield.Mlst/PandemicShield.Mlst.csproj" -r linux-x64

COPY . .
WORKDIR "/src/PandemicShield.Mlst"

# Самодостатня збірка: в образі staphb/mlst немає .NET.
RUN dotnet publish "PandemicShield.Mlst.csproj" -c Release -r linux-x64 --self-contained true -o /app/publish

# Образ StaPH-B: mlst 2.35.0, BLAST+ і схеми PubMLST уже всередині, тож тип штаму визначається офлайн.
FROM staphb/mlst:2.35.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV MLST_BIN=mlst
ENTRYPOINT ["/app/PandemicShield.Mlst"]
