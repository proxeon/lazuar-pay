# 014 — Evaluate new Lazuar Pay, then port Hub gateway adapters as HTTP judgment

**Date:** 24 August 2026  
**Branch:** `main`  
**HEAD at analysis start:** `ee2db8e5` — `feat(pay): Bar B receipts, webhook secret, merchant money UI`  
**Type:** Uncondensed evaluation. **Not** an implementation. **Not** a flip of [011/11](../011-new-lazuar-pay/11-checklist.md) cells. **Not** a project reference into `apps/lazuar-api`.

Parent judgment: [00-evaluation.md](./00-evaluation.md). Evidence: the ten uncondensed reports (~11,700 lines). **Do not treat this index as the analysis.** Read the file.

**Problem:** where the new stack (`lazuar-pay` + merchant Vite + checkout Vite) actually is after Bar B, and how to take Hub’s five PSP adapters as **HTTP judgment** into the new host — without cloning `Modules/Payments`, MediatR, outbox/inbox, or a factory of five on day one.

New stack (the thing under evaluation):

| Path | Role today (re-check in the reports; this row is a pointer) |
|------|--------------------------------------------------------------|
| `apps/lazuar-pay` | Focused C# host on **8081**. One façade, Postgres on 5435, Stripe hosted rail, PSP webhook, same-handler fulfillment. |
| `apps/lazuar-pay-merchant` | Vite **5178**. Staff shell. One OIDC. Not `lazuar-ops`. |
| `apps/lazuar-pay-checkout` | Vite **5179**. Hosted buyer page. Buyers have no One account. Not `lazuar-portal`. |

Old stack (steal HTTP extract; do not grow):

| Path | Role today |
|------|------------|
| `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/` | Five adapters + factory + DNS fallback + CHIP webhook registrar. |
| `apps/lazuar-api` | Modular monolith Hub API. IsolationTests ban a project reference. |
| `apps/lazuar-ops` / `lazuar-portal` | Old merchant / buyer UIs. Do not retarget at 8081. |

Binding from [011](../011-new-lazuar-pay/README.md), [012](../012-one-to-pay/README.md), [013](../013-prods/README.md):

1. Steal adapters as **HTTP judgment**. Do not copy the module, MediatR, `IEventBus`, outbox/inbox jobs, or `PaymentsDbContext` schema.
2. **One dogfood rail first** (Stripe is already on 8081). CHIP **or** Billplz is the Malaysian rail. Razorpay / Xendit stay later.
3. Wrap-rails honesty. BYOK. Same-handler fulfillment. Receipt ≠ tax invoice. Buyers are not Zitadel humans.
4. `008-evals` and `013-prods` papers are **historical**. Live files on this SHA are authority when they disagree.

| File | Subagent | Assigned slice |
|------|----------|----------------|
| [00-evaluation.md](./00-evaluation.md) | Parent (orchestrator) | Verdict, adapter answer, P0s, next ten |
| [01-new-pay-host.md](./01-new-pay-host.md) | New Pay host | `apps/lazuar-pay` current state vs 011/013 |
| [02-merchant-frontend.md](./02-merchant-frontend.md) | Merchant Vite | `:5178` OIDC, workspace, money UI |
| [03-checkout-frontend.md](./03-checkout-frontend.md) | Checkout Vite | `:5179` hosted pay; no Zitadel |
| [04-old-adapter-seam.md](./04-old-adapter-seam.md) | Hub Payments seam | Port, factory, capabilities, who calls adapters |
| [05-stripe-port.md](./05-stripe-port.md) | Stripe | Old `StripeGatewayAdapter` vs new `StripeHosted` |
| [06-malaysia-rails.md](./06-malaysia-rails.md) | CHIP + Billplz | First Malaysian rail; steal HTTP only |
| [07-sea-later-rails.md](./07-sea-later-rails.md) | Xendit + Razorpay | Later rails; why not day one |
| [08-webhooks-secrets-fulfillment.md](./08-webhooks-secrets-fulfillment.md) | Plane B + secrets | Verify, idempotency, SecretBox, same-handler |
| [09-porting-architecture.md](./09-porting-architecture.md) | New adapter seam | How to add rails on 8081 without the cathedral |
| [10-honesty-gaps-next.md](./10-honesty-gaps-next.md) | Honesty / next | Tracker vs live; ranked gaps; refuse list |

Write uncondensed. Do not summarize a report into a bullet list and delete the evidence.
