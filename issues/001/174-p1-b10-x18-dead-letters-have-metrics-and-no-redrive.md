---
number: "174"
id: B10-X18
severity: P1
status: resolved
resolved_branch: fix/174-dead-letter-redrive
source: plans/009-bugs/10-tenancy-workers-contracts-tests.md
head: "297ba98"
---

# 174 — B10-X18 — Dead letters have metrics and no redrive

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/10-tenancy-workers-contracts-tests.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/174-dead-letter-redrive`

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B10-X18 — P1 — Dead letters have metrics and no redrive

`MessageProcessingResultApplier.ApplyFailure` at max attempts sets `ProcessedAt` so the poll (`ProcessedAt IS NULL`) never sees the row again. `PlatformMetricsCollector` counts `Status = 'Dead'`. `/health/ready` **does not** fail on dead letters.

`Observability:OutboxLagReadyThreshold` is **null** in `appsettings.json`. `HealthReadiness` then skips the lag gate:

```39:46:apps/lazuar-api/BuildingBlocks/Infrastructure/Observability/HealthReadiness.cs
        if (options.OutboxLagReadyThreshold is not { } lagThreshold || lagThreshold <= TimeSpan.Zero)
        {
            return new Result(
                IsReady: true,
                Status: "ready",
                ...
```

A replica with a 3-day outbox backlog and a pile of Dead LHDN events is **ready**. Docker healthcheck is HTTP liveness. Fail-open for ops.

No admin API resets `ProcessedAt` / `Status`. Replay is raw SQL.

