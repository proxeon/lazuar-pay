# lazuar-api (sync Rust port)

Sync Rust port of `apps/lazuar-pay` — branch `rust-port`. The C# service stays
the reference implementation and production fallback until cutover is accepted.

- **Spec + gates:** `plans/023-evals/04-rust-port-spec.md`
- **Parity evidence:** `plans/023-evals/05-parity-evidence.md`
- **Decision log:** `PORT_DECISIONS.md`
- **.NET side:** never edited on this branch (G6)

## Run

```sh
cd apps/lazuar-api
cargo test                       # 78 tests; per-test real Postgres databases
Pay__ConnectionString='host=localhost port=5435 user=postgres password=postgres dbname=lazuar_pay' \
Pay__WrapKey='<32-byte base64>' \
LISTEN_ADDR=127.0.0.1:8095 cargo run
```

Port `8081` belongs to the C# service until cutover; the Rust host defaults to
`8095` in development.

## Stack

| Concern | Crate |
|---|---|
| HTTP | `rouille` (thread-per-request — sync, D001) |
| DB | `postgres` + `r2d2`, raw SQL (D003) |
| Money | `rust_decimal` (D006) |
| Signatures | `hmac`/`sha2`/`subtle` per rail |
| Vault | `aes-gcm` (SecretBox layout: nonce‖tag‖cipher) |
| Solana | first-party-style client over `Transport` (D009) |

## Layout

```
src/app.rs            router + State (handlers: thin)
src/config.rs         Pay:*/One:* env settings
src/domain/           currency rules, CAS transitions, checkout store
src/money/            refunds (reserve/settle), fulfillment, receipts
src/rails/            providers, remote seam, per-rail webhooks, solana/*
src/identity/         bearer, OneClient, member gate, whoami
src/secrets.rs        AES-256-GCM vault
src/webhooks/         ingest conductor, outbound dispatch + SSRF pinning
src/workers.rs        background loops (dispatch 5s, solana watch 2s)
migrations/           reference schema (dump of the C# EF migrations)
```

## Workers

The binary hosts the outbound webhook dispatcher (5s) and the Solana reference
watcher (2s) as threads. Single-replica constraint from the C# runbook still
applies: scaling out silently disables in-process gates.
