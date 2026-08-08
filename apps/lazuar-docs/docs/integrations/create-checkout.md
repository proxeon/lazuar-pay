# Create a checkout

Creates a hosted payment session and returns a URL for the guest.

## Endpoint

```http
POST /api/v1/integrations/payments/checkouts
Authorization: Bearer sk_test_…
```

Requires scope: **`payments.checkouts:write`**.

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

## Poll status (optional)

```http
GET /api/v1/integrations/payments/checkouts/{checkout_id}
Authorization: Bearer sk_test_…
```

Scope: **`payments.checkouts:read`**.  
Use for UX “processing…” pages; still verify webhooks for unlocks.

## Errors

See [Error codes](/reference/error-codes). Common: `PAYMENTS_NOT_CONFIGURED` when BYOK missing.

## Next

[Webhooks](/integrations/webhooks) — unlock domain on `payment.completed`.
