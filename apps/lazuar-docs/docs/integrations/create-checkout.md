# Create a checkout

Creates a hosted payment session and returns a URL for the guest.

**Product:** Payments (M2M) only — not Commerce catalog paths.

## Endpoint

```http
POST /api/v1/integrations/payments/checkouts
Authorization: Bearer sk_test_…
```

Requires scope: **`payments.checkouts:write`**.  
Machine auth only: `Authorization: Bearer sk_test_…` or `sk_live_…` (not browser cookies).

## Sequence

```text
Your app                    Hub Payments                 Active gateway           Guest
   |                              |                            |                    |
   |-- POST /integrations/payments/checkouts ----------------->|                    |
   |   Authorization: Bearer sk_…                             |                    |
   |   Idempotency-Key (preferred)                            |                    |
   |                              |-- scope payments.checkouts:write               |
   |                              |-- validate amount/currency/URLs                |
   |                              |-- fingerprint for idempotency                  |
   |                              |                            |                    |
   |         [same key + same fingerprint]                     |                    |
   |<-- replay prior session ---------------------------------|                    |
   |         [same key + different body]                       |                    |
   |<-- 409 IDEMPOTENCY_CONFLICT ------------------------------|                    |
   |         [no active BYOK]                                  |                    |
   |<-- 422 PAYMENTS_NOT_CONFIGURED ---------------------------|                    |
   |         [OK]                                              |                    |
   |                              |-- generate hosted session -------------------->|
   |                              |<-- checkout_url -------------------------------|
   |<-- 200 open + checkout_url + checkout_id -----------------|                    |
   |-- 302/redirect checkout_url ------------------------------------------------->|
   |                              |                            |<-- pay UI ---------|
   |                              |                            |                    |
   Note: success_url is UX only — wait for signed webhook to unlock
```

**Summary:** Your server posts amount/currency/URLs with a machine key and optional `Idempotency-Key`. Hub creates a gateway hosted session and returns `checkout_url`. Redirect the guest; do not treat browser return as paid. Missing BYOK yields `PAYMENTS_NOT_CONFIGURED`.

Amounts are **major units** (e.g. `25.00` MYR), not integer cents.

Full cashier path: [Payment flow](/integrations/payment-flow).

## Checkout status (session state)

```text
                 create checkout
                      |
                      v
                    open ----+----> completed   (payment.completed webhook)
                      |      |
                      |      +----> failed      (payment.failed webhook)
                      |
                      +----> (optional expired — product/gateway dependent)
```

Optional poll for UX only:

```http
GET /api/v1/integrations/payments/checkouts/{checkout_id}
Authorization: Bearer sk_test_…
```

Scope: **`payments.checkouts:read`**.  
Still verify webhooks for unlocks — never unlock on `success_url` alone.

```text
success_url hit  →  show "thanks / processing" UI only
webhook verified payment.completed  →  unlock domain
poll GET status  →  optional spinner / UX, not fulfillment SSoT
```

## Example

```bash
export HUB=http://localhost:8090/api/v1
export SK_TEST_KEY=sk_test_…

curl -sS -X POST "$HUB/integrations/payments/checkouts" \
  -H "Authorization: Bearer $SK_TEST_KEY" \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: demo-order-42" \
  -d '{
    "amount": 25.00,
    "currency": "MYR",
    "description": "Demo order 42",
    "customer_email": "guest@example.com",
    "success_url": "https://your-app.example/pay/success",
    "cancel_url": "https://your-app.example/pay/cancel",
    "metadata": {
      "order_id": "ord_42",
      "type": "demo_order"
    }
  }'
```

## Response (typical)

```json
{
  "checkout_id": "…",
  "checkout_url": "https://…",
  "gateway": "BILLPLZ",
  "status": "open",
  "expires_at": null
}
```

Redirect the guest to **`checkout_url`**.

## Rules

1. **Idempotency** — Same `Idempotency-Key` (or body field) for retries → same session, not double charge.  
2. **Minimum amount** — Gateways may reject tiny amounts (e.g. RM 2).  
3. **Gateway** — Optional `gateway_name`; else workspace active/default. Never silent wrong gateway.  
4. **Metadata** — Opaque string map. Put **your** domain ids here. Hub may stamp `checkout_id`, `hub_workspace_id`, `tenant_id`.  
5. **Not paid yet** — Browser hit on `success_url` is **not** fulfillment.  
6. **Auth** — Bearer `sk_test_` / `sk_live_` only for the machine path (no Commerce public buy-link confusion).

## Errors

See [Error codes](/reference/error-codes). Common: `PAYMENTS_NOT_CONFIGURED` when BYOK missing; `IDEMPOTENCY_CONFLICT` when key reused with a different body.

## Next

[Webhooks](/integrations/webhooks) — unlock domain on `payment.completed`.  
[Payment flow](/integrations/payment-flow) — end-to-end sequence SSoT.
