# S12 — Hub vs DIY gateways (why Hub)

**Track:** Docs IA · **Analysis:** `../09-hub-vs-diy-docs.md`  
**Depends on:** S00; prefer after S11  
**Goal:** Optional but high-value comparison without teaching insecure DIY.

---

## S12.1 Placement

- [ ] Create `apps/lazuar-docs/docs/guide/hub-vs-diy.md` **or** `integrations/hub-vs-diy.md` (prefer **integrations** after cashier per analysis)
- [ ] Sidebar: **after** Payments cashier (not first in Integrations)
- [ ] H1 clear: Hub vs DIY gateways / Why not embed Billplz or Stripe

## S12.2 Required editorial rules

- [ ] Banner: DIY column is **contrast only**, not supported primary path
- [ ] Primary CTA links to Payments cashier / provision — not Billplz docs as how-to
- [ ] **No** production-ready Billplz `x_signature` field-order recipes
- [ ] **No** full Stripe `ConstructEvent` tutorial as Path B
- [ ] No vendor dunking (“Billplz is broken”) — factual quirks only

## S12.3 Content blocks

- [ ] Dual flow ASCII/Mermaid: DIY app↔gateway vs App↔Hub↔gateway
- [ ] Condensed responsibility matrix (App DIY Billplz / DIY Stripe / App via Hub / Hub)
- [ ] One paragraph “why Hub” (multi-gateway, one signature, BYOK vault)
- [ ] Anti-pattern callouts (redirect unlock, dual webhooks double-credit, secrets in SPA)
- [ ] Trust hierarchy: Hub signed event > GET checkout > success_url never sole
- [ ] Migration dual-run: time-boxed insurance only (Aura) — not greenfield

## S12.4 Deep links (not re-host)

- [ ] ADR 009 Billplz metadata (engineering)
- [ ] payments-integration-quickstart (engineer twin)
- [ ] architecture-who-does-what, webhooks, second-app-checklist

## S12.5 Concepts teaser

- [ ] Optional 5–10 line “Why Hub” H2 in `guide/concepts.md` linking to this page

## S12.6 Exit

- [ ] Docs build green
- [ ] Review: zero runnable DIY gateway verify snippets
