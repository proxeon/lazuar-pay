---
number: "332"
id: B10-X30
severity: P2
status: open
source: plans/009-bugs/10-tenancy-workers-contracts-tests.md
head: "297ba98"
---

# 332 — B10-X30 — Outbox publisher holds SKIP LOCKED rows while running all in-process handlers

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/10-tenancy-workers-contracts-tests.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B10-X30 — P2 — Outbox publisher holds SKIP LOCKED rows while running all in-process handlers

`OutboxPublisherJob` begins a transaction, locks ≤20 rows, then `await eventBus.PublishAsync` which runs every handler (Billing + Commerce + Communications on `GatewayPaymentCompleted`, etc.) **before** commit. Long MyInvois-adjacent or HTTP-inside-handler work (there should be none; outbound HTTP is the dispatcher) extends lock time. A throwing handler mid-list fails the **event**; already-run handlers in that same `PublishAsync` have already committed their own DbContexts. Retry re-runs everyone. Idempotency is per-handler (`HasEntryBeenProcessedAsync`, unique disputes). Untested as a composition.

`InboxConsumerJob` same lock-across-mediatR shape.

## Evaluation (current tree, 2026-08-18)

### What the bug is
`OutboxPublisherJob` opens a transaction, `SELECT … FOR UPDATE SKIP LOCKED` up to 20 rows, then for each row `await eventBus.PublishAsync` — which runs every in-process `IIntegrationEventHandler` (Billing + Commerce + Communications on `GatewayPaymentCompleted`, etc.) — **before** `SaveChanges` + `Commit`. Handler work holds the row locks. A throw mid-handler list fails that message; handlers that already committed their own DbContexts stay committed. Retry re-runs everyone. Idempotency is per-handler (`HasEntryBeenProcessedAsync`, unique disputes), not a composition guarantee. `InboxConsumerJob` is the same lock-across-`IMediator.Publish` shape.

### Still present?
**STILL BROKEN**

Publisher loop is still lock → publish → apply → (after the foreach) save/commit:

```44:91:apps/lazuar-api/BuildingBlocks/Infrastructure/OutboxPublisherJob.cs
                await using var transaction = await db.Database.BeginTransactionAsync(stoppingToken);
                // SELECT ... LIMIT 20 FOR UPDATE SKIP LOCKED
                ...
                            if (integrationEvent is IIntegrationEvent @event)
                            {
                                await eventBus.PublishAsync(@event);
                            }
                ...
                    await db.SaveChangesAsync(stoppingToken);
                    await transaction.CommitAsync(stoppingToken);
```

Inbox is the same (`InboxConsumerJob.cs` 44–84) with `await mediator.Publish(notification, stoppingToken)` inside the locked foreach (72).

What 160–162/180 **did** change (do not re-fix):

- No handlers: `InMemoryEventBus.PublishAsync` now **throws** (`InMemoryEventBus.cs` 32–36) instead of returning success. Outbox then `ApplyFailure`. Test: `InMemoryEventBusTests.Publish_With_No_Handlers_Throws`.
- Non-`INotification` inbox payload: `InboxNotificationRequirement.Require` throws (`InboxConsumerJob.cs` 71, 109–119). Test: `InboxNotificationRequirementTests.Non_Notification_Throws`.
- `TypeResolver` no longer caches null (`TypeResolver.cs` 30–35; `TypeResolverTests.Failed_Resolve_Is_Not_Cached`).
- DI: every module uses `AddModuleOutboxInbox` (180). That does not shorten the lock.

A mid-list handler throw still fails the **event** after earlier handlers in that `PublishAsync` have committed their scopes (`InMemoryEventBus.cs` 38–47: new scope, sequential `HandleAsync`). Retry re-enters the same list. Untested as a composition.

### Related files
- `apps/lazuar-api/BuildingBlocks/Infrastructure/OutboxPublisherJob.cs` — lock across `PublishAsync`.
- `apps/lazuar-api/BuildingBlocks/Infrastructure/InboxConsumerJob.cs` — lock across MediatR.
- `apps/lazuar-api/BuildingBlocks/Infrastructure/InMemoryEventBus.cs` — sequential in-process fan-out in a new scope.
- `apps/lazuar-api/BuildingBlocks/Infrastructure/MessageProcessingResultApplier.cs` — retry/Dead after 5 (tested).
- Handler examples that run under the lock: Commerce/Billing/Communications `GatewayPaymentCompleted` handlers (HTTP must stay on the dispatcher, not here).
- `apps/lazuar-api/tests/Lazuar.ModuleTests/BuildingBlocks/{InMemoryEventBusTests,InboxNotificationRequirementTests,TypeResolverTests,MessageProcessingResultApplierTests}.cs` — unit locks, not the job.
- Issues 160, 161, 162, 174 (dead letters / no redrive), 180 (unified DI), 328 (no loop test).

### Tests
- Existing tests that touch this path: applier retry/Dead; EventBus no-handlers throw; inbox non-notification throw; TypeResolver null not cached; CRM/Lhdn registration tests. **No** test constructs `OutboxPublisherJob` + two handlers + a throw + a second `ExecuteAsync`.
- Whether any test would fail if the bug is still there: **no**.
- First regression test: Testcontainers, one outbox row, handler A commits a side table then handler B throws; after `ApplyFailure` the row is unprocessed; second drain: A is idempotent (`HasEntryBeenProcessedAsync` or unique key), B runs once. Assert the `FOR UPDATE` is not held across a fake 2s handler (lock timeout / second worker can claim other rows). Same shape for inbox.

### Reproduction today
Arrange: two handlers on one event; A writes a ledger/dispute row and `SaveChanges`; B throws. Act: enqueue one outbox row, run the publisher once. Assert: A’s row is committed; outbox `AttemptCount` incremented, `ProcessedAt` still null. Run again: A’s handler runs again (must no-op); B throws again until Dead (5). On two API replicas, replica 2 cannot claim those 20 rows until replica 1 finishes the whole handler list + commit.

### Blast radius
Ops under load and any event with more than one handler (payment completed is the hot path). Long MyInvois/HTTP inside a handler (there should be none) extends lock time and starves the other replica’s drain. Double-side-effects if a handler is not idempotent. Frequency: every outbox batch. Money: at-least-once journals/emails if idempotency is missed. PII: same for CRM/comms handlers.

### Suggested fix
Smallest correct change: apply success/failure and **commit the outbox transaction per message** (or copy payloads out, commit the claim/lease, then publish). Do not hold `SKIP LOCKED` across `PublishAsync`. Keep per-handler idempotency. Do not put outbound HTTP in these handlers (dispatcher already owns that). Do not TypeSpec-regen. Do not invent `subscription.updated`. 174’s redrive is a sibling if a mid-list throw Dead-letters the row.

### Evaluation notes
Still P2 (scale / composition), not the P1 “no handlers = success” (161, fixed) or “non-INotification = success” (160, fixed). 180 unified registration; it did not split the lock. 328’s “no job loop test” is the test half of this issue. Do not mark resolved because EventBus now throws.


