
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["PandemicShield.Aggregator/PandemicShield.Aggregator.csproj", "PandemicShield.Aggregator/"]
COPY ["PandemicShield.Contracts/PandemicShield.Contracts.csproj", "PandemicShield.Contracts/"]
COPY ["PandemicShield.DataAccess/PandemicShield.DataAccess.csproj", "PandemicShield.DataAccess/"]

RUN dotnet restore "PandemicShield.Aggregator/PandemicShield.Aggregator.csproj"

COPY . .
WORKDIR "/src/PandemicShield.Aggregator"
RUN dotnet build "PandemicShield.Aggregator.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "PandemicShield.Aggregator.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "PandemicShield.Aggregator.dll"]