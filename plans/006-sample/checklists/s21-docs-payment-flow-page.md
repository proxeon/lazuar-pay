# S21 — Canonical payment-flow page

**Track:** Docs diagrams · **Analysis:** `../01` SEQ-E2E-CASHIER, `../08`  
**Depends on:** S00, S20  
**Goal:** Single SSoT end-to-end diagram + step map.

---

## S21.1 Create page

- [x] Create `apps/lazuar-docs/docs/integrations/payment-flow.md`
- [x] H1: `Payment flow`
- [x] Product label: **Payments (M2M)** — not Commerce / LHDN / Paddle

## S21.2 Diagram content

- [x] E2E sequence: provision → BYOK human → create checkout → guest pay → inbound → outbound → unlock
- [x] Actors: Your app, Hub, Ops, Guest, Gateway
- [x] Paths exact:
  - [x] `POST /api/v1/one/integrations/workspaces/provision`
  - [x] `POST /api/v1/integrations/payments/checkouts`
  - [x] Outbound headers `X-Lazuar-Signature`, `X-Lazuar-Event`
- [x] Note: success_url is UX only
- [x] Prose summary 2–4 sentences under diagram (a11y)
- [x] Optional: failed-path or idempotent replay mini-diagram

## S21.3 Step table → deep links

- [x] Steps link to provision, create-checkout, webhooks, environments, architecture-who-does-what (architecture page not yet; product-lines / cashier cover)

## S21.4 Non-goals section

- [x] Explicitly exclude Commerce `subscription.*` and LHDN `invoice.*` from this page

## S21.5 SSoT discipline

- [x] `integrations/index.md` teases or links here (no second conflicting full E2E long-term)
- [x] Sidebar entry (S10)

## S21.6 Exit

- [x] Docs build green
- [x] Paths match runtime modules
