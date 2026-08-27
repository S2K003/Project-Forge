# ForgeOps.Api — container-first, twelve-factor (ProjectForge.md §7.6).
# Build context is the repository root:  docker build -f src/ForgeOps.Api/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json Directory.Build.props ./
COPY src/ForgeOps.Contracts/ src/ForgeOps.Contracts/
COPY src/ForgeOps.AI/ src/ForgeOps.AI/
COPY src/ForgeOps.Demo/ src/ForgeOps.Demo/
COPY src/ForgeOps.Api/ src/ForgeOps.Api/

RUN dotnet restore src/ForgeOps.Api/ForgeOps.Api.csproj
RUN dotnet publish src/ForgeOps.Api/ForgeOps.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Non-root (§7.6).
RUN useradd --uid 64198 --create-home forgeops
USER forgeops

COPY --from=build /app ./

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1
EXPOSE 8080

# Free-tier hosts route their own health checks; this is a local convenience.
HEALTHCHECK --interval=30s --timeout=4s --start-period=10s --retries=3 \
  CMD wget -qO- http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "ForgeOps.Api.dll"]
