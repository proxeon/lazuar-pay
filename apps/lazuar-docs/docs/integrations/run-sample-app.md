# Run sample app

**Status:** draft (code complete; full sandbox e2e optional — see evidence notes)  
**Audience:** Integrators proving Hub as a multi-app cashier without Aura.  
**Monorepo path:** `examples/hub-cashier-next` · package `@examples/hub-cashier-next` · port **3020**

## What it proves

| Proves | Does not prove |
|--------|----------------|
| Server-side Hub M2M `POST …/integrations/payments/checkouts` | Production multi-instance store |
| Redirect to Hub hosted checkout | Billplz/Stripe DIY in your app |
| Signed webhook unlock (`payment.completed` only) | Commerce / LHDN / subscriptions |
| Runtime **envelope + `data`** webhook shape | Durable Postgres / HA |
| No gateway SDKs — plain `fetch` | Using `@repo/api-types-ts` |

**Fulfillment rule:** mark paid **only** after a verified Hub webhook. Never unlock on `success_url` alone.

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

**Summary:** Domain (toy orders) lives in the sample. Money rails stay on Hub. Hop 2 can be local (`127.0.0.1:3020`); hop 1 (gateway → Hub) needs a public Hub URL for real sandbox pay.

## Prerequisites

| Need | Notes |
|------|--------|
| Hub API | `http://localhost:8080` → base `$HUB=http://localhost:8080/api/v1` |
| Provision auth | `INTEGRATOR_PROVISION_SECRET` **or** Ops-minted `sk_` + `whsec_` |
| BYOK | Ops → Payment settings → active gateway for the workspace (or checkout returns `PAYMENTS_NOT_CONFIGURED`) |
| Node / pnpm | Node 18+; monorepo root `pnpm install` |
| Tunnel (sandbox only) | Public Hub for gateway callbacks (hop 1). Local hop 2 does not need a tunnel if Hub can reach `127.0.0.1:3020` |

## 1. Start Hub

```bash
# from monorepo root — your usual stack
task infra:up
task dev
# API must listen on :8080 (canonical)
```

Confirm health at `http://localhost:8080` (or your ops health route). Do **not** default to **8090** — that is historical drift only.

## 2. Get secrets (provision)

Use a **non-aura** `external_product` so multi-app cashier evidence is real. Webhook path for this sample is exact:

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

| Response field | Sample env |
|----------------|------------|
| `api_key.plain_key` | `LAZUAR_SK_TEST_KEY` |
| `webhook.secret_key` | `LAZUAR_WEBHOOK_SECRET` (full `whsec_…` string — **do not strip** prefix) |

Secrets are returned **once** on first materialization. Re-provision of the same `(external_product, external_org_id)` does not re-print them.

Curl-only twin of this flow: monorepo `plans/006-sample/harness/second-app-proof.md`.

## 3. Configure BYOK (human, Ops)

In Hub Ops for the new workspace: Payment settings → add/activate test gateway (Billplz / Stripe / …).

Without this, create-checkout returns **`PAYMENTS_NOT_CONFIGURED`**. The sample cannot fix that from `.env` alone — money rails stay on Hub.

## 4. Install and run the sample

From monorepo root:

```bash
pnpm install
cp examples/hub-cashier-next/.env.example examples/hub-cashier-next/.env.local
# edit .env.local: LAZUAR_SK_TEST_KEY, LAZUAR_WEBHOOK_SECRET, NEXT_PUBLIC_APP_URL

pnpm example:cashier
# or
pnpm --filter @examples/hub-cashier-next dev
```

Open `http://localhost:3020` (or `http://127.0.0.1:3020`).

| Variable | Role |
|----------|------|
| `LAZUAR_HUB_BASE_URL` | Hub base **including** `/api/v1` (default `http://localhost:8080/api/v1`) |
| `LAZUAR_SK_TEST_KEY` | Machine key `sk_…` — **server only** |
| `LAZUAR_WEBHOOK_SECRET` | Full `whsec_…` HMAC material — **server only** |
| `NEXT_PUBLIC_APP_URL` | Absolute sample base for success/cancel (`http://127.0.0.1:3020`) |

**Never** put `sk_` or `whsec_` behind `NEXT_PUBLIC_`. Never commit real secrets.

Default product turbo scripts **exclude** `@examples/*` — the sample is optional for day-to-day monorepo CI.

## 5. Create checkout / pay sandbox

1. Open `/pay` → amount ≥ 2.00 MYR → **Pay with Hub**.
2. Guest is redirected to Hub hosted checkout (`checkout_url`).
3. Complete (or cancel) sandbox payment on the gateway.
4. Browser lands on `/pay/success` or `/pay/cancel`. Success page **polls local order status** and does **not** unlock.

For hop 1 (gateway → Hub) you need a **public Hub** base (`App:ApiBaseUrl` / tunnel). If sandbox pay is blocked, use the fake webhook path below after a checkout (or draft order) exists.

## 6. Verify webhook unlock + replay

**Real path:** sandbox pay → Hub outbound worker POSTs `/webhooks/hub/payments` → sample verifies HMAC → order `paid`.

**Offline / handler path** (after an order + checkout id exist):

```bash
cd examples/hub-cashier-next
ORDER_ID=<uuid> CHECKOUT_ID=<hub-checkout-id> \
  node scripts/send-fake-webhook.mjs
```

Unit vectors (no Next server):

```bash
pnpm --filter @examples/hub-cashier-next test:webhook
```

**Replay:** send the same `X-Lazuar-Delivery-Id` twice → second response is `already: true`; order stays paid once (no double unlock).

Signature: header `X-Lazuar-Signature: t=<unix>,v1=<hex>`; signed payload `{t}.{rawBody}`; HMAC-SHA256 with full `whsec_…` UTF-8 secret.

## 7. Troubleshooting

| Symptom | Fix |
|---------|-----|
| `PAYMENTS_NOT_CONFIGURED` | Ops → enable active BYOK gateway on the workspace |
| Signature fail / **401** | `LAZUAR_WEBHOOK_SECRET` must match provision; keep full `whsec_`; raw body only (`request.text()`) |
| Checkout **500** / misconfigured | Set `LAZUAR_SK_TEST_KEY` in `.env.local` |
| `IDEMPOTENCY_CONFLICT` | New order if fields changed under same idempotency key |
| Order never paid after browser success | Hop 1 or hop 2 broken — success page alone is not fulfillment |
| MYR validation error | Amount ≥ 2.00 |
| Port clash | Sample is **3020** (not product 3002–3005) |
| Wrong Hub port | Prefer **8080**; 8090 is alternate/historical only |

## Test vs live keys

| Key | Use |
|-----|-----|
| `sk_test_…` | **Default** for local sample and sandbox |
| `sk_live_…` | Production only; never in committed env files |

Prefer `is_test_mode: true` at provision outside production. Treat live keys like production credentials.

## Related

- [Payment flow](/integrations/payment-flow) — canonical E2E  
- [Architecture: who does what](/guide/architecture-who-does-what) — ownership matrices  
- [Second-app checklist](/integrations/second-app-checklist) — independence bar  
- [Payments cashier](/integrations/payments-cashier) — M2M overview  
- [Provision](/integrations/provision) · [Create checkout](/integrations/create-checkout) · [Webhooks](/integrations/webhooks)  
- App README: monorepo `examples/hub-cashier-next/README.md`  
- Curl harness: monorepo `plans/006-sample/harness/second-app-proof.md`  
- Evidence template: monorepo `plans/006-sample/evidence/local-e2e.md`
