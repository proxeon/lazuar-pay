# R40 — LHDN webhooks product lock

**Track:** Webhooks · **Analysis:** `../02-lhdn-webhooks-one-dispatcher.md`  
**Goal:** Written decisions before any enqueue code  
**No dispatcher implementation in this phase**  
**Artifact:** [`../webhook-convergence-decisions.md`](../webhook-convergence-decisions.md) · **Status:** complete  
**Wave seed:** `../wave-decisions.md` (R40 seed defaults) · **00.2:** `../../004-maintenance/decisions.md`

---

## R40.1 Inventory refresh

- [x] LHDN events: `invoice.valid`, `invoice.invalid`; others (submitted/cancelled): **out of MVP**
- [x] One signing: Standard Webhooks `t=,v1=` (live `OutboundWebhookSignature`)
- [x] LHDN signing: body-only HMAC hex (live `WebhookSenderService`)
- [x] Prod/staging Lhdn webhook subscription row counts: **pending ops** (blocked like keys R04 — do not invent)

## R40.2 Locks (write answers)

- [x] Signing end-state: **One `t=,v1=` only**; dual-verify window **if prod LHDN subs exist** (hard cut if prod count 0)
- [x] Payload: **platform envelope wrapping LHDN `data`** (P-B); stable `data.*` field names
- [x] Routing: **migrate to `TenantWebhookEndpoints`**; migrated LHDN URLs get `EnabledEvents = [invoice.valid, invoice.invalid]`; **empty = all** (unchanged `AcceptsEvent`)
- [x] Breaking notice required? **Yes** (signing and/or top-level payload shape)

## R40.3 Design choice

- [x] Confirm **A1** (Lhdn publishes `OutboundWebhookRequestedIntegrationEvent`) — **chosen**; A2/A3 not chosen
- [x] Explicitly reject B (second stack)

## R40.4 Artifact

- [x] Written `plans/005-remaining/webhook-convergence-decisions.md` with answers  
  _(commit when wave commits; this phase is docs-only, no app code)_

## R40.5 Exit

- [x] R41 unblocked
