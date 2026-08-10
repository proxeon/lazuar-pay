# S99 — Definition of done (006 program)

**Track:** Program · **Analysis:** `../10`, `../README.md`  
**Depends on:** selected tracks complete  
**Goal:** Close 006 honestly; residuals are normal tickets.

---

## S99.1 Docs track

- [x] Architecture-who-does-what (matrices) published
- [x] Payment-flow SSoT page published
- [x] Critical page diagrams (provision, checkout, webhooks, env/product/second-app) present
- [x] Hub-vs-diy present **or** explicitly deferred with reason — present at `/integrations/hub-vs-diy`
- [x] Run-sample-app page present and linked
- [x] Dead `script/second-app-proof` actionable refs gone
- [x] Diagram maintenance notes in how-to-maintain
- [x] `pnpm --filter lazuar-docs build` green

## S99.2 Sample track

- [x] `examples/hub-cashier-next` runs on 3020
- [x] Creates Hub integration checkout (server-only sk_)
- [x] Verifies Hub signature on raw body (envelope + data)
- [x] Unlocks local order only on payment.completed
- [x] Replay idempotent
- [x] No Billplz/Stripe SDK deps
- [x] Product turbo/CI not forced to build sample
- [x] README + .env.example complete

## S99.3 Proof

- [x] Harness restored (plans or scripts)
- [x] Second-app checklist points at sample
- [x] At least one e2e evidence path (fake webhook and/or sandbox)

## S99.4 Explicit non-claims

- [x] Not claiming production deploy of sample
- [x] Not claiming 005 keys/webhooks ops residual closed
- [x] Not claiming Commerce/LHDN covered by sample

## S99.5 Program docs

- [x] `plans/006-sample/README.md` status → **done** (or partial with residual list)
- [x] Checklist README phase statuses updated if tracked
- [x] Residual tickets listed (e.g. Mermaid plugin if ASCII-only; optional mprocs; TypeSpec envelope honesty)

## S99.6 Exit

- [x] Declare 006 closed for implementable scope
- [x] Stop inventing S-phases; follow-ups are normal issues
