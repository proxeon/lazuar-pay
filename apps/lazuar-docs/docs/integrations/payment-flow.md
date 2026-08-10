# Payment flow

**Product:** **Payments (M2M)** cashier — ad-hoc amount + metadata → hosted gateway page → signed `payment.*` webhooks.

**Not this page:** Commerce (`subscription.*`, catalog), LHDN (`invoice.*`), or Paddle / MoR SaaS billing.

<!-- source: IntegrationEndpoints + OutboundWebhook* ; diagrams: plans/006-sample/01 SEQ-E2E-CASHIER -->

## End-to-end sequence

Canonical path for a second app integrating Hub as BYOK cashier.

```text
Ops human     Your app              Lazuar Hub                 Gateway              Guest browser
   |              |                      |                        |                      |
   |              |-- POST /api/v1/one/integrations/workspaces/provision -------------->|
   |              |   (X-Lazuar-Provision-Key)                                           |
   |              |<-- workspace_id, sk_test_… (once), whsec_… (once) ------------------|
   |              |                      |                        |                      |
   |-- Configure BYOK on workspace (Ops UI — not M2M) -->|        |                      |
   |              |                      | TenantPaymentConfiguration active             |
   |              |                      |                        |                      |
   |              |-- POST /api/v1/integrations/payments/checkouts -------------------->|
   |              |   Bearer sk_… + Idempotency-Key               |                      |
   |              |                      |-- create bill / session -------------------->|
   |              |                      |<-- provider_session_id + hosted URL ---------|
   |              |<-- checkout_id, checkout_url, status=open ----|                      |
   |              |                      |                        |                      |
   |              |-- redirect guest to checkout_url ---------------------------------->|
   |              |                      |                        |<-- pay on hosted page|
   |              |                      |<-- inbound provider webhook (public Hub URL) -|
   |              |                      |-- mark IntegrationCheckoutSession completed ->|
   |              |<-- POST your webhook_url ---------------------|                      |
   |              |   X-Lazuar-Signature: t=…,v1=…                |                      |
   |              |   X-Lazuar-Event: payment.completed           |                      |
   |              |-- verify HMAC + unlock domain --------------->|                      |
   |              |                      |                        |                      |
   |              |<-- optional success_url (UX only — NOT fulfillment) ----------------|
```

**Summary:** Your app provisions a workspace (secrets once), an Ops human activates gateway BYOK, then the app creates a checkout and redirects the guest. The gateway notifies **Hub** (hop 1); Hub signs and posts to **your** webhook (hop 2). Unlock domain only after verified `payment.completed`. Browser `success_url` is UX only — never fulfillment.

### Dual hops (inbound vs outbound)

```text
Gateway  --hop1-->  Hub public base (App:ApiBaseUrl)
Hub      --hop2-->  Your app webhook_url (signed POST)

Guest browser  -->  checkout_url on gateway  (NOT a webhook hop)
```

## Step map

| # | Step | Detail | Guide |
|---|------|--------|-------|
| 1 | Choose product | Payments cashier M2M, not Commerce / LHDN / Paddle | [Product lines](/guide/product-lines) |
| 2 | Provision workspace | `POST /api/v1/one/integrations/workspaces/provision` | [Provision](/integrations/provision) |
| 3 | Store secrets once | `sk_test_` / `sk_live_`, `whsec_` | [API keys](/integrations/api-keys) |
| 4 | Human BYOK | Ops UI — not machine path | [Payments cashier](/integrations/payments-cashier) |
| 5 | Create checkout | `POST /api/v1/integrations/payments/checkouts` + `Idempotency-Key` | [Create a checkout](/integrations/create-checkout) |
| 6 | Redirect guest | Send browser to `checkout_url` | Create checkout |
| 7 | Public URLs | Hop 1 public Hub; hop 2 Hub→you | [Environments](/integrations/environments) |
| 8 | Fulfill webhook | Verify `X-Lazuar-Signature`, unlock on `payment.completed` | [Webhooks](/integrations/webhooks) |
| 9 | Prove independence | No Aura imports / shared DB | [Second-app checklist](/integrations/second-app-checklist) |

## Paths & headers (exact)

| Item | Value |
|------|--------|
| Provision | `POST /api/v1/one/integrations/workspaces/provision` |
| Create checkout | `POST /api/v1/integrations/payments/checkouts` |
| Optional status | `GET /api/v1/integrations/payments/checkouts/{checkout_id}` |
| Outbound headers | `X-Lazuar-Signature`, `X-Lazuar-Event`, `X-Lazuar-Delivery-Id`, `X-Lazuar-Webhook-Id` |
| Signature shape | `X-Lazuar-Signature: t=<unix>,v1=<hex>` over `{t}.{raw_body}` |

## Failed path / replay (mini)

```text
payment.failed  →  mark domain failed / do NOT unlock
replay same delivery  →  handler idempotent → 2xx, no double credit
same Idempotency-Key + same body  →  Hub replays prior checkout session
same Idempotency-Key + different body  →  409 IDEMPOTENCY_CONFLICT
no active BYOK  →  422 PAYMENTS_NOT_CONFIGURED
```

## Non-goals

- Commerce lifecycle events (`subscription.*`, `order.completed`)
- LHDN e-invoice (`invoice.*`)
- Paddle / merchant-of-record SaaS seat billing
- App calling Billplz/Stripe SDKs directly (Hub holds BYOK)

## Related

- [Payments cashier (M2M)](/integrations/payments-cashier) — task-oriented twin  
- [Integrations overview](/integrations/) — guide map  
- [Webhooks](/integrations/webhooks) — hop 2 + envelope  
- [Environments & public URLs](/integrations/environments) — hop reachability  
