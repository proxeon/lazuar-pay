# Hub vs DIY gateways

**Why not embed Billplz or Stripe directly in your app?**

> **Contrast only.** The DIY column explains trade-offs. It is **not** a supported primary integration path in Hub docs. For greenfield apps, use the [Payments cashier](/integrations/payments-cashier). Official processor docs remain the place for merchant account setup and BYOK credentials configured **inside Hub Ops**.

## When to use Hub

Lazuar Hub’s **Payments cashier** is a BYOK multi-gateway façade: your server creates ad-hoc checkouts with a scoped `sk_`, guests pay on the **merchant’s** processor page, and your server receives **one** signed webhook shape (`payment.completed` / `payment.failed`) regardless of Billplz, Stripe, CHIP, or Razorpay. Hub is **not** Merchant of Record for guest GMV. Domain objects stay in your app.

**Hub wins when:** multi-gateway MY/SEA stack, multi-tenant SaaS, or you want domain-focused engineering instead of N adapter codebases.

**DIY may still win when:** you need extreme processor features Hub lacks, you will stay on a single gateway forever, or you have a regulatory need to avoid any intermediary API for payment create.

## Dual flow (conceptual)

```text
DIY
  Your app  ←→  Gateway (Billplz or Stripe or …)
  You verify each provider signature; you normalize events.

Hub cashier
  Your app  ←→  Lazuar Hub  ←→  Gateway
  You verify one Hub signature; Hub owns Hop 1 + adapters.
```

## Hub cashier vs DIY

| | Hub Payments cashier | DIY (Billplz/Stripe in your app) |
|--|----------------------|----------------------------------|
| **Integration surface** | One HTTP API + one signature scheme | One API + signature scheme **per** processor |
| **Hosted pay page** | Yes (via gateway) | Yes (you wire each) |
| **Settlement** | Merchant account (BYOK) | Merchant account |
| **MoR for guest GMV** | No | No (unless you use a MoR product) |
| **Credential storage** | Hub vault per workspace | Your vault / secrets manager |
| **Multi-gateway** | Adapters already in Hub | You write adapters |
| **Metadata quirks** | Hub session survives (e.g. Billplz strip) | You discover edge cases in prod |
| **Ops UI for keys** | Hub Ops | You build or use scripts |
| **Fulfillment signal** | Normalized `payment.*` webhooks | Provider-specific events |
| **Vendor lock-in** | Hub API (open HTTP) | Processor APIs (also lock-in) |
| **Moving parts in your app** | Keys + verify + domain unlock | Keys + N verifies + N clients + unlock |

## Security surface

| Concern | DIY | Hub |
|---------|-----|-----|
| Processor API secrets in app | Yes | No (BYOK in Hub) |
| Webhook secrets in app | Per processor | One `whsec_` (Hub) |
| Risk of trusting browser redirect | Same footgun | Same footgun — docs forbid |
| Signature bugs | N implementations | 1 implementation |
| Cross-tenant key misuse | Your bug classes | Workspace-bound `sk_` |

## Feature matrix (short)

| Capability | DIY Billplz | DIY Stripe | Hub |
|------------|-------------|------------|-----|
| Create payment | Billplz API | Stripe API | `POST …/integrations/payments/checkouts` |
| Verify | Billplz rules | Stripe rules | `X-Lazuar-Signature` |
| Multi-gateway | You | You | Built-in allow-list |
| Normalized paid event | You | You | `payment.completed` |

## Cost / ops (honest, non-pricing)

| Topic | Note |
|-------|------|
| Processor fees | Unchanged — merchant pays gateway fees either way |
| Hub product pricing | Separate from GMV; listed on the Hub host at `/pricing` (do not copy numbers here) |
| Eng time | DIY costs ongoing adapter + verify maintenance |
| Incident debug | DIY: provider logs only; Hub: Ops delivery logs + session |

## What Hub does **not** replace

- Your **domain** catalog, bookings, invoices, unlock rules  
- **Merchant of Record** SaaS seat billing (e.g. Paddle for platform fees)  
- **Commerce** product catalog and subscription lifecycle (different product line)

## Trust hierarchy

1. **Hub signed** `payment.completed` / `payment.failed` (after verify) — source of truth for unlock  
2. Optional **GET checkout** — UX “processing…”, not sole unlock  
3. **`success_url` redirect** — never sole paid signal  

## Anti-patterns

- Unlock domain on browser redirect alone  
- Dual webhooks (Hub **and** processor) without careful dedupe → double credit  
- Processor or Hub secrets in SPA / `NEXT_PUBLIC_*`  
- Re-implementing processor signature verify in your app **while** using Hub checkouts  

Full anti-pattern table: [Architecture: who does what — M7](/guide/architecture-who-does-what#m7--anti-patterns-do-not).

## Migration / dual-run

**Greenfield:** Hub-only.  
**Migration:** temporary dual-run may exist in first-party apps (e.g. Aura). Do not design new systems around dual-run.

## Next steps

| Step | Guide |
|------|--------|
| Start integrating | [Payments cashier](/integrations/payments-cashier) |
| Full ownership matrices | [Architecture: who does what](/guide/architecture-who-does-what) |
| Signature + events | [Webhooks](/integrations/webhooks) |
| Multi-app independence | [Second-app checklist](/integrations/second-app-checklist) |
| Engineer twin (repo) | `docs/payments-integration-quickstart.md` |
| Billplz metadata ADR | `docs/architecture-decision-log/009-stateless-webhook-metadata-transmission.md` |

Do **not** look here for production DIY Billplz `x_signature` field-order recipes or full Stripe `ConstructEvent` tutorials — use official provider docs if you configure BYOK keys in Ops.
