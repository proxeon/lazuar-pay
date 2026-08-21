# G21 — Webhook idempotency `(org_id, provider, event_id)`

**Track:** Rails · **Depends:** G18, D23  
**Analysis:** [06](../06-money-rails.md) §5.2 / §5.4  
**IDs:** NP-GW-006  
**Goal:** Retry no-ops. `NP-GW-006`. Journal once is F22.

---

## G21.1 Insert

- [x] After verify, insert D23 unique `(org_id, provider, event_id)`
- [x] Stripe `event_id` = `evt_…`. CHIP = `{kind}:{purchaseId}` — **never** invent a Guid
- [x] Conflict / unique violation → **200** no-op (`duplicate: true` is fine)
- [x] Second POST does **not** call fulfill again. F22 completes the journal side

## G21.2 Test two posts

- [x] Same body twice: first 200 (record ± fulfill), second **200** and still one row
- [x] Org A `event_id` does not collide with org B
- [x] Hermetic. No live PSP

## G21.3 Must not

- [x] Do not ACK 200 **before** the insert
- [x] Do not copy Hub outbox requeue / `HandleExistingLogAsync`
- [x] Tick `NP-GW-006` only on **this** host — Hub’s unique index does not count

## G21.4 Exit

- [x] `NP-GW-006` may move for the HTTP no-op; F22 still owns double-journal
- [x] Prefer G21 then F10 next (G18: no silent drop)
- [x] Unblocked for F10 / G25
