---
layout: home
title: Lazuar Hub Docs
hero:
  name: Lazuar Hub
  text: Integrator guides
  tagline: Connect your app to payments, webhooks, and workspaces — without embedding Billplz or Stripe yourself.
  actions:
    - theme: brand
      text: Payments cashier
      link: /integrations/payments-cashier
    - theme: alt
      text: Product lines
      link: /guide/product-lines
    - theme: alt
      text: Concepts
      link: /guide/concepts
features:
  - title: BYOK cashier
    details: Your merchant accounts (Billplz, Stripe, CHIP, Razorpay). Hub vaults keys, creates hosted checkouts, and normalizes webhooks.
  - title: Server-to-server
    details: Scoped API keys (sk_test_ / sk_live_), M2M checkout create/get, signed outbound payment events.
  - title: Domain stays in your app
    details: Bookings, orders, invoices remain yours. Hub only moves money rails and notifies when payment is real.
  - title: Multi-app ready path
    details: Provision with external_product + external_org_id. Aura is the first client — not the only shape.
---

## Start here

| If you want to… | Read |
|-----------------|------|
| Charge a variable amount from any backend | [Payments cashier](/integrations/payments-cashier) |
| Understand Payments vs Commerce vs LHDN | [Product lines](/guide/product-lines) |
| Wire webhooks safely | [Webhooks](/integrations/webhooks) |
| See how Aura does it | [Aura as a reference client](/integrations/aura-reference) |

## Status

These guides are **drafts for refinement**. Runtime APIs live in the monorepo; Scalar OpenAPI is under **lazuar-developers** (`/payments`). Update guides as contracts change.

## Local preview

```bash
cd apps/lazuar-docs
pnpm dev
# → http://localhost:5180
```
