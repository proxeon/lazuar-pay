# S13 — Homepage + cross-links

**Track:** Docs IA · **Analysis:** `../08-docs-information-architecture.md`  
**Depends on:** S11 at least; S12/S21 if those pages exist  
**Goal:** Discoverability without CTA clutter or dead links.

---

## S13.1 Homepage `docs/index.md`

- [x] Expand **Start here** table with:
  - [x] Architecture: who does what
  - [ ] Payment flow (if S21 exists)
  - [x] Run sample app (only if S50 exists; else Second-app checklist)
  - [x] Keep: cashier, product lines, webhooks, Aura
- [x] Hero actions: brand = Payments cashier; alt = Payment flow and/or Architecture (no dead Sample)
- [x] Optional feature card: “Who does what” or “Multi-app + sample path”
- [x] Status blurb: orientation pages vs OpenAPI in developers app

## S13.2 Integrations overview `integrations/index.md`

- [x] Guide map rows for Architecture, Payment flow, Run sample (as available)
- [ ] Point E2E diagram to **payment-flow** as SSoT (avoid two diverging ASCII copies long-term) (deferred — S21)
- [x] Keep short teaser diagram or link only

## S13.3 Bidirectional links

- [x] `payments-cashier.md` — See also: architecture, payment-flow, second-app, run-sample (when ready)
- [x] `second-app-checklist.md` — Related + harness pointer (not deleted script path)
- [x] `webhooks.md` — link M2 ownership page for hop responsibility
- [x] `api-keys.md` — link secrets matrix section
- [x] `create-checkout.md` — link architecture create-payment matrix
- [x] `concepts.md` / `product-lines.md` — footer related links
- [x] `aura-reference.md` — payment-flow + sample when ready (linked architecture / hub-vs-diy / second-app; flow+sample deferred)

## S13.4 Exit

- [x] No internal 404s among new links
- [x] Docs build green
