# Payments cashier (M2M)

**Audience:** Any server app using Hub as a **BYOK cashier**.  
**Not for:** Commerce catalog, LHDN, Paddle SaaS.

## What you get

1. Hosted payment page (Billplz / Stripe / CHIP / Razorpay depending on workspace config)  
2. Server-side checkout session with metadata  
3. Signed webhooks when payment completes or fails  

## Prerequisites

1. Hub API base URL (include `/api/v1` in examples below as `$HUB`).  
2. Provision secret **or** SUPER_ADMIN access.  
3. Active gateway BYOK on the workspace.  
4. Public URL for **your** webhook receiver.  
5. For sandbox processors: Hub inbound webhook URL must be reachable (tunnel in local).

## End-to-end in one page

### 1) Provision

See [Provision a workspace](/integrations/provision).

Store once:

- `api_key.plain_key` → `SK_TEST_KEY`  
- `webhook.secret_key` → `WHSEC` (if webhook_url supplied)

### 2) Create checkout

See [Create a checkout](/integrations/create-checkout).

Redirect guest to `checkout_url`.

### 3) Fulfill on webhook

See [Webhooks](/integrations/webhooks).

Only after valid signature + `payment.completed` → unlock your domain object.

## Do / don’t

| Do | Don’t |
|----|--------|
| Use opaque metadata for your ids | Put API keys in the browser |
| Idempotent webhook handlers | Trust `?payment=success` alone |
| Use HTTPS public URLs in staging/prod | Expect Commerce subscription events for M2M checkouts |
| Keep domain rules in your app | Reimplement Billplz signature verify in your app |

## OpenAPI

Scalar developers page **Payments** product · TypeSpec `packages/api-spec/docs-payments.tsp` · `packages/api-spec/dist/payments/openapi.yaml`.

See [OpenAPI & Scalar](/reference/openapi).

## Related monorepo files

- `docs/payments-integration-quickstart.md` (engineer-oriented twin)  
- `script/second-app-proof.md` (curl harness)  
