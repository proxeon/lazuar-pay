# S13 — Homepage + cross-links

**Track:** Docs IA · **Analysis:** `../08-docs-information-architecture.md`  
**Depends on:** S11 at least; S12/S21 if those pages exist  
**Goal:** Discoverability without CTA clutter or dead links.

---

## S13.1 Homepage `docs/index.md`

- [ ] Expand **Start here** table with:
  - [ ] Architecture: who does what
  - [ ] Payment flow (if S21 exists)
  - [ ] Run sample app (only if S50 exists; else Second-app checklist)
  - [ ] Keep: cashier, product lines, webhooks, Aura
- [ ] Hero actions: brand = Payments cashier; alt = Payment flow and/or Architecture (no dead Sample)
- [ ] Optional feature card: “Who does what” or “Multi-app + sample path”
- [ ] Status blurb: orientation pages vs OpenAPI in developers app

## S13.2 Integrations overview `integrations/index.md`

- [ ] Guide map rows for Architecture, Payment flow, Run sample (as available)
- [ ] Point E2E diagram to **payment-flow** as SSoT (avoid two diverging ASCII copies long-term)
- [ ] Keep short teaser diagram or link only

## S13.3 Bidirectional links

- [ ] `payments-cashier.md` — See also: architecture, payment-flow, second-app, run-sample (when ready)
- [ ] `second-app-checklist.md` — Related + harness pointer (not deleted script path)
- [ ] `webhooks.md` — link M2 ownership page for hop responsibility
- [ ] `api-keys.md` — link secrets matrix section
- [ ] `create-checkout.md` — link architecture create-payment matrix
- [ ] `concepts.md` / `product-lines.md` — footer related links
- [ ] `aura-reference.md` — payment-flow + sample when ready

## S13.4 Exit

- [ ] No internal 404s among new links
- [ ] Docs build green
