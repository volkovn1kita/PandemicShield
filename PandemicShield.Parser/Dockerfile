
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["PandemicShield.Parser/PandemicShield.Parser.csproj", "PandemicShield.Parser/"]
COPY ["PandemicShield.Contracts/PandemicShield.Contracts.csproj", "PandemicShield.Contracts/"]

RUN dotnet restore "PandemicShield.Parser/PandemicShield.Parser.csproj"

COPY . .
WORKDIR "/src/PandemicShield.Parser"
RUN dotnet build "PandemicShield.Parser.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "PandemicShield.Parser.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

EXPOSE 8080 
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "PandemicShield.Parser.dll"]