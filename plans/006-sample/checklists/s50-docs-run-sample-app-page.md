# S50 — Docs: Run sample app page

**Track:** Runbook · **Analysis:** `../06` §6, `../08`  
**Depends on:** S31–S45 minimum (sample runnable); S46 preferred  
**Goal:** Cold reader can run sample from VitePress alone.

---

## S50.1 Create page

- [ ] `apps/lazuar-docs/docs/integrations/run-sample-app.md`
- [ ] H1: `Run sample app`
- [ ] Status: draft until e2e evidence once

## S50.2 Content sections

- [ ] What it proves (multi-app cashier; no Aura)
- [ ] Prerequisites: Hub, INTEGRATOR_PROVISION_SECRET, BYOK, tunnel notes
- [ ] Start Hub (task/docker) — API **8080**
- [ ] Get secrets: provision curl with `external_product` ≠ aura + webhook_url exact path
- [ ] Configure BYOK (human Ops) — PAYMENTS_NOT_CONFIGURED callout
- [ ] Install/run sample (`pnpm --filter …`, port **3020**)
- [ ] Create checkout / pay sandbox
- [ ] Verify webhook unlock + replay idempotency
- [ ] Troubleshooting table (signature, hops, scopes, BYOK)
- [ ] Test vs live keys policy (default sk_test_)
- [ ] Related: payment-flow, architecture, second-app-checklist, cashier

## S50.3 Nav

- [ ] Sidebar entry live (S10)
- [ ] Homepage Start here + optional hero alt CTA
- [ ] payments-cashier + second-app-checklist point here

## S50.4 Exit

- [ ] Docs build green
- [ ] No dead script/second-app-proof as primary harness
