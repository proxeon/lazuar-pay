# R41 — Registry backfill Lhdn → One notes

**Date:** 2026-08-09  
**Track:** Webhooks  
**Checklist:** `checklists/r41-webhooks-registry-backfill.md`  
**Runbook:** `r41-webhooks-registry-backfill-runbook.md`  
**Depends on:** R40 product lock (`webhook-convergence-decisions.md`)  
**Scope this pass:** **Code + unit tests + runbook**. Staging/prod execute remains ops.

---

## Summary

| Concern | State |
|---------|--------|
| Source | Active `lhdn.WebhookSubscriptions` only |
| Target | `one.TenantWebhookEndpoints` |
| Secret | **Preserved** Lhdn `Secret` → One `SecretKey` via domain ctor (no remint) |
| EnabledEvents | `["invoice.valid","invoice.invalid"]` (not empty) |
| Idempotency | Org + Url |
| Invalid URL | Quarantine via `WebhookUrlValidator` |
| Dual-write register API | **Skipped** (optional; not required for backfill) |
| Fire-and-forget | Unchanged (R42/R43) |

---

## Code

`apps/lazuar-api/src/Lazuar.Api/Jobs/WebhookSubscriptionMigration/`

| Piece | Role |
|-------|------|
| `WebhookSubscriptionMigrationOptions` | `Enabled=false`, `DryRun=true`, `BatchSize=500` |
| `LegacyWebhookSubscriptionMigrator` | Pure orchestration + domain insert mapping |
| `SqlWebhookSubscriptionMigrationStore` | Dapper/Npgsql |
| `LegacyWebhookSubscriptionMigrationHostedService` | Flag-gated one-shot |

Env: `WEBHOOK_SUBSCRIPTION_MIGRATION_ENABLED`, `WEBHOOK_SUBSCRIPTION_MIGRATION_DRY_RUN`.

Registration in `Program.cs` only when `Enabled=true`.

---

## Tests

`tests/Lazuar.ModuleTests/One/LegacyWebhookSubscriptionMigratorTests.cs` — fake store, 10 cases (empty, copy+secret, idempotent, existing, invalid URL, empty secret, orphan org, dry-run, trim URL, no remint).

---

## Ops remaining

- [ ] Staging row counts + dry-run + live  
- [ ] Prod row counts + dry-run + live when active Lhdn > 0  
- [ ] Residual Org+Url query should be 0 (or only quarantined residuals signed off)
