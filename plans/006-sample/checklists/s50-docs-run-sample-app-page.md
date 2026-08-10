# S50 — Docs: Run sample app page

**Track:** Runbook · **Analysis:** `../06` §6, `../08`  
**Depends on:** S31–S45 minimum (sample runnable); S46 preferred  
**Goal:** Cold reader can run sample from VitePress alone.

---

## S50.1 Create page

- [x] `apps/lazuar-docs/docs/integrations/run-sample-app.md`
- [x] H1: `Run sample app`
- [x] Status: draft until e2e evidence once

## S50.2 Content sections

- [x] What it proves (multi-app cashier; no Aura)
- [x] Prerequisites: Hub, INTEGRATOR_PROVISION_SECRET, BYOK, tunnel notes
- [x] Start Hub (task/docker) — API **8080**
- [x] Get secrets: provision curl with `external_product` ≠ aura + webhook_url exact path
- [x] Configure BYOK (human Ops) — PAYMENTS_NOT_CONFIGURED callout
- [x] Install/run sample (`pnpm --filter …`, port **3020**)
- [x] Create checkout / pay sandbox
- [x] Verify webhook unlock + replay idempotency
- [x] Troubleshooting table (signature, hops, scopes, BYOK)
- [x] Test vs live keys policy (default sk_test_)
- [x] Related: payment-flow, architecture, second-app-checklist, cashier

## S50.3 Nav

- [x] Sidebar entry live (S10)
- [x] Homepage Start here + optional hero alt CTA
- [x] payments-cashier + second-app-checklist point here

## S50.4 Exit

- [x] Docs build green
- [x] No dead script/second-app-proof as primary harness
