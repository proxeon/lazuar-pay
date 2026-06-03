# syntax=docker/dockerfile:1.7-labs

# ==========================================
# 1. Build Stage
# ==========================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy directory-level MSBuild properties and packages configurations
COPY ["apps/lazuar-api/Directory.Build.props", "apps/lazuar-api/"]
COPY ["apps/lazuar-api/Directory.Packages.props", "apps/lazuar-api/"]
COPY ["apps/lazuar-api/global.json", "apps/lazuar-api/"]
COPY ["apps/lazuar-api/Lazuar.slnx", "apps/lazuar-api/"]

# Dynamically copy ALL .csproj files while preserving their folder structure!
# This eliminates the need to manually update the Dockerfile when adding new modules.
COPY --parents apps/lazuar-api/**/*.csproj ./

# Restore dependencies
RUN dotnet restore "apps/lazuar-api/src/Lazuar.Api/Lazuar.Api.csproj"

# Copy entire source files
COPY ["apps/lazuar-api/", "apps/lazuar-api/"]

# Build and publish to /app/publish
WORKDIR "/src/apps/lazuar-api/src/Lazuar.Api"
RUN dotnet publish "Lazuar.Api.csproj" -c Release -o /app/publish --no-restore

# ==========================================
# 2. Runtime Stage
# ==========================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 8080

# Configure ASP.NET Core production port
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Lazuar.Api.dll"]
