FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src


COPY ["UndergroundVandals.Api/UndergroundVandals.Api.csproj", "UndergroundVandals.Api/"]
COPY ["UndergroundVandals.Bot/UndergroundVandals.Bot.csproj", "UndergroundVandals.Bot/"]
RUN dotnet restore "./UndergroundVandals.Bot/UndergroundVandals.Bot.csproj"

COPY . .
WORKDIR "/src/UndergroundVandals.Bot"
RUN dotnet build "./UndergroundVandals.Bot.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./UndergroundVandals.Bot.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "UndergroundVandals.Bot.dll"]