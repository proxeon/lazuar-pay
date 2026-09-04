# syntax=docker/dockerfile:1
FROM rust:1-bookworm AS build
WORKDIR /src
COPY apps/lazuar-api/Cargo.toml apps/lazuar-api/Cargo.toml
COPY apps/lazuar-api/Cargo.lock apps/lazuar-api/Cargo.lock
COPY apps/lazuar-api/src apps/lazuar-api/src
COPY apps/lazuar-api/migrations apps/lazuar-api/migrations
WORKDIR /src/apps/lazuar-api
RUN cargo build --release --bin lazuar-api

FROM debian:bookworm-slim AS runtime
RUN apt-get update \
  && apt-get install -y --no-install-recommends ca-certificates curl \
  && rm -rf /var/lib/apt/lists/* \
  && useradd --system --uid 10001 --create-home pay
WORKDIR /app
COPY --from=build --chown=pay:pay /src/apps/lazuar-api/target/release/lazuar-api /app/lazuar-api
USER pay
EXPOSE 8081
ENV LISTEN_ADDR=0.0.0.0:8081
ENV ASPNETCORE_ENVIRONMENT=Production
HEALTHCHECK --interval=15s --timeout=5s --start-period=30s --retries=5 \
  CMD curl -fsS http://127.0.0.1:8081/ready || exit 1
ENTRYPOINT ["/app/lazuar-api"]
