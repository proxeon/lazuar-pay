# Hosted Commerce checkout (sell a Hub product link)

This is the **merchant CaaS path**: create a workspace, paste BYOK + Resend, create a product, share a public pay link, fulfill on signed Commerce webhooks.

This is **not** the Payments M2M cashier. Cashier apps that charge ad-hoc amounts should follow [Payments cashier](/integrations/payments-cashier) instead.

## 1. Create a workspace

Sign up in Ops (`lazuar-ops`). First login creates a workspace (name + public slug). The slug is the first segment of every buyer URL:

```text
/{workspace_slug}/checkout/{product_slug}
```

## 2. Paste BYOK + Resend

Hard gates before a **paid** checkout can start:

| Gate | Where | Why |
|------|--------|-----|
| Payment gateway | Workspace → Payment Gateways | Billplz / Stripe / CHIP / Razorpay **your** keys (BYOK). Hub does not acquire. |
| Email (Resend) | Workspace → Email Provider | Receipts, dunning, magic links. Initiate checkout stays gated without it. |

You can draft a product while Resend DNS is pending. Buyers cannot pay until email is active.

Set the gateway environment explicitly (sandbox vs live). A Hub hostname does **not** pick Billplz www vs sandbox — see [Environments](/integrations/environments).

## 3. Create a product and copy the link

Commerce → Products → create. Copy:

```text
{PORTAL_ORIGIN}/{workspace_slug}/checkout/{product_slug}
```

Local portal is typically `http://localhost:3004`. Production portal host is your Hub client URL (`App:ClientUrl`).

## 4. Buyer pays

Hop 1 is **your** hosted form (name, email, amount). Hop 2 is the processor page (Billplz / Stripe / CHIP). Do not restyle hop 2.

The success URL is only a **poller**. It is not proof of payment.

## 5. Fulfill on webhooks

Register an endpoint in Ops → Developer → Webhooks (or provision). Unlock on:

| Product type | Event |
|--------------|--------|
| One-time | `order.completed` |
| Recurring | `subscription.activated` (and `subscription.resumed` after recovery) |

Revoke on `subscription.canceled` / `subscription.suspended`. Catalog: [Event catalog (v1)](/reference/events). Verify: [Webhooks](/integrations/webhooks).

## 6. Do not trust the success URL

The buyer landing on `/checkout/{product}/success` is not fulfillment (LP-024). Only `COMPLETED` session status or the signed webhook is money truth.

## Related

- [Integrations overview](/integrations/)
- [API keys & scopes](/integrations/api-keys)
- [Environments & public URLs](/integrations/environments)
- [Payments cashier](/integrations/payments-cashier) (different product)
