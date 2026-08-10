# Concepts

## Workspace (tenant)

A Hub **workspace** (organization) is the isolation boundary for:

- Payment gateway credentials (BYOK)
- API keys
- Outbound webhook endpoints
- Commerce / billing data (if used)

Rule of thumb: **one workspace per tenant of your product**  
(e.g. one Aura salon org → one Hub workspace).

## BYOK (bring your own keys)

Gateway API keys (Billplz collection, Stripe secret, …) live in Hub’s vault for that workspace.  
Money settles in the **merchant’s** processor account. Hub is not Merchant of Record for guest GMV.

## Machine API key

- Prefix: `sk_test_` / `sk_live_`
- Auth: `Authorization: Bearer sk_…`
- Bound to **one** workspace
- Scoped (least privilege)

Never put machine keys in browser or mobile apps.

## Integration checkout session

Server-side record created when your app calls M2M checkout:

- Stores amount, metadata, gateway session id
- Survives provider metadata quirks (e.g. Billplz)
- Status polled via GET checkout

## Outbound webhook

Hub POSTs signed events to **your** HTTPS (or tunnel) URL:

- `payment.completed`
- `payment.failed`
- (refunds: maturing)

Unlock domain state only after verification + business rules.

## Provision

Server-to-server bootstrap of a workspace:

- Identity: `(external_product, external_org_id)`
- Returns one-time API key (+ optional webhook secret)
- Idempotent re-call does not remint secrets

## Dual money systems (Aura-specific, but educational)

| Path | Who pays whom | Provider |
|------|---------------|----------|
| Guest → salon | Customer → merchant | Hub BYOK |
| Salon → platform | Tenant → Aura company | Paddle (outside Hub) |

## Why Hub (cashier)

For multi-gateway or multi-tenant apps, Hub holds BYOK credentials and normalizes one webhook signature so your app does not re-implement Billplz/Stripe verify per processor. Full comparison: [Hub vs DIY gateways](/integrations/hub-vs-diy). Ownership matrices: [Architecture: who does what](/guide/architecture-who-does-what).

## Related

- [Product lines](/guide/product-lines)
- [Payments cashier](/integrations/payments-cashier)
- [Architecture: who does what](/guide/architecture-who-does-what)
- [Hub vs DIY](/integrations/hub-vs-diy)
