# S21 — Canonical payment-flow page

**Track:** Docs diagrams · **Analysis:** `../01` SEQ-E2E-CASHIER, `../08`  
**Depends on:** S00, S20  
**Goal:** Single SSoT end-to-end diagram + step map.

---

## S21.1 Create page

- [ ] Create `apps/lazuar-docs/docs/integrations/payment-flow.md`
- [ ] H1: `Payment flow`
- [ ] Product label: **Payments (M2M)** — not Commerce / LHDN / Paddle

## S21.2 Diagram content

- [ ] E2E sequence: provision → BYOK human → create checkout → guest pay → inbound → outbound → unlock
- [ ] Actors: Your app, Hub, Ops, Guest, Gateway
- [ ] Paths exact:
  - [ ] `POST /api/v1/one/integrations/workspaces/provision`
  - [ ] `POST /api/v1/integrations/payments/checkouts`
  - [ ] Outbound headers `X-Lazuar-Signature`, `X-Lazuar-Event`
- [ ] Note: success_url is UX only
- [ ] Prose summary 2–4 sentences under diagram (a11y)
- [ ] Optional: failed-path or idempotent replay mini-diagram

## S21.3 Step table → deep links

- [ ] Steps link to provision, create-checkout, webhooks, environments, architecture-who-does-what

## S21.4 Non-goals section

- [ ] Explicitly exclude Commerce `subscription.*` and LHDN `invoice.*` from this page

## S21.5 SSoT discipline

- [ ] `integrations/index.md` teases or links here (no second conflicting full E2E long-term)
- [ ] Sidebar entry (S10)

## S21.6 Exit

- [ ] Docs build green
- [ ] Paths match runtime modules
