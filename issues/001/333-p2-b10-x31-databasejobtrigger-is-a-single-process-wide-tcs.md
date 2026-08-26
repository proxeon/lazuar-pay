---
number: "333"
id: B10-X31
severity: P2
status: open
source: plans/009-bugs/10-tenancy-workers-contracts-tests.md
head: "297ba98"
---

# 333 — B10-X31 — `DatabaseJobTrigger` is a single process-wide TCS

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/10-tenancy-workers-contracts-tests.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B10-X31 — P2 — `DatabaseJobTrigger` is a single process-wide TCS

Any module’s `SaveChanges` wakes **all** outbox/inbox jobs. Harmless extra polls. Does not cross replicas (those rely on 5s). Tests construct it; none prove multi-waiter correctness (the swap is racy-looking but `Interlocked.Exchange` + `TrySetResult` is the usual pattern).

## Evaluation (current tree, 2026-08-18)

### What the bug is
`DatabaseJobTrigger` is one process-wide `TaskCompletionSource`. Every module DbContext’s successful `SaveChanges` calls `JobTrigger.Trigger()`, which completes that TCS and swaps a new one. Every outbox and inbox job waits on the **same** instance (`WaitAsync` + 5s cancel). One One-schema write therefore wakes Commerce, Billing, Lhdn, … pollers. Extra polls are cheap and fail-closed (SKIP LOCKED sees nothing). Multi-replica correctness is the 5s SQL poll, not this trigger. The Interlocked swap is the usual pattern; it is untested with two waiters.

### Still present?
**STILL BROKEN**

The type is still a single TCS:

```7:26:apps/lazuar-api/BuildingBlocks/Infrastructure/DatabaseJobTrigger.cs
public class DatabaseJobTrigger
{
    private volatile TaskCompletionSource _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Trigger()
    {
        var currentTcs = Interlocked.Exchange(ref _tcs, new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
        currentTcs.TrySetResult();
    }

    public async ValueTask WaitAsync(CancellationToken ct)
    {
        try
        {
            await _tcs.Task.WaitAsync(ct);
        }
        catch (OperationCanceledException)
        {
        }
    }
}
```

Every `PlatformDbContext.SaveChangesAsync` still pokes it:

```105:109:apps/lazuar-api/BuildingBlocks/Infrastructure/PlatformDbContext.cs
        // 4. Trigger background outbox/inbox workers instantly on success
        if (result > 0)
        {
            JobTrigger.Trigger();
        }
```

Jobs still wait on that singleton (`OutboxPublisherJob.cs` 109, `InboxConsumerJob.cs` 102). 180 made every module register both jobs via `AddModuleOutboxInbox`; it did **not** give each module its own trigger. Grep of tests: `new DatabaseJobTrigger()` is fixture wiring (Commerce/Billing/One/… DbContexts). There is no `DatabaseJobTriggerTests` and no two-waiter assertion.

### Related files
- `apps/lazuar-api/BuildingBlocks/Infrastructure/DatabaseJobTrigger.cs` — the TCS.
- `apps/lazuar-api/BuildingBlocks/Infrastructure/PlatformDbContext.cs` — global poke.
- `apps/lazuar-api/BuildingBlocks/Infrastructure/{OutboxPublisherJob,InboxConsumerJob}.cs` — waiters.
- `apps/lazuar-api/src/Lazuar.Api/Program.cs` (line 175: `AddSingleton<DatabaseJobTrigger>()`) — one instance for the process.
- `apps/lazuar-api/BuildingBlocks/Infrastructure/ModuleOutboxInboxServiceCollectionExtensions.cs` — still one trigger from that singleton.
- Issue 180 (idle inbox pollers + this global trigger — helper unified, trigger not split); 332 (lock time; this only wakes the loop).

### Tests
- Existing tests that touch this path: none that call `Trigger`/`WaitAsync` as the SUT. Fixtures construct it so `SaveChanges` does not NRE.
- Whether any test would fail if the bug is still there: **no**.
- First regression test: two `WaitAsync` callers, one `Trigger`, both complete; a waiter that already cancelled does not throw; a trigger with zero waiters is not lost (next `WaitAsync` must not hang forever — today’s swap means a trigger with no waiter is **lost**, which is why the 5s poll exists). Document that lost-wake is accepted.

### Reproduction today
Arrange: API process with all nine outbox+inbox jobs. Act: `POST` something that `SaveChanges` on One only (e.g. create invite). Assert (logs/metrics): every module’s publisher/consumer wakes, runs `SELECT … SKIP LOCKED`, finds nothing, sleeps. Kill the process and run two replicas: a write on replica A does **not** wake replica B; B drains within 5s. Unit: two tasks on `WaitAsync`, `Trigger()` once — empirically both usually complete; there is no test.

### Blast radius
Neon/Postgres chatter: 18 waiters × every successful SaveChanges, plus the 5s poll. Harmless at Hub scale; noisy if `Maximum Pool Size=50` and many modules. Does not lose events (SQL poll). Does not cross tenants. Money/PII: none. Frequency: every write.

### Suggested fix
Accept as-is and add the two-waiter test + a comment that wakes are process-local and extra polls are intentional. If chatter matters: keyed trigger per schema (`DatabaseJobTrigger` per module, poke only that module’s outbox/inbox). Do not try to signal other replicas (LISTEN/NOTIFY is out of wrap-rails). Do not TypeSpec-regen. Do not fold this into 332’s lock-across-handlers fix.

### Evaluation notes
Still P2. 180 is the sibling (“eight idle inbox pollers + one global trigger”); that issue unified DI and is resolved — this leftover is the single TCS. Not a duplicate of 332. Severity stays P2 (ops-only under scale). Closing as “documented, test added” is honest if product does not want per-module triggers.


