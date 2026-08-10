# 006 — Sample app + docs diagrams program

**Status:** analysis complete 2026-08-10 · **execution checklists ready**  
**Scope:** Make Hub integrator story **demoable** without Aura: VitePress flow diagrams + responsibility matrices in `apps/lazuar-docs`, plus a standalone Next.js sample under `examples/` that provisions (or accepts keys), creates M2M checkouts, verifies signed webhooks, and fulfills a toy domain object.  
**Prerequisite:** `plans/005-remaining` wave closed (ops residual only). **No 005 residual blocks this program.**

## Detailed implementation checklists

→ **[`checklists/README.md`](./checklists/README.md)** — **S00–S99** granular phases (one phase ≈ one PR)

Tracks: Docs IA (S10–S14), Diagrams (S20–S25), Sample packaging (S30–S31), Sample app (S40–S46), Runbook (S50–S53), Polish (S60–S61). Prefer these over fat mega-todos.

---

## Program goals

| Goal | Outcome |
|------|---------|
| **G1 — Diagrams** | Every critical integrator path has a Mermaid (or intentional ASCII) diagram in `lazuar-docs` that matches runtime. |
| **G2 — Responsibility matrices** | Who owns what (app vs Hub vs gateway) is paste-ready on dedicated + existing pages so DIY mistakes are visible. |
| **G3 — Sample app** | A Next.js 16 App Router app under `examples/` proves second-app path: keys → checkout → webhook → unlock. |
| **G4 — Contracts fidelity** | Sample + docs use snake_case JSON, real scopes, real signature algorithm (`OutboundWebhookSignature`). |
| **G5 — Monorepo hygiene** | Sample is in workspace for convenience but **excluded** from default turbo CI product builds; plain `fetch`, not `@repo/api-types-ts`. |

### Non-goals

- Not a second product surface (no Commerce catalog in sample).
- Not embedding Billplz/Stripe SDKs in the sample app.
- Not publishing public marketing site polish (docs remain draft-friendly).
- Not Dockerfile / prod deploy for the sample.
- Not dual-run Aura migration modes.

---

## Analysis index (full text — uncondensed)

| # | File | Focus |
|---|------|--------|
| 01 | [`01-docs-flow-diagrams.md`](./01-docs-flow-diagrams.md) | VitePress structure; which pages get diagrams; full Mermaid sources (E2E cashier, provision, checkout, webhooks, product lines, second-app, environments); a11y/maintenance |
| 02 | [`02-responsibility-matrices.md`](./02-responsibility-matrices.md) | Matrices M1–M7 paste-ready; page placement; sample cross-links |
| 03 | [`03-sample-app-architecture.md`](./03-sample-app-architecture.md) | Placement under `examples/`; Next 16 App Router; routes; env; in/out scope; raw-body risks |
| 04 | [`04-checkout-create-contract.md`](./04-checkout-create-contract.md) | Exact API request/response; scopes; idempotency; near-final Next route handler; errors map |
| 05 | [`05-webhook-verify-nextjs.md`](./05-webhook-verify-nextjs.md) | Headers; algorithm matching `OutboundWebhookSignature.cs`; raw body; full TS verify + route; envelope; curl/python tests |
| 06 | [`06-provision-and-env.md`](./06-provision-and-env.md) | `sk_` / `whsec_` acquisition; `.env.example`; ports 8080 vs 8090; BYOK; provision script; run-sample-app docs; test vs live |
| 07 | [`07-monorepo-packaging.md`](./07-monorepo-packaging.md) | pnpm `examples/*`; turbo filters; CI no-build sample; plain fetch; file tree; no Dockerfile |
| 08 | [`08-docs-information-architecture.md`](./08-docs-information-architecture.md) | Current + proposed VitePress sidebar/nav; new pages; homepage; phased docs PRs |
| 09 | [`09-hub-vs-diy-docs.md`](./09-hub-vs-diy-docs.md) | Pros/cons DIY comparison; hybrid placement; condensed tables only; sample reinforces no Billplz in app |
| 10 | [`10-program-sequencing.md`](./10-program-sequencing.md) | Goals/non-goals; D00–D06 PRs; dependencies; DoD; manual test plan; risks; README shape |

---

## Delivery phases (D00–D06)

