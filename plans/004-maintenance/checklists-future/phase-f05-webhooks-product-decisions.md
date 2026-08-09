# F05 — LHDN webhooks product decisions (FW-2 gate)

**Goal:** Lock signing, payload, routing before any convergence code.  
**Depends on:** F00 webhooks track selected  
**Do not:** Implement dispatcher wiring in this phase

---

## F05.1 Inventory refresh

- [ ] Re-list LHDN outbound event types (`invoice.valid` / `invalid` / …)
- [ ] Re-list One platform event types and signing scheme
- [ ] Confirm freeze still in effect (`WebhookSenderService` fire-and-forget)

## F05.2 Product locks (write answers)

- [ ] **Signing:** keep body HMAC hex / move to One Standard Webhooks `t=,v1=` / dual-verify window ________
- [ ] **Payload:** keep LHDN envelope / wrap in One envelope / versioned ________
- [ ] **Routing:** all workspace endpoints / filtered by event type ________
- [ ] **Breaking change notice:** required? yes/no + channel ________

## F05.3 If product cannot share One signing

- [ ] Re-open `decisions.md` §00.2 formally
- [ ] Choose B only with ADR (second stack) — default remains **reject B**

## F05.4 Exit

- [ ] Written decision doc committed (e.g. `plans/004-maintenance/webhook-convergence-decisions.md`)
- [ ] F06 unblocked
