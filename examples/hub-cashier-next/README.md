# Hub Cashier Sample (Next.js)

**Package:** `@examples/hub-cashier-next`  
**Port:** `3020`  
**Status:** scaffold only (S31) — shell routes, no Hub calls yet.

Teachable Next.js App Router sample that will prove Lazuar Hub as a multi-app payments cashier. **Not production software.**

## Disclaimer

- Local / demo only. In-memory store (later) is single-process.
- Fulfillment must use **signed Hub webhooks** — never unlock on `success_url` alone.
- Holds **no** Billplz/Stripe long-term secrets; talks to Hub only.
- No `@repo/api-types-ts`, no gateway SDKs — plain `fetch` + local types (later phases).

## Start

From monorepo root:

```bash
pnpm install
pnpm example:cashier
# or
pnpm --filter @examples/hub-cashier-next dev
```

Open http://localhost:3020

## Routes (scaffold)

| Path | Role |
|------|------|
| `GET /` | Landing |
| `GET /pay` | Pay UI placeholder |
| `GET /pay/success` | `success_url` target |
| `GET /pay/cancel` | `cancel_url` target |
| `POST /api/checkout` | Checkout stub (501) |
| `POST /webhooks/hub/payments` | Hub webhook stub (501, `runtime = "nodejs"`) |

Product turbo scripts exclude `@examples/*` — see [`../README.md`](../README.md).

Env, orders, checkout, and webhook verify land in S40–S45.
