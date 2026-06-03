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

# Copy project files keeping folder structures for efficient cache restoration
COPY ["apps/lazuar-api/BuildingBlocks/Domain/BuildingBlocks.Domain.csproj", "apps/lazuar-api/BuildingBlocks/Domain/"]
COPY ["apps/lazuar-api/BuildingBlocks/Application/BuildingBlocks.Application.csproj", "apps/lazuar-api/BuildingBlocks/Application/"]
COPY ["apps/lazuar-api/BuildingBlocks/Infrastructure/BuildingBlocks.Infrastructure.csproj", "apps/lazuar-api/BuildingBlocks/Infrastructure/"]

COPY ["apps/lazuar-api/SharedKernel/SharedKernel.csproj", "apps/lazuar-api/SharedKernel/"]

COPY ["apps/lazuar-api/Modules/Tenant/Contracts/Modules.Tenant.Contracts.csproj", "apps/lazuar-api/Modules/Tenant/Contracts/"]
COPY ["apps/lazuar-api/Modules/Tenant/Domain/Modules.Tenant.Domain.csproj", "apps/lazuar-api/Modules/Tenant/Domain/"]
COPY ["apps/lazuar-api/Modules/Tenant/Application/Modules.Tenant.Application.csproj", "apps/lazuar-api/Modules/Tenant/Application/"]
COPY ["apps/lazuar-api/Modules/Tenant/Infrastructure/Modules.Tenant.Infrastructure.csproj", "apps/lazuar-api/Modules/Tenant/Infrastructure/"]

COPY ["apps/lazuar-api/Modules/Messaging/Contracts/Modules.Messaging.Contracts.csproj", "apps/lazuar-api/Modules/Messaging/Contracts/"]
COPY ["apps/lazuar-api/Modules/Messaging/Domain/Modules.Messaging.Domain.csproj", "apps/lazuar-api/Modules/Messaging/Domain/"]
COPY ["apps/lazuar-api/Modules/Messaging/Application/Modules.Messaging.Application.csproj", "apps/lazuar-api/Modules/Messaging/Application/"]
COPY ["apps/lazuar-api/Modules/Messaging/Infrastructure/Modules.Messaging.Infrastructure.csproj", "apps/lazuar-api/Modules/Messaging/Infrastructure/"]

COPY ["apps/lazuar-api/Modules/UserAccess/Domain/Modules.UserAccess.Domain.csproj", "apps/lazuar-api/Modules/UserAccess/Domain/"]
COPY ["apps/lazuar-api/Modules/CRM/Domain/Modules.CRM.Domain.csproj", "apps/lazuar-api/Modules/CRM/Domain/"]

COPY ["apps/lazuar-api/src/Lazuar.Api/Lazuar.Api.csproj", "apps/lazuar-api/src/Lazuar.Api/"]

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
