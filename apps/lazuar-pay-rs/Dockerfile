# syntax=docker/dockerfile:1
# Context: apps/lazuar-pay-rs (not repo root — root .dockerignore drops *secret*).

FROM rust:1.88-bookworm AS build
WORKDIR /src
COPY Cargo.toml Cargo.lock rustfmt.toml ./
COPY crates crates
COPY migrations migrations
RUN --mount=type=cache,target=/usr/local/cargo/registry \
    --mount=type=cache,target=/src/target \
    cargo build --release -p lazuar-pay-rs --locked \
    && cp /src/target/release/lazuar-pay-rs /tmp/lazuar-pay-rs

FROM debian:bookworm-slim AS runtime
RUN apt-get update \
  && apt-get install -y --no-install-recommends ca-certificates curl \
  && rm -rf /var/lib/apt/lists/* \
  && useradd --system --uid 10001 --home-dir /app --create-home pay
WORKDIR /app
COPY --from=build --chown=pay:pay /tmp/lazuar-pay-rs /app/lazuar-pay-rs
USER pay
EXPOSE 8081
ENV PORT=8081
ENV ASPNETCORE_ENVIRONMENT=Production
HEALTHCHECK --interval=15s --timeout=5s --start-period=30s --retries=5 \
  CMD curl -fsS http://127.0.0.1:8081/health || exit 1
ENTRYPOINT ["/app/lazuar-pay-rs"]
CMD ["serve"]
