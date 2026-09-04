# Phase 8 cutover runbook

Ops checklist (`plans/029-checklist/07-phase-8-cutover.md`).

**Laptop 2026-09-05:** C# `dotnet watch` on `:8081` stopped. Rust
(`Development`, `LISTEN_ADDR=127.0.0.1:8081`) is the money process on the same
`lazuar_pay` Postgres. Pending deliveries were already 0. Production public
HTTPS was **not** applied — boot guards reject localhost One/checkout.

C# binary/image stays for 30-day rollback. Do not delete `apps/lazuar-pay`.

Code gates that must stay true (enforced by `scripts/pay-cutover-preflight.sh`
and `tests/http_paths.rs`):

- Buyer paths are `/v1/pay/{token}`, `/start`, `/confirm`. No `/v1/public/pay`.
- Start uses `VaultedRail` when the pool exists; `TestRail` only if the pool is missing.
- Refunds and webhook ingest load `LiveRefunder`. `NoopRefunder` is tests-only.
- Issue 018: Razorpay captured events dedupe on body-derived `captured:{payment_id}`.
- Rust never migrates schema. `/ready` fails closed on missing `checkouts` / `org_settings`.
- Single replica. Subscriptions stay **A**: accept `mo`/`yr`, never rebill.

---

## Pre-flight (all required — human)

- [ ] Phase 6 HTTP fixtures 029/01–05 green. `cargo test --manifest-path apps/lazuar-api/Cargo.toml`.
- [ ] Phase 7 packaging 029/06 done. Image HEALTHCHECK is `/ready`.
- [ ] CI green for **C# and Rust** (`dotnet test` + `cargo test` + honesty scripts). CI currently runs on `main`; run both suites on this SHA before swapping.
- [ ] Side-by-side on the same `lazuar_pay` DB for ≥ 7 days: C# serving `:8081`, Rust on `:8095`. Unexpected Rust errors = stop.
- [ ] A human can explain `money/refunds.rs`, `money/fulfillment.rs`, `domain/transitions.rs` (CAS + ambiguous-refund) without the comments.
- [ ] Rollback rehearsal done once on a **staging** DB: Rust `:8081` → kill → C# `:8081` → one checkout end to end.
- [ ] Postgres backup **named** and restore-tested (Neon PITR / `pg_dump`). Name: `________________`. **Do not cut over with this blank.**
- [ ] 018 fixed in the stack that will serve (Razorpay body-derived `captured:` id).
- [ ] `scripts/pay-cutover-preflight.sh` exits 0.

---

## Execution (human, after pre-flight)

Laptop after 2026-09-05: Rust `http://127.0.0.1:8081/ready`. C# is stopped.
Shadow port `:8095` is free. Same Postgres.

1. Pause org charges (`ChargesPaused` via One `org.suspended` / settings). Drain
   `org_webhook_deliveries` pending count to 0.
2. Freeze C# writes (stop the `:8081` process / `pay` container).
3. Start Rust with the **same** `ConnectionStrings__Pay`, `Pay__WrapKey`, rail
   webhook secrets, checkout/public/CORS URLs, `One__*`, Solana cluster/RPC:
   `LISTEN_ADDR=0.0.0.0:8081` `ASPNETCORE_ENVIRONMENT=Production`.
   Boot guards B1–B15 must pass — a failed guard is a stop.
   Compose overlay (does not run itself):
   `docker compose -f apps/lazuar-pay/docker-compose.pay.yml -f apps/lazuar-pay/docker-compose.cutover.yml --profile rust up -d`
4. Smoke on `:8081`: `/health`, `/ready`, whoami, one test-rail checkout → start →
   paid → receipt → Plane C HMAC; one webhook replay `{duplicate:true}`; one
   Stripe-fixture refund if the org has a test key.
5. Unpause charges.
6. Watch stale-pending refund log and dispatch errors for 24h.

---

## Rollback

Stop Rust, start C# on `:8081`, same DB, same wrap key. No Rust migration exists,
so this is a process swap. Keep the C# image deployable **30 days**. Tag C# source
`csharp-final` only after the swap is accepted.

---

## After (only if the swap happened)

- Update `plans/023-evals/05-parity-evidence.md` to “cut over”.
- Archive 026 as historical. This file stays the ops checklist.

## Residual risks (accepted)

- First production-only bug will be found by traffic, in a young Rust money path.
  Mitigation: ported suite + C# rollback.
- `ureq` webhook dispatch: connect-time address pinning is implemented; SNI still uses the hostname.
- Single-replica constraint still applies (in-process gates/limiter).
- Subscriptions accept `mo`/`yr` and never rebill.
