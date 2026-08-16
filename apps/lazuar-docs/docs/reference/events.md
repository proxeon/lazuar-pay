# Event catalog (v1)

**Catalog v1** · API `/api/v1` · OpenAPI `1.0.0`.

This page is the **normative list of outbound events the Hub dispatcher posts today**. Additive types are non-breaking; endpoints that filter by name must opt in to new names. See [API versioning](/reference/openapi) and the repo policy `docs/api-versioning.md`.

Do not treat Scalar product pages as the catalog. How to verify hop-2 deliveries: [Webhooks](/integrations/webhooks).

## Delivery

Every workspace-dispatched family uses the same envelope:

```json
{
  "id": "<uuid v7>",
  "event_type": "payment.completed",
  "created_at": "<iso-8601>",
  "data": { }
}
```

Headers:

| Header | Value |
|--------|--------|
| `X-Lazuar-Signature` | `t=<unix>,v1=<hmac-sha256-hex>` of `{t}.{rawBody}` |
| `X-Lazuar-Event` | Same as `event_type` |
| `X-Lazuar-Delivery-Id` | This delivery attempt |
| `X-Lazuar-Webhook-Id` | Endpoint id |

`data` keys below are the **runtime builders**, not TypeSpec `PaymentWebhookPayloadDto` (that DTO is not the wire envelope).

**Do not mix families.** A Payments cashier checkout emits `payment.*`. A Hub Commerce product emits `order.completed` / `subscription.*` / `payment_link.paid`. LHDN poller emits `invoice.valid` / `invoice.invalid`. Unlock the domain that actually charged.

## Payments (M2M cashier)

| event_type | When | Your action | `data` keys |
|------------|------|-------------|-------------|
| `payment.completed` | Integrator checkout captured (`POST /integrations/payments/checkouts`) | Unlock **once** (idempotent on `checkout_id` + `gateway_transaction_id`) | `event_id`, `checkout_id`, `gateway`, `gateway_transaction_id`, `provider_session_id`, `amount`, `currency`, `status`, `metadata`, `description`, `customer_email` |
| `payment.failed` | Integrator checkout failed at the gateway | Do not unlock | Same keys; `status` is failed |

## Commerce (Hub products / hosted links)

| event_type | When | Your action | `data` keys |
|------------|------|-------------|-------------|
| `subscription.activated` | First paid period or recovery that lands `ACTIVE` | Unlock SaaS access | `subscription_id`, `client_profile_id`, `customer_id`, `product_id`, `status`, `current_period_end?`, `customer_email?`, `amount?`, `currency?`, `interval?`, `is_first_payment?`, `metadata?`, `checkout_url?` |
| `subscription.resumed` | Recovered from `SUSPENDED` | Restore access | Same as activated |
| `subscription.past_due` | Renewal failed; collection in progress | Restrict or warn | Same as activated |
| `subscription.canceled` | Immediate cancel, period-end finalize, dunning terminal, or PDPA | Revoke access | Same as activated |
| `subscription.suspended` | Dunning terminal suspend | Revoke until recovered | Same as activated |
| `order.completed` | One-time Hub product settled | Fulfill once | `order_id`, `client_profile_id`, `product_id`, `status`, `quantity` |
| `payment_link.paid` | Custom / ad-hoc Hub payment link settled | Fulfill that invoice/link | `checkout_session_id` (and amount/customer when present) |

Use these only if you sell **Hub Commerce** products. A Payments cashier must not wait for `subscription.activated`.

## LHDN

| event_type | When | Your action | `data` keys |
|------------|------|-------------|-------------|
| `invoice.valid` | MyInvois poller: document `VALID` | Store UUID / show QR | `internal_id`, `lhdn_uuid`, `status`, `qr_link`, `error_message` |
| `invoice.invalid` | MyInvois poller: document `INVALID` | Surface error; do not treat as filed | Same |

## Not in v1

Do **not** subscribe expecting these. They are not emitted by the current dispatcher.

| Name | Note |
|------|------|
| `payment.refunded` | Refunds exist in Ops / adapters; no outbound event yet |
| `invoice.submitted` | Poller does not emit this |
| `invoice.cancelled` | Poller does not emit this |
| `subscription.updated` | Explicitly forbidden — use the discrete status events above |

When you add a new event type, update **this page in the same PR**.
