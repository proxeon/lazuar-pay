# 035/01 — REST, CLI, MCP for other AI agents

**Date:** 2026-09-10 · Index [00](00-index.md) · Host `apps/lazuar-pay-rs`.

**What this is:** how to expose Pay so another app or agent can mint a `pay_url` and
unlock on Plane C without clicking the merchant SPA for every charge.
**What this is not:** a new money writer, a Pay-minted API-key product, dashboard
replacement, or x402 / Pay.sh.

---

## 0. Verdict

REST is already the product. CLI and MCP should be thin clients of the same TypeSpec
`/v1` + `lzr_sk_` contract — not a second money API, and not in-process calls into
`storage::apply`. The dashboard stays the human bootstrap (One keys + BYOK vault).
Agents attach after that.

| Surface | Status | Build |
|---|---|---|
| REST | Exists (`/v1` + `lzr_sk_`) | Document, OpenAPI honesty for Rust, poll as first-class |
| CLI | Missing | HTTP client binary, same env as `pay-node` |
| MCP | Missing | stdio adapter over that client; ~8 tools; no secrets |

---

## 1. What exists today

The host already exposes ~30 `/v1` operations (`packages/pay-spec`, axum `api::router`).
Integrators already do this without the SPA (`examples/pay-node`, root README):

1. Mint a One workspace key (`lzr_sk_` with `tenant:read` + `authz:check`) in merchant
   **Developers → API keys**. That page talks to **One**, not Pay. Pay does not mint
   `lzr_sk_` and does not mint Hub/Stripe `sk_`.
2. `PUT /v1/orgs/{orgId}/webhooks` with an HTTPS URL; copy `whsec_` once.
3. `POST /v1/checkouts` or `/v1/payment-links` with `Authorization: Bearer lzr_sk_…`.
4. Send the buyer `pay_url` (`{CheckoutBaseUrl}/c/{token}`).
5. Unlock on Plane C HMAC (`payment.completed` / `payment.failed` / `checkout.expired` /
   `refund.created`).

Identity already has two Bearer families (032/13): human JWT (SPA) and `lzr_sk_`
(machine). `sk_` as Authorization is 401 before One. Pay does not own users, orgs, or
API keys (no `organizations` / `users` / `members` / `api_keys` tables).

So “expose REST for AI agents” is mostly **documentation + a client**, not new routes.
The real gaps: humans still click the dashboard to bootstrap, and agents cannot host a
public webhook as easily as `pay-node` can.

`lazuar-pay-rs` today is `serve` / `--api-only` / `--worker-only` / `--watcher-only` /
`backfill`. That is the money host, not a merchant CLI.

---

## 2. Locks (do not reopen)

1. **One TypeSpec catalog.** Agents hit the same `/v1` the SPA and `pay-node` use. Do
   not add `/v1/agent/*`. Do not invent a second JSON dialect (wire `paid`, not domain
   `Settled`).
2. **CLI and MCP are HTTP clients.** They must not call `storage::apply`. That is a
   fourth writer and breaks 032/12 (one SoT). They work against .NET `:8081` today and
   Rust tomorrow.
3. **Do not generate the host from OpenAPI.** Generating a *client* from
   `packages/pay-spec/dist/openapi.yaml` is fine.
4. **Do not put MCP inside `serve`.** A tool-loop bug must not take down fulfill /
   PSync. Crate boundaries are future binaries (032/05 D8, 032/19).
5. **No Pay-minted API keys.** `lzr_sk_` stays One’s. Never `VITE_*`. Vault PUT still
   accepts `sk_test` **in JSON**; Authorization still rejects `sk_`.
6. **No `Proof::Admin`.** Agents cannot mark paid. Status `paid` means a sufficient
   Proof took.
7. **Secrets are not tool arguments.** Gateway PUT (CHIP PEM, Stripe `sk_`, Solana
   receive address), One `whsec_`, wrap key, metrics token stay off MCP.
8. **Buyer doors stay buyer doors.** `POST /v1/pay/{token}/start` and `/confirm` are
   not agent tools.
9. **Not x402 / Pay.sh.** Agents mint a hosted link; a person still pays on checkout /
   Stripe / CHIP / Solana QR.

---

## 3. One contract, three adapters

```text
                    packages/pay-spec  (TypeSpec / OpenAPI)
                              │
              ┌───────────────┼───────────────┐
              ▼               ▼               ▼
         REST :8081        CLI client      MCP server
         (axum, exists)    (new)           (new)
              │               │               │
              └───────────────┴───────────────┘
                              │
                    pay-client (new, HTTP only)
                    Bearer lzr_sk_ → /v1
                              │
                    lazuar-pay-rs serve   (money host)
```

Suggested crates (v1 does not ship acquiring / Bitcoin / `lazuar-vault` — 032/19):

