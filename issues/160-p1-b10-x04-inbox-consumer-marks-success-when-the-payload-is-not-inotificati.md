---
number: "160"
id: B10-X04
severity: P1
status: resolved
resolved_branch: fix/160-inbox-non-notification
source: plans/009-bugs/10-tenancy-workers-contracts-tests.md
head: "297ba98"
---

# 160 — B10-X04 — Inbox consumer marks success when the payload is not `INotification`

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/10-tenancy-workers-contracts-tests.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/160-inbox-non-notification`

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B10-X04 — P1 — Inbox consumer marks success when the payload is not `INotification`

```70:76:apps/lazuar-api/BuildingBlocks/Infrastructure/InboxConsumerJob.cs
                            var inboxEvent = JsonSerializer.Deserialize(message.Data, eventType);
                            if (inboxEvent is INotification notification)
                            {
                                await mediator.Publish(notification, stoppingToken);
                            }

                            MessageProcessingResultApplier.ApplySuccess(message, DateTime.UtcNow);
```

If `TypeResolver` returns a type that deserializes but is not `INotification`, the row is processed with no handler and no error. Contrast outbox: non-`IIntegrationEvent` **throws** and goes through `ApplyFailure`.

Today the only writers serialize integration events (which are `INotification`). The branch is still a landmine for the next inbox writer who serializes a command DTO.

No test covers this branch. `MessageProcessingResultApplierTests` only test the applier, not the job loop.

