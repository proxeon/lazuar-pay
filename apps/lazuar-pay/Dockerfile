# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY apps/lazuar-pay/global.json apps/lazuar-pay/global.json
COPY apps/lazuar-pay/src/Lazuar.Pay/Lazuar.Pay.csproj apps/lazuar-pay/src/Lazuar.Pay/
RUN dotnet restore apps/lazuar-pay/src/Lazuar.Pay/Lazuar.Pay.csproj
COPY apps/lazuar-pay/src/Lazuar.Pay/ apps/lazuar-pay/src/Lazuar.Pay/
RUN dotnet publish apps/lazuar-pay/src/Lazuar.Pay/Lazuar.Pay.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 8081
ENV ASPNETCORE_URLS=http://+:8081
ENV ASPNETCORE_ENVIRONMENT=Production
USER root
RUN apt-get update \
  && apt-get install -y --no-install-recommends curl \
  && rm -rf /var/lib/apt/lists/*
USER app
COPY --from=build --chown=app:app /app/publish .
HEALTHCHECK --interval=15s --timeout=5s --start-period=30s --retries=5 \
  CMD curl -fsS http://127.0.0.1:8081/health || exit 1
ENTRYPOINT ["dotnet", "Lazuar.Pay.dll"]