| Crate | Job |
|---|---|
| `crates/client` | Typed HTTP: whoami, mint, list, refund, ready. No sqlx. |
| `crates/cli` | `lazuar-pay` binary: clap over `client` |
| `crates/mcp` | stdio MCP over `client` |
| `crates/app` | unchanged: `serve` / workers / backfill |

Optional later: `lazuar-pay-rs --mcp` that only **spawns** the MCP binary, still as an
HTTP client.

---

## 4. REST — ship as the agent API, tighten edges

**Keep `/v1` as-is.** Adding `/v1/agent/*` duplicates the catalog and will drift.

What to add (small):

| Work | Why |
|---|---|
| Point OpenAPI honesty at **Rust** `api::router` too (today `scripts/check-pay-openapi-honesty.mjs` scrapes .NET `Map*`) | Agents will generate from `packages/pay-spec/dist/openapi.yaml` |
| Machine-key cookbook in README: env vars, idempotency, problem+json | `examples/pay-node` is the only hatch today |
| `GET /v1/checkouts/{id}` as the poll door | Many agents cannot expose HTTPS for Plane C |
| Stable `Idempotency-Key` on mint + refund | Agents retry; host already supports this |
| Optional `GET /v1/orgs/{orgId}/events?after=` **later** | Cursor of outbound envelopes so agents need not host a webhook. Skip in v1 if poll is enough |

What not to add on REST:

- OAuth for remote MCP in the money host
- A second JSON dialect
- Agent-only “mark paid”

Auth stays: `Authorization: Bearer lzr_sk_…` + org in path/body. JWT is for humans.
`X-Lazuar-Tenant-Id` is a hint forwarded to One; it does not authorize (032/13).

---

## 5. CLI — Stripe-CLI shape, HTTP client

Binary name: `lazuar-pay` (client), not more flags on `lazuar-pay-rs serve`.

```text
export LAZUAR_PAY_BASE_URL=http://localhost:8081
export LAZUAR_PAY_API_KEY=lzr_sk_…
export LAZUAR_PAY_ORG_ID=<one tenant id>

lazuar-pay whoami
lazuar-pay ready
lazuar-pay checkout create --provider test --amount 10.00 --currency MYR
lazuar-pay checkout get <id>
lazuar-pay payments list
lazuar-pay receipts list
lazuar-pay refund create --checkout <id> --idempotency-key …
lazuar-pay listen --forward-to http://127.0.0.1:3021/hook   # local only
```

`listen` is the high-value CLI trick (Stripe CLI): receive Plane C on the laptop
without a public URL. Registering webhooks from a CLI is easy to leave pointing at
localhost in staging — `ThrowIfMisconfigured` already forbids that in Production.

- **Testing/Dev:** `listen` may `PUT` a loopback webhook (`OutboundUrl` allow_loopback).
- **Staging/Prod:** CLI only polls `GET /v1/checkouts/{id}` / lists payments; merchants
  keep a real HTTPS webhook.

CLI must print `pay_url` and never print vault secrets. Gateway PUT can exist as
`lazuar-pay gateway put --file` for operators, not as the default happy path.

---

## 6. MCP — few tools, task-shaped, no secrets in args

MCP is how Claude / Cursor / Grok attach. Transport for v1: **stdio** (the agent spawns
the process with env). Remote Streamable-HTTP MCP is a later product (auth, SSRF,
multi-tenant) and must not live in the money host.

### Tools (keep this list short)

| Tool | Maps to | Notes |
|---|---|---|
| `pay_whoami` | `GET /v1/whoami` | Prove the key |
| `pay_ready` | `GET /v1/orgs/{org}/ready` | “Can this shop take money?” |
| `pay_create_checkout` | `POST /v1/checkouts` | Returns `id`, `pay_url`, `status`. Idempotency required |
| `pay_get_checkout` | `GET /v1/checkouts/{id}` | Poll. Buyer JSON: `open` / `paid` / `failed` / `expired` |
| `pay_list_payments` | `GET …/payments` | |
| `pay_list_receipts` | `GET …/receipts` | |
| `pay_create_refund` | `POST …/refunds` | Destructive; tool description must say so |
| `pay_create_payment_link` | `POST /v1/payment-links` | Occupancy / cap |

**Resources (read-only):** `pay://org/{org}/payments`, `pay://checkout/{id}`.

### Do not expose as tools

- `PUT /gateway` (CHIP PEM, Stripe `sk_`, Solana address) — LLM context leak
- `PUT /one-webhook` (`whsec_`)
- Wrap key, metrics token
- `POST /pay/{token}/start` and `/confirm` — buyer
- PSP webhook ingest
- `POST …/refunds/{id}/resolve` without a human (ops hatch)

### Honesty in tool descriptions

If the schema does not say this, the model will lie to the user:

