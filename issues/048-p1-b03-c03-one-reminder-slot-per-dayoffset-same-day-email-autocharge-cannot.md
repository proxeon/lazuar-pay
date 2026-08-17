---
number: "048"
id: B03-C03
severity: P1
status: open
source: plans/009-bugs/03-commerce-dunning-arrears-portal.md
head: "297ba98"
---

# 048 — B03-C03 — One reminder slot per DayOffset; same-day EMAIL + AUTO_CHARGE cannot both run

- **Severity:** P1
- **Status:** open
- **Source:** `plans/009-bugs/03-commerce-dunning-arrears-portal.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B03-C03 — P1 — One reminder slot per DayOffset; same-day EMAIL + AUTO_CHARGE cannot both run

**Evidence.** Unique index:

```307:312:apps/lazuar-api/Modules/Commerce/Infrastructure/CommerceDbContext.cs
        modelBuilder.Entity<ReminderDispatchLog>(builder =>
        {
            builder.ToTable("ReminderDispatchLogs");
            builder.HasKey(x => x.Id);
            // Idempotency by DayOffset so campaign step ID regeneration does not re-fire or orphan logs.
            builder.HasIndex(x => new { x.SubscriptionId, x.TargetBillingDate, x.DayOffset }).IsUnique();
```

Processor filter (`PastDueDunningProcessor.cs` 93–97) is the same triple, not `step.Id`. Default seed separates offsets. Ops UI does not.

**Repro.** Campaign: day 0 EMAIL, day 0 AUTO_CHARGE. PAST_DUE day 0. Only the first in `OrderBy(DayOffset)` (stable by insert) runs. The other never appears in logs as a distinct step.

**Blast.** Merchants who design “email and retry the card today” get email-only or charge-only. Recovery rate drops; they think AUTO_CHARGE is broken.

**Tests.** Default-seed tests pass because offsets differ. Add a test that two steps at offset 0 both take effect **or** that create/update campaign rejects duplicate offsets.

**Fix direction.** Unique key `(Sub, Date, StepId)` **or** forbid duplicate offsets at campaign save **or** treat AUTO_CHARGE as not consuming the comms slot (separate attempt log is already the charge receipt).

---

