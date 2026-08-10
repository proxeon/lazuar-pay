# Local e2e evidence — Hub cashier sample

**Template + partial fill for program 006.**  
**Do not paste full `sk_` / `whsec_` values.**

---

## Run metadata

| Field | Value |
|-------|--------|
| Date | 2026-08-10 |
| Branch | `chore/sample-006` |
| Hub port | **8080** (canonical; not started for this fill) |
| Sample port | **3020** |
| Sample package | `@examples/hub-cashier-next` |
| Operator | local (S53) |

---

## Secrets (redacted)

| Item | Value |
|------|--------|
| Provision | **not run** this session (no Hub) |
| `sk_` | `sk_test_***` (dummy for handler-only path) |
| `whsec_` | `whsec_***` (dummy local secret for HMAC) |
| BYOK | **not configured** (Hub absent) |

---

## Identifiers (ok to store)

| Item | Value |
|------|--------|
| Local order id | `783e6127-54ec-4456-9f0d-ef06861e08ae` |
| Checkout id | `00000000-0000-0000-0000-00000000e2e1` (placeholder; not Hub-created) |
| Delivery id | `00000000-0000-0000-0000-00000000d001` |
| Event id (last) | `1276431f-caa9-420c-a75a-55748941d3ac` |

---

## Tunnel notes

| Hop | Status |
|-----|--------|
| Hop 1 (gateway → Hub) | **Not exercised** — no public Hub / sandbox this session |
| Hop 2 (Hub → sample) | **Simulated** — local `POST http://127.0.0.1:3020/webhooks/hub/payments` with HMAC matching sample `OutboundWebhookSignature` algorithm |
| Browser success/cancel | **Not exercised** (UI path residual) |

---

## Pass / fail checklist

### Curl / handler path (S53.2)

| Step | Result | Notes |
|------|--------|--------|
| Provision (or existing keys) | **skip** | No Hub; dummy `sk_`/`whsec_` for sample only |
| Create checkout with valid sk + BYOK | **skip** | Requires Hub + BYOK → residual ops |
| Create local draft order | **pass** | `POST /api/orders` → status `draft` |
| Fake signed `payment.completed` → order paid | **pass** | `node scripts/send-fake-webhook.mjs` → order `status: paid` |
| Bad signature → 401 | **pass** | HTTP `401` `{"error":"invalid_signature"}` |
| Replay same delivery → single unlock | **pass** | second POST `already: true`; `paidAt` unchanged; still one paid order |
| Webhook unit vectors | **pass** | `pnpm --filter @examples/hub-cashier-next test:webhook` |

### Browser path (S53.3)

| Step | Result | Notes |
|------|--------|--------|
| Create order in UI → redirect to gateway | **blocked** | Needs Hub create-checkout + BYOK |
| Complete sandbox pay | **blocked** | No tunnel / sandbox this session |
| Pass via fake webhook only | **pass** | Documented above |
| Success page alone does not pay | **code-reviewed** | `/pay/success` polls local status only (S43–S46) |
| Cancel path does not pay | **code-reviewed** | `/pay/cancel` messaging only |

### Negative spots (S53.4)

| Step | Result | Notes |
|------|--------|--------|
| `PAYMENTS_NOT_CONFIGURED` when BYOK off | **documented** | Requires Hub; sample maps Hub error on checkout route |
| Missing scope / key | **documented** | Misconfigured sample without `LAZUAR_SK_TEST_KEY` fails server checkout create |

---

## Commands used (handler path)

```bash
# env (gitignored .env.local — dummy values)
# LAZUAR_WEBHOOK_SECRET=whsec_***
# LAZUAR_SK_TEST_KEY=sk_test_***

pnpm --filter @examples/hub-cashier-next test:webhook
pnpm --filter @examples/hub-cashier-next dev   # :3020

curl -sS -X POST http://127.0.0.1:3020/api/orders \
  -H "Content-Type: application/json" \
  -d '{"amount":25,"currency":"MYR","customer_email":"guest@example.com","description":"S53 fake webhook e2e"}'

ORDER_ID=… CHECKOUT_ID=00000000-0000-0000-0000-00000000e2e1 \
  DELIVERY_ID=00000000-0000-0000-0000-00000000d001 \
  node examples/hub-cashier-next/scripts/send-fake-webhook.mjs

# bad signature
curl -sS -o /dev/null -w "%{http_code}\n" -X POST http://127.0.0.1:3020/webhooks/hub/payments \
  -H "Content-Type: application/json" \
  -H "X-Lazuar-Signature: t=1,v1=deadbeef" \
  --data-binary '{"id":"x","event_type":"payment.completed","data":{}}'
# → 401
```

---

## Residual ops (not open sample code debt)

1. **Real sandbox e2e:** Hub on `:8080`, provision non-aura product, Ops BYOK, public Hub for hop 1, pay sandbox, confirm outbound delivery unlocks sample order.  
2. **Browser path:** `/pay` → hosted checkout → success poll → paid only after Hub webhook.  
3. Optional: mprocs entry for sample with **autostart false**.  
4. Optional later: Mermaid plugin (docs wave chose ASCII).  
5. TypeSpec envelope honesty gap (runtime envelope vs flat DTO) — docs note already; not sample-blocking.

---

## Verdict

| Scope | Status |
|-------|--------|
| Sample code path (verify + unlock + idempotency + 401) | **green** |
| Full multi-hop sandbox with Hub | **residual ops** |
| Program 006 implementable code+docs | **closable** with residual above |
