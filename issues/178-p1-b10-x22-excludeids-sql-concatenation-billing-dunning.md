---
number: "178"
id: B10-X22
severity: P1
status: open
source: plans/009-bugs/10-tenancy-workers-contracts-tests.md
head: "297ba98"
---

# 178 — B10-X22 — `excludeIds` SQL concatenation (billing + dunning)

- **Severity:** P1
- **Status:** open
- **Source:** `plans/009-bugs/10-tenancy-workers-contracts-tests.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B10-X22 — P1 — `excludeIds` SQL concatenation (billing + dunning)

```129:131:apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/BillingEngineJob.cs
        var excludeClause = excludeIds.Count == 0
            ? ""
            : $""" AND "Id" NOT IN ({string.Join(",", excludeIds.Select(id => $"'{id}'"))})""";
```

Same in `DunningEngineJob.Claim.cs` 99–101. Values are `Guid`s from our process, not user input. Injection risk is low. Residual:

- Not parameterized; plan cache churn every distinct set.
- Default Guid format in SQL is culture-sensitive in theory (`Guid.ToString()` is `D` format, invariant). Fine in practice.
- The hunt named this specifically. It is the only user-facing-adjacent dynamic SQL in the claim path. `FromSqlRaw` with interpolated GUIDs is the same family as BillingEngineJob `excludeIds` in the 009 brief.

Schema/table interpolation in outbox/inbox jobs comes from EF model metadata, not request data. Same pattern, trusted source.

