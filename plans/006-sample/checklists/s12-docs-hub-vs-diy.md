# S12 — Hub vs DIY gateways (why Hub)

**Track:** Docs IA · **Analysis:** `../09-hub-vs-diy-docs.md`  
**Depends on:** S00; prefer after S11  
**Goal:** Optional but high-value comparison without teaching insecure DIY.

---

## S12.1 Placement

- [x] Create `apps/lazuar-docs/docs/guide/hub-vs-diy.md` **or** `integrations/hub-vs-diy.md` (prefer **integrations** after cashier per analysis)
- [x] Sidebar: **after** Payments cashier (not first in Integrations)
- [x] H1 clear: Hub vs DIY gateways / Why not embed Billplz or Stripe

## S12.2 Required editorial rules

- [x] Banner: DIY column is **contrast only**, not supported primary path
- [x] Primary CTA links to Payments cashier / provision — not Billplz docs as how-to
- [x] **No** production-ready Billplz `x_signature` field-order recipes
- [x] **No** full Stripe `ConstructEvent` tutorial as Path B
- [x] No vendor dunking (“Billplz is broken”) — factual quirks only

## S12.3 Content blocks

- [x] Dual flow ASCII/Mermaid: DIY app↔gateway vs App↔Hub↔gateway
- [x] Condensed responsibility matrix (App DIY Billplz / DIY Stripe / App via Hub / Hub)
- [x] One paragraph “why Hub” (multi-gateway, one signature, BYOK vault)
- [x] Anti-pattern callouts (redirect unlock, dual webhooks double-credit, secrets in SPA)
- [x] Trust hierarchy: Hub signed event > GET checkout > success_url never sole
- [x] Migration dual-run: time-boxed insurance only (Aura) — not greenfield

## S12.4 Deep links (not re-host)

- [x] ADR 009 Billplz metadata (engineering)
- [x] payments-integration-quickstart (engineer twin)
- [x] architecture-who-does-what, webhooks, second-app-checklist

## S12.5 Concepts teaser

- [x] Optional 5–10 line “Why Hub” H2 in `guide/concepts.md` linking to this page

## S12.6 Exit

- [x] Docs build green
- [x] Review: zero runnable DIY gateway verify snippets
