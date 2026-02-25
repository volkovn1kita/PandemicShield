
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["PandemicShield.Worker/PandemicShield.Worker.csproj", "PandemicShield.Worker/"]
COPY ["PandemicShield.Contracts/PandemicShield.Contracts.csproj", "PandemicShield.Contracts/"]
COPY ["PandemicShield.DataAccess/PandemicShield.DataAccess.csproj", "PandemicShield.DataAccess/"]

RUN dotnet restore "PandemicShield.Worker/PandemicShield.Worker.csproj"

COPY . .
WORKDIR "/src/PandemicShield.Worker"
RUN dotnet build "PandemicShield.Worker.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "PandemicShield.Worker.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "PandemicShield.Worker.dll"]