# Integrations overview

Integrate **your backend** with Lazuar Hub using HTTP + scoped keys. Hub does not embed your domain model.

## Flow (Payments cashier)

Compact overview. **Canonical E2E (SSoT):** [Payment flow](/integrations/payment-flow).

```text
Your app                  Lazuar Hub                 Gateway
   |                          |                         |
   |-- provision workspace -->|                         |
   |<-- sk_ + whsec_ ---------|                         |
   |                          |<-- human BYOK config ---|
   |-- create checkout ------>|                         |
   |<-- checkout_url ---------|-- create bill/session ->|
   |-- redirect guest --------|------------------------>|
   |                          |<-- provider webhook ----|
   |<-- payment.completed ----|                         |
   |-- unlock domain ---------|                         |
```

**Summary:** Provision once, configure BYOK, create checkout, redirect guest, fulfill only on signed `payment.completed` from Hub — never on browser success alone.

## Guide map

| Step | Guide |
|------|--------|
| 1. Choose product | [Product lines](/guide/product-lines) |
| Hosted product link | [Hosted Commerce checkout](/integrations/hosted-checkout) |
| 2. Full money path (cashier) | [Payment flow](/integrations/payment-flow) |
| 3. Provision | [Provision a workspace](/integrations/provision) |
| 4. Keys | [API keys & scopes](/integrations/api-keys) |
| 5. Checkout | [Create a checkout](/integrations/create-checkout) |
| 6. Webhooks | [Webhooks](/integrations/webhooks) |
| Events that exist | [Event catalog (v1)](/reference/events) |
| 7. Environments | [Environments & public URLs](/integrations/environments) |
| Full cashier | [Payments cashier](/integrations/payments-cashier) |
| Prove second app | [Second-app checklist](/integrations/second-app-checklist) |
| Run sample | [Run sample app](/integrations/run-sample-app) |

## Auth summary

| Actor | Auth |
|-------|------|
| Your server | `Authorization: Bearer sk_test_…` or `sk_live_…` |
| Workspace provision | `X-Lazuar-Provision-Key` or SUPER_ADMIN session |
| Human BYOK setup | Hub Ops cookie / JWT (org admin) |
| Guest browser | No Hub keys — only gateway hosted page |

## Status

**v1.** Runtime is production-capable for first-party apps (e.g. Aura) and hosted Commerce links. OpenAPI stays reference.
