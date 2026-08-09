# R40 — LHDN webhooks product lock

**Track:** Webhooks · **Analysis:** `../02-lhdn-webhooks-one-dispatcher.md`  
**Goal:** Written decisions before any enqueue code  
**No dispatcher implementation in this phase**

---

## R40.1 Inventory refresh

- [ ] LHDN events: `invoice.valid`, `invoice.invalid`, others: ________
- [ ] One signing: Standard Webhooks `t=,v1=`
- [ ] LHDN signing: body-only HMAC hex
- [ ] Prod/staging Lhdn webhook subscription row counts: ________

## R40.2 Locks (write answers)

- [ ] Signing end-state: One only / dual-verify window / keep LHDN header: ________
- [ ] Payload: platform envelope wrapping LHDN data / pure LHDN body: ________
- [ ] Routing: all workspace endpoints / filter by EnabledEvents: ________
- [ ] Breaking notice required? ________

## R40.3 Design choice

- [ ] Confirm **A1** (Lhdn publishes `OutboundWebhookRequestedIntegrationEvent`) vs A2/A3: ________
- [ ] Explicitly reject B (second stack)

## R40.4 Artifact

- [ ] Commit `plans/005-remaining/webhook-convergence-decisions.md` with answers

## R40.5 Exit

- [ ] R41 unblocked
