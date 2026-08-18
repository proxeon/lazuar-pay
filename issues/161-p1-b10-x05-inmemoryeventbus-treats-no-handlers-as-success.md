---
number: "161"
id: B10-X05
severity: P1
status: resolved
resolved_branch: fix/161-eventbus-no-handlers
source: plans/009-bugs/10-tenancy-workers-contracts-tests.md
head: "297ba98"
---

# 161 — B10-X05 — `InMemoryEventBus` treats “no handlers” as success

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/10-tenancy-workers-contracts-tests.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/161-eventbus-no-handlers`

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B10-X05 — P1 — `InMemoryEventBus` treats “no handlers” as success

```32:36:apps/lazuar-api/BuildingBlocks/Infrastructure/InMemoryEventBus.cs
        if (!_handlers.TryGetValue(eventName, out var handlers))
        {
            _logger.LogInformation("Event {EventName} was published but has no registered handlers.", eventName);
            return;
        }
```

Outbox then `ApplySuccess`. A missing `Use*Subscriptions` call, a typo in `Subscribe<TEvent, THandler>`, or a handler registered against the compile-time interface name rather than the runtime type, **drops the event permanently** (one Information log). This is how a forgotten Lhdn subscription would have failed closed-looking (“outbox drained”) while Billing never saw `LhdnDocumentValidated`.

There is no architecture test that every `IIntegrationEvent` has at least one `Subscribe`.

