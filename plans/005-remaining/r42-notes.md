# R42 — Enqueue LHDN lifecycle into One dispatcher (A1) notes

**Date:** 2026-08-09  
**Track:** Webhooks  
**Checklist:** `checklists/r42-webhooks-enqueue-path.md`  
**Depends on:** R40 product lock, R41 registry backfill (recommended before prod cutover)  
**Scope this pass:** **Code + unit tests**. Staging verification remains ops.

---

## Summary

| Concern | State |
|---------|--------|
| Design | **A1** — Lhdn publishes `OutboundWebhookRequestedIntegrationEvent` |
| Trigger | Unchanged: `LhdnStatusPollingJob` → `DispatchExternalWebhookCommand` on VALID/INVALID |
| Enqueue | `DispatchExternalWebhookCommandHandler` → `LhdnEventBus` outbox → One `OutboundWebhookEventHandlers` |
| EventType | `invoice.valid` / `invoice.invalid` |
| TargetUrl | `null` (fan-out) |
| Payload | Data-only snake_case: `internal_id`, `lhdn_uuid`, `status`, `qr_link`, `error_message` |
| Fire-and-forget | **Removed from this path** (no `GetActiveWebhooks` / `IWebhookSenderService`) |
| WebhookSenderService | **Kept** in DI/code for R43 retirement |
| Dual-sign | **Skipped** this pass |

---

## Code

| Piece | Change |
|-------|--------|
| `Modules/Lhdn/Application/Commands/DispatchExternalWebhookCommand.cs` | Replace sender loop with publish of `OutboundWebhookRequestedIntegrationEvent` |
| `Modules/Lhdn/Application/Modules.Lhdn.Application.csproj` | ProjectReference → `Modules.Commerce.Contracts` |
| `Modules/Lhdn/Infrastructure/Workers/LhdnStatusPollingJob.cs` | Unchanged (still sends command) |
| `IWebhookSenderService` / `WebhookSenderService` | Untouched (R43) |

### Publish shape

```csharp
new OutboundWebhookRequestedIntegrationEvent(
    OrganizationId: request.OrganizationId,
    TargetUrl: null,
    EventType: $"invoice.{status.ToLowerInvariant()}",
    Payload: dataElement) // SerializeToElement(snake_case data object)
```

Bus: keyed `"LhdnEventBus"` (`OutboxEventBus<LhdnDbContext>`). Poller `SaveChanges` flushes outbox; `LhdnOutboxPublisherJob` dispatches to One subscription.

### Payload vs legacy

| Legacy LHDN wire | R42 + One |
|------------------|-----------|
| Top-level `{ event, data }` | One envelope `{ id, event_type, created_at, data }` |
| `data.timestamp` | Prefer envelope `created_at` (not in data) |
| Direct HMAC to `lhdn.WebhookSubscriptions` | One HMAC / Standard Webhooks to `one.TenantWebhookEndpoints` |

---

## Tests

`apps/lazuar-api/tests/Lazuar.ModuleTests/Lhdn/DispatchExternalWebhookCommandTests.cs`

- VALID → `invoice.valid`, null TargetUrl, qr_link built, data fields snake_case
- INVALID → `invoice.invalid` + `error_message`
- Single publish side effect (no sender service dependency)

Existing One tests cover fan-out / `AcceptsEvent` for `invoice.valid`.

---

## Dual-sign (skipped)

R40 optional dual-verify window needs ops prod row counts + calendar end date. Not implemented here. Prefer hard cut after R41 backfill when staging verified.

---

## Staging exit (ops)

1. Ensure R41 migration ran (or endpoints already have `invoice.valid`/`invoice.invalid` in EnabledEvents).
2. Force a VALID/INVALID poll path (or inject command) for a test org.
3. Confirm `one.WebhookDeliveryOutboxes` row(s) for that org + event type.
4. Confirm dispatcher delivers and no fire-and-forget POST to legacy Lhdn URLs from this path.

---

## Explicit non-goals (this pass)

- Delete `WebhookSenderService` / Lhdn webhook register endpoints (R43)
- Dual-fire fire-and-forget + One
- Dual-sign headers
- Expand catalog beyond `invoice.valid` / `invoice.invalid`
