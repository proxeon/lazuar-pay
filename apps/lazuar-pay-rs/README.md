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
2. `migrations/` + unique tests (P1 / 033/01) — done.
2b. `storage::apply` TX + G4 races (P2 / 033/02) — this tree.
3. `api` adapter: health, whoami, test rail, webhook ingest, public start.
4. `workers`.
5. Live rails one PR each.
6. `chain/` Solana watcher (`--watcher-only` capable).

```sh
cargo test -p domain
cargo test -p storage   # Docker: Postgres 16 via testcontainers
cargo check --workspace
```

`domain` must compile with no `tokio`, `sqlx`, or `axum`. CI greps it.

## Honesty

- Proof is the only thing that can Take. No `Proof::Admin`.
- Status is a fold (`propose`). After `expires_at`, never fulfill.
- Exact amount in v1. USDC minor exponent 6, quote display 2. IDR stays 2-decimal.
- One live attempt in v1. Schema allows N.
- Buyer JSON: `Processing` → `open`, `Settled` → `paid`.
- Same Postgres instance, schema `pay_rs`. No dual-write with `public.checkouts`.
