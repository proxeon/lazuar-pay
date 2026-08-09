# R41 — Registry backfill Lhdn → One webhook endpoints

**Track:** Webhooks · **Analysis:** `../02-lhdn-webhooks-one-dispatcher.md` · **Product lock:** `../webhook-convergence-decisions.md`  
**Depends on:** R40  
**Runbook:** `../r41-webhooks-registry-backfill-runbook.md`  
**Goal:** One `TenantWebhookEndpoint` rows cover LHDN customer URLs

---

## R41.1 Migrator

- [x] Map `lhdn.WebhookSubscriptions` (active) → `one.TenantWebhookEndpoints`
- [x] Set `EnabledEvents` to `invoice.valid` / `invoice.invalid` (per R40)
- [x] Idempotent on Org + Url; preserve secrets (`Secret` → `SecretKey`, no remint)
- [x] Quarantine invalid URLs / empty secrets / orphan orgs
- [x] Domain ctor with preserved secret (not `CreateWebhookEndpointCommand`)
- [x] Hosted one-shot: `WebhookSubscriptionMigration` options `Enabled=false`, `DryRun=true` default
- [x] Env: `WEBHOOK_SUBSCRIPTION_MIGRATION_ENABLED`, `WEBHOOK_SUBSCRIPTION_MIGRATION_DRY_RUN`
- [x] Staging then prod runbook → `../r41-webhooks-registry-backfill-runbook.md`
- [x] Dual-write of `/lhdn/webhooks` register API: **skipped** (optional; R43 façade can follow)

## R41.2 Validation

- [ ] Row counts match expectations (run SQL in runbook on staging/prod)
- [ ] No silent zero endpoints for orgs that had active Lhdn subs (residual query)
- [x] Unit tests: empty / copy / idempotent / invalid URL / empty secret / orphan org / dry-run / secret preserve

## R41.3 Exit

- [x] Migrator implemented; fire-and-forget still runs until R42/R43 cutover plan says stop
- [ ] Staging dry-run + live executed (ops)
- [ ] Prod dry-run + live executed when counts warrant (ops)
