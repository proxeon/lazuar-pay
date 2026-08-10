# S99 — Definition of done (006 program)

**Track:** Program · **Analysis:** `../10`, `../README.md`  
**Depends on:** selected tracks complete  
**Goal:** Close 006 honestly; residuals are normal tickets.

---

## S99.1 Docs track

- [ ] Architecture-who-does-what (matrices) published
- [ ] Payment-flow SSoT page published
- [ ] Critical page diagrams (provision, checkout, webhooks, env/product/second-app) present
- [ ] Hub-vs-diy present **or** explicitly deferred with reason
- [ ] Run-sample-app page present and linked
- [ ] Dead `script/second-app-proof` actionable refs gone
- [ ] Diagram maintenance notes in how-to-maintain
- [ ] `pnpm --filter lazuar-docs build` green

## S99.2 Sample track

- [ ] `examples/hub-cashier-next` runs on 3020
- [ ] Creates Hub integration checkout (server-only sk_)
- [ ] Verifies Hub signature on raw body (envelope + data)
- [ ] Unlocks local order only on payment.completed
- [ ] Replay idempotent
- [ ] No Billplz/Stripe SDK deps
- [ ] Product turbo/CI not forced to build sample
- [ ] README + .env.example complete

## S99.3 Proof

- [ ] Harness restored (plans or scripts)
- [ ] Second-app checklist points at sample
- [ ] At least one e2e evidence path (fake webhook and/or sandbox)

## S99.4 Explicit non-claims

- [ ] Not claiming production deploy of sample
- [ ] Not claiming 005 keys/webhooks ops residual closed
- [ ] Not claiming Commerce/LHDN covered by sample

## S99.5 Program docs

- [ ] `plans/006-sample/README.md` status → **done** (or partial with residual list)
- [ ] Checklist README phase statuses updated if tracked
- [ ] Residual tickets listed (e.g. Mermaid plugin if ASCII-only; optional mprocs; TypeSpec envelope honesty)

## S99.6 Exit

- [ ] Declare 006 closed for implementable scope
- [ ] Stop inventing S-phases; follow-ups are normal issues
