# R43 — Retire LHDN fire-and-forget sender notes

**Date:** 2026-08-09  
**Track:** Webhooks  
**Checklist:** `checklists/r43-webhooks-retire-fire-and-forget.md`  
**Depends on:** R42 (enqueue path already One-only for customer delivery)  
**Scope this pass:** Delete dead fire-and-forget code, metrics call-site cleanup, docs honesty, mark FW-2 done.

---

## Summary

| Concern | State |
|---------|--------|
| Customer delivery path | One only (`OutboundWebhookRequestedIntegrationEvent` → outbox → dispatcher) |
| Trigger | Unchanged: `LhdnStatusPollingJob` → `DispatchExternalWebhookCommand` on VALID/INVALID |
| `DispatchExternalWebhookCommand` | **Kept** as pure publish path (no rename) |
| `WebhookSenderService` / `IWebhookSenderService` | **Deleted** |
| DI | `AddScoped<IWebhookSenderService, WebhookSenderService>()` removed |
| Metrics | No remaining `RecordWebhookFailed("lhdn")` call sites; `RecordWebhookFailed` still used for `"outbound"` / `"payment"` |
| `lhdn.WebhookSubscriptions` | **Kept** (R41 migrator source; optional later drop — not this pass) |
| Lhdn register/list/delete webhook API | Untouched (optional façade later) |
| FW-2 | **Done** in `plans/004-maintenance/FUTURE-WORK.md` |

---

## Code

| Piece | Change |
|-------|--------|
| `Modules/Lhdn/Infrastructure/Services/WebhookSenderService.cs` | Deleted |
| `Modules/Lhdn/Application/Services/IWebhookSenderService.cs` | Deleted |
| `Modules/Lhdn/Infrastructure/DependencyInjection.cs` | Removed sender registration |
| `Modules/Lhdn/Application/Commands/DispatchExternalWebhookCommand.cs` | Xmldoc only (still pure publish) |
| Lhdn / One `README.md` | Freeze language removed; invoice events listed on One |
| `FUTURE-WORK.md` FW-2 | Marked done |

### Grep (code) after R43

No remaining C# references to `IWebhookSenderService` or `WebhookSenderService` outside tests comments / plan docs.

`RecordWebhookFailed("lhdn")` — **zero** call sites.

---

## Why keep `DispatchExternalWebhookCommand`

R42 already made the handler a pure publish of `OutboundWebhookRequestedIntegrationEvent`. Renaming would churn poller + tests without delivery benefit. Command name remains historical “dispatch external webhook” but implementation is enqueue-only.

---

## Explicit non-goals

- Drop `lhdn.WebhookSubscriptions` table / EF mapping
- Dual-write or façade of `/lhdn/webhooks` over One
- Expand event catalog beyond `invoice.valid` / `invoice.invalid`
- Dual-sign headers
- Staging/prod live verification (ops / R42.4)

---

## Exit

- [x] Fire-and-forget sender unreachable (deleted)
- [x] Only One durable path for LHDN customer webhooks
- [x] Docs + FW-2 updated
- [ ] Staging: LHDN VALID/INVALID → `one.WebhookDeliveryOutboxes` (ops)
