# 035/02 — CLI implementation plan (`crates/client` + `crates/cli`)

**Date:** 2026-09-10 · Follows [01](01-rest-cli-mcp.md) §5 and §9 steps 1+3.
**What this is:** how `lazuar-pay` lands in `apps/lazuar-pay-rs` without a fourth money writer.
**What this is not:** MCP, `listen`, gateway PUT, refunds, payment-links. Those wait until
checkout mint/get and money lists are boring.

---

## 0. Decision

Two crates, HTTP only, TypeSpec wire JSON:

| Crate | Package | Binary | Depends on |
|---|---|---|---|
| `crates/client` | `pay-client` | — | reqwest, serde_json, thiserror. **No sqlx, axum, storage.** |
| `crates/cli` | `pay-cli` | `lazuar-pay` | `pay-client`, clap. **No sqlx, storage.** |

The money host stays `lazuar-pay-rs serve`. The CLI is a **client** of `:8081`, so it
works against .NET Pay today and Rust tomorrow.

Stdout is the host JSON (pretty). Status words are wire `open`/`paid`/`failed`/`expired`,
never domain `Settled`. Amounts stay JSON numbers.

---

## 1. Auth / env

```text
LAZUAR_PAY_BASE_URL   default http://localhost:8081
LAZUAR_PAY_API_KEY    required (lzr_sk_… or Testing test-writer)
LAZUAR_PAY_ORG_ID     required for org-scoped commands
```

Flags override env: `--base-url`, `--api-key`, `--org-id`.

Client rejects `sk_` / `sk_test_` / `sk_live_` locally (same 032/13 door as the host).
`lzr_sk_` is the machine family. Testing Fake One also accepts `test-writer`.

---

## 2. Commands (this PR)

```text
lazuar-pay whoami
lazuar-pay ready
lazuar-pay checkout create --provider <rail> --amount <n> [--currency MYR] [--idempotency-key]
lazuar-pay checkout get <id>
lazuar-pay payments list [--limit] [--after]
lazuar-pay receipts list [--limit] [--after]
lazuar-pay receipts get <id>
```

`--provider` is required (no silent `test` default — mint with an explicit rail).
`--amount` is a decimal string; never `f64`.

Deferred (01 §9): `refund`, `payment-link`, `listen`, `gateway put`.

---

## 3. HTTP map

| Command | Method | Path |
|---|---|---|
| whoami | GET | `/v1/whoami` |
| ready | GET | `/v1/orgs/{orgId}/ready` |
| checkout create | POST | `/v1/checkouts` |
| checkout get | GET | `/v1/checkouts/{id}` |
| payments list | GET | `/v1/orgs/{orgId}/payments` |
| receipts list | GET | `/v1/orgs/{orgId}/receipts` |
| receipts get | GET | `/v1/orgs/{orgId}/receipts/{id}` |

Idempotency-Key header on create when `--idempotency-key` is set. Body still includes
`org_id` like the TypeSpec `CreateCheckoutRequest`.

Non-2xx → parse `PayProblem` `{status,title,detail}` to stderr, exit 1.

---

## 4. Tests

1. **client unit:** `sk_` rejected; empty base/key; trailing slash stripped.
2. **client HTTP (Docker Postgres + Fake One):** spawn `api::router(testing_state)` on
   `127.0.0.1:0`; `lzr_sk_test` whoami, ready, checkout create/get. Amount is a JSON
   number; status is `open`; `pay_url` starts with checkout origin.
3. **cli unit:** clap requires `--provider` and `--amount` on create; `sk_` flag fails
   before HTTP.
4. **cli HTTP:** `run()` against the same Fake host; stdout JSON has `pay_url`.

No live PSP. No `apply()` from the CLI crate.

---

## 5. Taskfile / README

- `task pay-rs:cli` → `cargo test -p pay-client -p pay-cli`
- `pay-rs:check` greps `pay-client` for sqlx/axum/storage
- README: `cargo run -p pay-cli -- whoami`
