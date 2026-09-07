# lazuar-pay-rs

Rust payment host for Lazuar Pay. New engine (032/05–07), not a port of `CheckoutRow`.

**Location is this folder.** Do not put this tree in `apps/lazuar-api`: that path is the archived `rust-port` host (gitignored on `main`, 028 P0s, 06 D06.2). Production money stays `apps/lazuar-pay` until cutover.

## Layout

```text
crates/
  domain/     # Money, Proof, propose(). No I/O. First mergeable crate (032/06 D06.6).
  rails/      # one module per rail (032/10). No sqlx.
  chain/      # Solana watcher boundary. Future --watcher-only.
  storage/    # sqlx, CAS, apply TX, pay_rs schema. Only crate that writes SQL.
  api/        # axum TypeSpec /v1 adapter. Parse → domain → storage.
  workers/    # outbox, settler, psync, retention. SKIP LOCKED.
  obs/        # /metrics + OTLP.
  app/        # binary: serve | --api-only | --worker-only | --watcher-only
migrations/   # sqlx migrate, one folder (pay_rs). P1 init is in.
```

v1 does **not** ship `acquiring`, `lazuar-vault`, or Bitcoin crates (032/19).

## Build order

1. `domain` + tests — done.
2. `migrations/` + unique tests (P1) — done.
2b. `storage::apply` TX + G4 races (P2) — done.
3. Thin TypeSpec `/v1` adapter, test rail (P3 / 033/03) — done.
4. `workers` (expire, outbound HMAC, PSync skip test, CHIP never auto-settled) — done.
5. Stripe hosted + webhook + PSync + refund (P5 / 033/05) — done. Fixture-backed; CI does not call `api.stripe.com`.
6. CHIP hosted + PEM vault + webhook + PSync (P6 / 033/06) — done.
7. Billplz hosted + collection vault + form HMAC + PSync (P7 / 033/07) — done.
8. Xendit hosted invoice + callback-token webhook + PSync (P8 / 033/08) — done.
9. Razorpay payment link + split-secret vault + HMAC webhook + PSync (P9 / 033/09) — done.
10. Solana Pay URI + receive-address vault + reservation + `Proof::ChainTx` watcher (P10 / 033/10) — done.
11. Payment-links + occupancy HTTP + `slot_key` child mint + MYR products (P11 / 033/11) — done.
12. Merchant charges list + receipts + POST/GET/resolve refunds (P12 / 033/12) — done.
13. Org webhook PUT/GET/rotate/test + One inbound + `GET /v1/orgs/{orgId}/ready` (P13 / 033/13) — done.
14. Prometheus `GET /metrics` + `psp_parse_outcome` + pending-refund gauges (P14 / 033/14) — done. OTLP skipped.
15. `ThrowIfMisconfigured` + live mint HTTP in `serve` + CORS + `X-Request-Id` (P15 / 033/15) — done. Fake PSP only in Testing.
16. Offline terminal backfill `public` → `pay_rs` (P16 / 033/16) — done. Skips `open`. No JWKS / SPA flip.
17. Dockerfile + compose overlay on `pay-db` (P17 / 033/17) — this tree. Does not replace .NET `pay`.

```sh
cargo test -p domain
cargo test -p rails     # Stripe parse + fixtures; no sqlx
cargo test -p storage   # Docker: Postgres 16 via testcontainers
cargo test -p api       # Docker + Fake One + Fake Stripe
cargo test -p workers   # Docker: SKIP LOCKED loops
cargo run -p lazuar-pay-rs -- serve          # :8081 + workers + watcher
cargo run -p lazuar-pay-rs -- --api-only     # :8081, no loops
cargo run -p lazuar-pay-rs -- --worker-only  # loops, no bind
cargo run -p lazuar-pay-rs -- --watcher-only # Solana watch + bind only
cargo run -p lazuar-pay-rs -- backfill        # dry-run terminal copy public → pay_rs
cargo run -p lazuar-pay-rs -- backfill --apply
```

Testing `serve` (`ASPNETCORE_ENVIRONMENT=Testing`, default) uses Fake One (`Authorization: Bearer test-writer`) unless `One__BaseUrl` is set, Fake PSP mint, and Fake worker remotes. Staging/Production require `Pay__WrapKey` (32-byte base64), `ConnectionStrings__Pay`, public `One__BaseUrl`, https `Pay__CheckoutBaseUrl`, and `Pay__CorsOrigins` (must include the checkout origin). Live mint decrypts the vault and POSTs to the PSP; CI never calls `api.stripe.com`.

Public start limiter is per-process (`Pay__StartMaxPerMinute`, default 20); two **API** replicas = 2×. Two **worker** replicas are OK (SKIP LOCKED). `Pay__MetricsToken` gates `/metrics`. `X-Request-Id` is echoed (printable ASCII, cap 64).

`domain` must compile with no `tokio`, `sqlx`, or `axum`. CI greps it.

## Compose (G5 overlay)

Does **not** replace .NET `pay` in `apps/lazuar-pay/docker-compose.pay.yml`. `:8081` is mutex: stop `cargo run` / `task pay:dev` / `--profile apps` `pay` first.

```sh
docker compose \
  -f apps/lazuar-pay/docker-compose.pay.yml \
  -f apps/lazuar-pay-rs/docker-compose.overlay.yml \
  --profile pay-rs up -d --build
# or: task pay-rs:compose
```

Laptop overlay defaults `ASPNETCORE_ENVIRONMENT=Testing` (Fake PSP mint) and
`One__BaseUrl=http://host.docker.internal:8080/api/v1`. Image default is Production
(ThrowIf fail-closed). Bake target `lazuar-pay-rs` is **not** in the default GHCR group.

## Honesty

- Proof is the only thing that can Take. No `Proof::Admin`.
- Status is a fold (`propose`). After `expires_at`, never fulfill.
- Exact amount in v1. USDC minor exponent 6, quote display 2. IDR stays 2-decimal.
- One live attempt in v1. Schema allows N.
- Buyer JSON: `Processing` → `open`, `Settled` → `paid`.
- Same Postgres instance, schema `pay_rs`. No dual-write with `public.checkouts`.
