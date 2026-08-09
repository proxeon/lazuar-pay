# R41 — Registry backfill Lhdn → One webhook endpoints

**Track:** Webhooks · **Analysis:** `../02-lhdn-webhooks-one-dispatcher.md`  
**Depends on:** R40  
**Goal:** One `TenantWebhookEndpoint` rows cover LHDN customer URLs

---

## R41.1 Migrator

- [ ] Map `lhdn.WebhookSubscription` (or current table) → One endpoints
- [ ] Set `EnabledEvents` to `invoice.valid` / `invoice.invalid` (per R40)
- [ ] Idempotent; preserve secrets/signing material per R40 dual-verify design
- [ ] Staging then prod runbook

## R41.2 Validation

- [ ] Row counts match expectations
- [ ] No silent zero endpoints for orgs that had Lhdn subs

## R41.3 Exit

- [ ] Backfill complete; fire-and-forget still may run until R42/R43 cutover plan says stop
