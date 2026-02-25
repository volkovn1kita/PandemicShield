
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["PandemicShield.Api/PandemicShield.Api.csproj", "PandemicShield.Api/"]
COPY ["PandemicShield.Contracts/PandemicShield.Contracts.csproj", "PandemicShield.Contracts/"]
COPY ["PandemicShield.DataAccess/PandemicShield.DataAccess.csproj", "PandemicShield.DataAccess/"]

RUN dotnet restore "PandemicShield.Api/PandemicShield.Api.csproj"

COPY . .
WORKDIR "/src/PandemicShield.Api"

RUN dotnet build "PandemicShield.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "PandemicShield.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

EXPOSE 8080

COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "PandemicShield.Api.dll"]