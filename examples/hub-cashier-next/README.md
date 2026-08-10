# Hub Cashier Sample (Next.js)

**Package:** `@examples/hub-cashier-next`  
**Port:** `3020`  
**Status:** S40–S46 implemented (env, orders, checkout, UI, webhook verify + fulfill)

Teachable Next.js App Router sample that proves **Lazuar Hub** as a multi-app payments cashier. **Not production software.**

---

## What this proves / does not prove

| Proves | Does not prove |
|--------|----------------|
| Server-side Hub M2M `POST …/integrations/payments/checkouts` | Production multi-instance store |
| Redirect to Hub hosted checkout | Billplz/Stripe DIY integration |
| Signed webhook unlock (`payment.completed`) | Commerce / LHDN / subscriptions |
| Envelope + `data` payload honesty | Durable Postgres / HA |
| No gateway SDKs — plain `fetch` | Using `@repo/api-types-ts` |

**Fulfillment rule:** mark paid **only** after verified Hub webhook. Never unlock on `success_url` alone.

---

## Prerequisites

1. **Hub API** on `http://localhost:8080` (`task dev` or your usual stack).
2. **Keys:** provision secret **or** pasted `sk_test_…` + `whsec_…` (Ops mint).
3. **BYOK:** Ops → Payment settings → active gateway (Billplz/Stripe/…) for that workspace. Without this, checkout returns `PAYMENTS_NOT_CONFIGURED`.
4. **Tunnel** for real sandbox pay (Hop 1: gateway → Hub). Local webhook Hop 2 can use `http://127.0.0.1:3020/webhooks/hub/payments`.

---

## Quick start

From monorepo root:

```bash
pnpm install
cp examples/hub-cashier-next/.env.example examples/hub-cashier-next/.env.local
# edit .env.local with sk_ / whsec_ / NEXT_PUBLIC_APP_URL

pnpm example:cashier
# or
pnpm --filter @examples/hub-cashier-next dev
```

Open http://localhost:3020 (or http://127.0.0.1:3020).

---

## Environment

See [`.env.example`](./.env.example).

| Variable | Role |
|----------|------|
| `LAZUAR_HUB_BASE_URL` | Hub base **including** `/api/v1` (default `http://localhost:8080/api/v1`) |
| `LAZUAR_SK_TEST_KEY` / `LAZUAR_API_KEY` | Machine key `sk_…` — **server only** |
| `LAZUAR_WEBHOOK_SECRET` | Full `whsec_…` string (HMAC key material; **do not strip** prefix) |
| `NEXT_PUBLIC_APP_URL` | Absolute sample base for success/cancel (`http://127.0.0.1:3020`) |

**Never** put `sk_` or `whsec_` behind `NEXT_PUBLIC_`. Never log full secrets.

---

## Provision (one-time)

Use a **non-aura** `external_product` so multi-app cashier evidence is real.

Webhook URL for this sample (exact path):

```text
http://127.0.0.1:3020/webhooks/hub/payments
```

```bash
export HUB=http://localhost:8080/api/v1
export INTEGRATOR_PROVISION_SECRET=…   # from Hub config

curl -sS -X POST "$HUB/one/integrations/workspaces/provision" \
  -H "Content-Type: application/json" \
  -H "X-Lazuar-Provision-Key: $INTEGRATOR_PROVISION_SECRET" \
  -d '{
    "external_product": "sample-shop",
    "external_org_id": "local-dev-1",
    "display_name": "Hub Cashier Sample",
    "is_test_mode": true,
    "webhook_url": "http://127.0.0.1:3020/webhooks/hub/payments"
  }'
```

Map once (secrets only on first materialization):

| Response | `.env.local` |
|----------|--------------|
| `api_key.plain_key` | `LAZUAR_SK_TEST_KEY` |
| `webhook.secret_key` | `LAZUAR_WEBHOOK_SECRET` |

Then enable **BYOK** in Hub Ops for the new workspace.

---

## Architecture

```text
Browser  →  Sample (:3020)  →  Hub (:8080/api/v1)
   │              │                    │
   │         local order            create checkout
   │         .data/                    │
   │              │                    ▼
   │              │            hosted checkout URL
   │              │◄── redirect ───────┘
   │
   │   success_url  →  /pay/success  (poll local; never unlock)
   │
Hub worker ──POST /webhooks/hub/payments──► verify HMAC → mark paid
```

