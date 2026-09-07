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
8. Xendit hosted invoice + callback-token webhook + PSync (P8 / 033/08) — this tree. Mint wire amount is **major** (`10` not `1000`). CI does not call `api.xendit.co`.
9. Razorpay, one PR.
10. `chain/` Solana watcher (`--watcher-only` capable).

```sh
cargo test -p domain
cargo test -p rails     # Stripe parse + fixtures; no sqlx
cargo test -p storage   # Docker: Postgres 16 via testcontainers
cargo test -p api       # Docker + Fake One + Fake Stripe
cargo test -p workers   # Docker: SKIP LOCKED loops
cargo run -p lazuar-pay-rs -- serve          # :8081 + workers
cargo run -p lazuar-pay-rs -- --api-only     # :8081, no loops
cargo run -p lazuar-pay-rs -- --worker-only  # loops, no bind
```

Testing `serve` uses Fake One (`Authorization: Bearer test-writer`) unless `One__BaseUrl` is set. Public start limiter is per-process (`Pay__StartMaxPerMinute`, default 20); two **API** replicas = 2×. Two **worker** replicas are OK (SKIP LOCKED).

`domain` must compile with no `tokio`, `sqlx`, or `axum`. CI greps it.

## Honesty

- Proof is the only thing that can Take. No `Proof::Admin`.
- Status is a fold (`propose`). After `expires_at`, never fulfill.
- Exact amount in v1. USDC minor exponent 6, quote display 2. IDR stays 2-decimal.
- One live attempt in v1. Schema allows N.
- Buyer JSON: `Processing` → `open`, `Settled` → `paid`.
- Same Postgres instance, schema `pay_rs`. No dual-write with `public.checkouts`.
