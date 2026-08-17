---
number: "331"
id: B10-X29
severity: P2
status: open
source: plans/009-bugs/10-tenancy-workers-contracts-tests.md
head: "297ba98"
---

# 331 — B10-X29 — Pre-dunning SQL excludes `TRIALING` (comms hole, not a 02 claim bug)

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/10-tenancy-workers-contracts-tests.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B10-X29 — P2 — Pre-dunning SQL excludes `TRIALING` (comms hole, not a 02 claim bug)

```107:108:apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/DunningEngineJob.Claim.cs
                WHERE s."Status" = 'ACTIVE'
                  AND s."CancelAtPeriodEnd" IS NOT TRUE
```

A trial that ends in 14 days gets **no** pre-dunning “your trial ends” step from this engine. Billing will convert on the due tick (02’s product). This slice only notes the isolation of campaign matching: campaigns load with `IgnoreQueryFilters` and no org predicate in the load (all tenants’ campaigns in one list), then matchers re-scope. That load is intentional for a platform job.