- Exact amount, 2 display decimals (USDC quote included)
- `interval` `mo`/`yr` → 400; recurring billing is not offered (031/01)
- Solana is receive-only USDC; no chain refund
- Status `paid` means a Proof took; there is no admin override
- Unlock on `payment.completed` HMAC, or poll until `paid`

**Confirmation:** MCP `annotations.destructiveHint` on refund; `readOnlyHint` on
list/get. Refunds should require the host’s confirmation UX where available.

### Config (stdio)

```json
{
  "mcpServers": {
    "lazuar-pay": {
      "command": "lazuar-pay-mcp",
      "env": {
        "LAZUAR_PAY_BASE_URL": "https://pay.example",
        "LAZUAR_PAY_API_KEY": "lzr_sk_…",
        "LAZUAR_PAY_ORG_ID": "…"
      }
    }
  }
}
```

The process is a client. It holds the key in env, not in tool arguments.

---

## 7. Bootstrap vs runtime (why the dashboard stays)

| Step | Who | Surface |
|---|---|---|
| Create workspace in One | Human | One app / merchant SPA |
| Mint `lzr_sk_` | Human | Developers page → One (`oneApi.ts`) |
| Paste rail keys (BYOK) | Human | Gateway page → `PUT /gateway` |
| Point Plane C webhook | Human or CLI (dev) | Webhooks page |
| Mint checkout, list money, refund | Agent | REST / CLI / MCP |

Pay cannot mint `lzr_sk_`. An agent cannot fully provision a shop unless **One** also
grows MCP/CLI. That is a different repo. Until then, “connect an AI agent” means: human
pastes three env vars, agent mints `pay_url`s.

Agents also do not replace the buyer. They mint a hosted link; a person still pays.

---

## 8. Auth and blast radius

Same doors as 032/13:

- Machine key is a **writer** for every org it is bound to (`RequireMember` = writer
  for `lzr_sk_`).
- One `/me` cached 60s; revoke is Plane A (`tenant.suspended` / `api_key.revoked`).
- Org in the path is the tenant Pay checks.

For agents, prefer a **narrow One key** (`tenant:read` + `authz:check` only), never
`*`. CLI/MCP should refuse `sk_` / Stripe keys as `Authorization` (host already 401s).

SSRF: outbound webhooks already pin DNS. MCP must not grow a “fetch this URL” tool.

---

## 9. Implementation order

1. **`crates/client`** — hand-typed from TypeSpec (or generated *client* from OpenAPI).
   Tests against the Fake host in `api` tests.
2. **REST docs + honesty scrape includes `lazuar-pay-rs` `router()`.** Cookbook next to
   `examples/pay-node`. Poll `GET /v1/checkouts/{id}` as the no-webhook path.
3. **CLI** `whoami | ready | checkout create/get | payments | receipts`. No `listen` yet.
4. **MCP stdio** with the eight tools above. Fixture tests: tool schema + fake HTTP. No
   live PSP.
5. **CLI `listen`** (Testing loopback only) or skip in favor of poll.
6. **Stop.** Remote MCP, OAuth, event cursor, gateway-via-agent — only after 1–5 are
   boring.

Do not block this on Stage 6 cutover. CLI/MCP can target .NET `:8081` today and Rust
tomorrow; TypeSpec is the contract.

First proof: `crates/client` + CLI `checkout create` against Testing Fake One. That
proves the adapter split before any MCP crate.

---

## 10. Risks if done wrong

| Failure | Why it hurts |
|---|---|
| Secret-in-context | Gateway PUT as an MCP tool is how `sk_live` lands in a chat log |
| In-process MCP on the money binary | Tool panic / blocking LLM HTTP inside `serve` |
| Webhook-only unlock | Agents without a public URL invent “poll the PSP” or ask the user to paste a CHIP receipt. Give them `GET checkout` |
| Too many tools | 1:1 with ~30 routes teaches the model to PUT gateways. Task-shaped tools only |
| Second JSON | CLI printing domain `Settled` instead of wire `paid` confuses every agent |
| Fourth writer | CLI/MCP calling `apply` locally while REST also writes `pay_rs` |

---

## 11. Related files

| Path | Why |
|---|---|
| `packages/pay-spec/main.tsp` | Catalog (~30 ops) |
| `apps/lazuar-pay-rs/crates/api/src/lib.rs` | axum `router` |
| `apps/lazuar-pay-rs/crates/app/src/main.rs` | host roles, not merchant CLI |
| `examples/pay-node/` | existing machine-key hatch |
| `scripts/check-pay-openapi-honesty.mjs` | OpenAPI ↔ .NET `Map*` (Rust not scraped yet) |
| `apps/lazuar-pay-merchant/src/pages/org/ApiKeysPage.tsx` | One key mint (human) |
| `apps/lazuar-pay-rs/crates/api/src/identity.rs` | JWT vs `lzr_sk_` doors |
