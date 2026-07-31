# syntax=docker/dockerfile:1.7-labs

# ==========================================
# 1. Build Stage
# ==========================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Directory-level MSBuild props / solution metadata
COPY ["apps/lazuar-api/Directory.Build.props", "apps/lazuar-api/"]
COPY ["apps/lazuar-api/Directory.Packages.props", "apps/lazuar-api/"]
COPY ["apps/lazuar-api/global.json", "apps/lazuar-api/"]
COPY ["apps/lazuar-api/Lazuar.slnx", "apps/lazuar-api/"]

# Generated API contracts live outside apps/lazuar-api but are project-referenced
COPY ["packages/api-types-dotnet/Lazuar.ApiContracts.csproj", "packages/api-types-dotnet/"]

# Dynamically copy ALL .csproj files while preserving folder structure
COPY --parents apps/lazuar-api/**/*.csproj ./

# Restore (layer-cached while csproj graph is stable)
RUN dotnet restore "apps/lazuar-api/src/Lazuar.Api/Lazuar.Api.csproj"

# Full sources for publish
COPY ["packages/api-types-dotnet/", "packages/api-types-dotnet/"]
COPY ["apps/lazuar-api/", "apps/lazuar-api/"]

WORKDIR "/src/apps/lazuar-api/src/Lazuar.Api"
RUN dotnet publish "Lazuar.Api.csproj" -c Release -o /app/publish --no-restore

# ==========================================
# 2. Runtime Stage
# ==========================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# curl for HEALTHCHECK (debian-based aspnet image)
USER root
RUN apt-get update \
  && apt-get install -y --no-install-recommends curl \
  && rm -rf /var/lib/apt/lists/*
USER app

COPY --from=build --chown=app:app /app/publish .
HEALTHCHECK --interval=15s --timeout=5s --start-period=90s --retries=5 \
  CMD curl -fsS http://127.0.0.1:8080/health || exit 1
ENTRYPOINT ["dotnet", "Lazuar.Api.dll"]
