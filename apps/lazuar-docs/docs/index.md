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
      text: Who does what
      link: /guide/architecture-who-does-what
    - theme: alt
      text: Run sample
      link: /integrations/run-sample-app
    - theme: alt
      text: Product lines
      link: /guide/product-lines
features:
  - title: BYOK cashier
    details: Your merchant accounts (Billplz, Stripe, CHIP, Razorpay). Hub vaults keys, creates hosted checkouts, and normalizes webhooks.
  - title: Server-to-server
    details: Scoped API keys (sk_test_ / sk_live_), M2M checkout create/get, signed outbound payment events.
  - title: Domain stays in your app
    details: Bookings, orders, invoices remain yours. Hub only moves money rails and notifies when payment is real.
  - title: Who does what
    details: Clear ownership matrices (app vs Hub vs gateway) for checkout, webhooks, secrets, multi-tenant BYOK, and anti-patterns.
---

## Start here

| If you want to… | Read |
|-----------------|------|
| Charge a variable amount from any backend | [Payments cashier](/integrations/payments-cashier) |
| See who owns each step | [Architecture: who does what](/guide/architecture-who-does-what) |
| Compare Hub vs embedding Billplz/Stripe | [Hub vs DIY](/integrations/hub-vs-diy) |
| Understand Payments vs Commerce vs LHDN | [Product lines](/guide/product-lines) |
| Wire webhooks safely | [Webhooks](/integrations/webhooks) |
| Prove multi-app independence | [Second-app checklist](/integrations/second-app-checklist) |
| Run the Next.js cashier sample | [Run sample app](/integrations/run-sample-app) |
| See how Aura does it | [Aura as a reference client](/integrations/aura-reference) |

## Status

These guides are **drafts for refinement**. Runtime APIs live in the monorepo; Scalar OpenAPI is under **lazuar-developers** (`/payments`). Orientation pages (architecture, cashier, checklists) live here; machine-readable contracts stay in developers/OpenAPI. Update guides as contracts change. Includes a runnable sample under monorepo `examples/hub-cashier-next` (port **3020**).

## Local preview

```bash
cd apps/lazuar-docs
pnpm dev
# → http://localhost:5180
```