| Phase | Name | Primary outputs | Depends on |
|-------|------|-----------------|------------|
| **D00** | Align & freeze | This folder + decision notes (envelope honesty, placement, ports) | — |
| **D01** | Docs IA + matrices | Sidebar/nav; M1–M7 pages; architecture-who-does-what; hub-vs-diy | D00 |
| **D02** | Flow diagrams | Mermaid on integrations + guide pages | D00 (parallel-ok with D01 after freeze) |
| **D03** | Sample scaffold | `examples/hub-cashier-next` package + env + turbo exclude | D00 |
| **D04** | Checkout + UI | Create checkout route, success/cancel pages, order toy model | D03, contract freeze in **04** |
| **D05** | Webhooks | Raw-body verify matching Hub; fulfill domain | D03, algorithm freeze in **05** |
| **D06** | Runbooks + second-app green | `run-sample-app` docs page; provision script outline; manual e2e checklist | D01–D05 |

Orchestration detail, DoD, and risks: **[`10-program-sequencing.md`](./10-program-sequencing.md)**.

---

## Runtime anchors (SSoT for analysis accuracy)

| Concern | Path |
|---------|------|
| M2M checkout endpoints | `apps/lazuar-api/Modules/Payments/Infrastructure/IntegrationEndpoints.cs` |
| Checkout validation / idempotency | `…/Payments/Application/Commands/CreateIntegrationCheckoutCommandHandler.cs` |
| Error codes | `…/Payments/Application/Exceptions/PaymentIntegrationException.cs` |
| TypeSpec payments | `packages/api-spec/modules/payments/models.tsp`, `routes.tsp` |
| TypeSpec provision | `packages/api-spec/modules/one/models/provision.tsp` |
| Outbound signing | `apps/lazuar-api/Modules/One/Infrastructure/Workers/OutboundWebhookSignature.cs` |
| Outbound headers + dispatch | `…/OutboundWebhookDispatcherJob.cs` |
| Envelope wrap | `…/EventHandlers/OutboundWebhookEventHandlers.cs` |
| Payment payload (data) | `…/Payments/…/IntegrationCheckoutGatewayEventsHandler.cs` |
| VitePress config | `apps/lazuar-docs/docs/.vitepress/config.ts` |
| Engineer quickstart twin | `docs/payments-integration-quickstart.md` |
| pnpm workspace | `pnpm-workspace.yaml` (`apps/*`, `packages/*` — **examples not yet**) |
| API local port | `8080` (`launchSettings.json`, compose, README) |
| Docs local port | `5180` (`lazuar-docs` package.json) |

### Contract honesty note (locked for sample)

Live outbound JSON is **envelope + data**, not the flat TypeSpec `PaymentWebhookPayloadDto` alone:

```json
{
  "id": "<delivery envelope id>",
  "event_type": "payment.completed",
  "created_at": "<utc>",
  "data": {
    "event_id": "…",
    "checkout_id": "…",
    "gateway": "…",
    "gateway_transaction_id": "…",
    "provider_session_id": "…",
    "amount": 25.0,
    "currency": "MYR",
    "status": "completed",
    "metadata": { },
    "description": "…",
    "customer_email": "…"
  }
}
```

Sample + webhook docs must document the **runtime envelope**. TypeSpec flat model remains a description of payment fields inside `data` (and a known honesty gap for Wave B / docs PR).

---

## Relationship to other plan folders

| Folder | Relationship |
|--------|----------------|
| `001-backend` | Established M2M + outbound webhooks solidification |
| `004-maintenance` / `005-remaining` | Webhook one-dispatcher, keys One-only; **closed for code** — ops residual does not block 006 |
| `002-change-name`, `003-dev-caddy` | Ports / monorepo rename context only |

---

## How to implement (process)

1. Read **checklists/README.md** for phase index and parallel rules.  
2. Read **10** for program risks/DoD narrative.  
3. Start **S00** freeze, then tracks in suggested order (docs ∥ packaging, then sample serial).  
4. For each S-phase: open matching checklist; use **01–09** analysis as how-to; land small PRs.  
5. Keep docs and sample in the same mental model: **domain in app, money rails in Hub**.

---

## Definition of done (program-level)

- [ ] VitePress builds with new diagrams and matrices (`pnpm --filter lazuar-docs build`).  
- [ ] Sample app runs locally with Hub `:8080`, creates checkout, verifies webhook, unlocks toy order.  
- [ ] No Billplz/Stripe SDK in sample dependencies.  
- [ ] Sample not required for monorepo product CI green.  
- [ ] Second-app checklist can point at sample as evidence path.  
- [ ] Guides mark envelope shape and raw-body requirement explicitly.