- **Domain** (orders) lives in the sample under **`.data/`** (file-backed JSON, gitignored).
- **Money rails** stay on Hub; sample holds no Billplz/Stripe long-term secrets.
- Product turbo scripts exclude `@examples/*` — see [`../README.md`](../README.md).

---

## Routes

| Method | Path | Role |
|--------|------|------|
| `GET` | `/` | Landing |
| `GET` | `/pay` | Create order + start checkout |
| `GET` | `/pay/success` | `success_url` — polls local status; **never unlocks** |
| `GET` | `/pay/cancel` | `cancel_url` — cancelled messaging |
| `GET` | `/orders` | List local orders |
| `GET` | `/orders/[id]` | Order detail / status badges |
| `POST` | `/api/checkout` | Create local order (optional) + Hub checkout |
| `GET/POST` | `/api/orders` | List / create draft without Hub |
| `GET` | `/api/orders/[id]` | JSON for polling |
| `POST` | `/webhooks/hub/payments` | Hub webhook (raw body + HMAC) |

---

## Demo path

1. Start Hub + sample; fill `.env.local`.
2. Open `/pay` → amount ≥ 2 MYR → **Pay with Hub**.
3. Complete (or cancel) hosted checkout.
4. On success page, status stays non-paid until webhook.
5. Real path: pay in sandbox → Hub delivers webhook → order `paid`.
6. Offline path: after a checkout creates an order, run:

```bash
cd examples/hub-cashier-next
ORDER_ID=<uuid> CHECKOUT_ID=<hub-checkout-id> \
  node scripts/send-fake-webhook.mjs
```

Fake webhook is **dev only** and does not replace sandbox pay for full e2e.

---

## Webhook signature

Matches Hub `OutboundWebhookSignature.cs`:

- Header: `X-Lazuar-Signature: t=<unix>,v1=<hex>`
- Signed payload: `{t}.{rawBody}`
- HMAC-SHA256 with **full** `whsec_…` UTF-8 secret (keep prefix)
- Skew default 300s; constant-time compare

Unit vectors (no Next server):

```bash
pnpm --filter @examples/hub-cashier-next test:webhook
# or
node examples/hub-cashier-next/scripts/test-webhook-verify.mjs
```

---

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| `PAYMENTS_NOT_CONFIGURED` | Ops → enable active BYOK gateway on workspace |
| Signature fail / 401 | `LAZUAR_WEBHOOK_SECRET` must match provision; use full `whsec_`; raw body only |
| Checkout 500 `MISCONFIGURED` | Set `LAZUAR_SK_TEST_KEY` in `.env.local` |
| `IDEMPOTENCY_CONFLICT` | Create a new order (fields changed under same key) |
| Order never paid after browser success | Hop 1 (gateway→Hub) or Hop 2 (Hub→sample webhook) broken — success page alone is not fulfillment |
| MYR validation | Use amount ≥ 2.00 |
| Port clash | Sample is **3020** (not 3005) |

---

## Security checklist

- [x] No `NEXT_PUBLIC_` for `sk_` / `whsec_`
- [x] No Billplz/Stripe SDK dependencies
- [x] No processor secrets required on sample runtime path
- [x] Webhook uses `request.text()` then verify; `runtime = "nodejs"`
- [x] Success page does not set paid
- [x] Never log full secrets
- [x] Sample badge “not production” in layout

---

## Scripts

| Script | Command |
|--------|---------|
| Dev | `pnpm --filter @examples/hub-cashier-next dev` |
| Typecheck | `pnpm --filter @examples/hub-cashier-next check-types` |
| Webhook unit vectors | `pnpm --filter @examples/hub-cashier-next test:webhook` |
| Fake signed webhook | `node scripts/send-fake-webhook.mjs` |

---

## Docs

When the docs runbook exists: Integrations → **Run sample app** in `lazuar-docs` (S50). Until then, this README is the operator guide.

---

## Disclaimer

Local / demo only. File store under `.data/` is single-process. Multi-worker and serverless cold starts will not share orders. Do not deploy as production merchant software.
