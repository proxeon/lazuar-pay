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

## Evaluation (current tree, 2026-08-18)

### What the bug is
`DunningEngineJob` pre-dunning claim SQL (and the InMemory twin) only selects `Status = 'ACTIVE'` with `NextBillingDate` in the next 14 days. A `TRIALING` subscription whose trial ends in three days never matches, so campaign steps with `DayOffset < 0` (“your trial ends”) never dispatch. Billing still converts the trial on the due tick (02 / BillingEngineJob). This is a comms hole, not a claim-logic / starve / double-charge bug. Campaigns are loaded unfiltered by design for a platform job; matchers re-scope by org.

### Still present?
**STILL BROKEN**

Relational claim:

```104:118:apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/DunningEngineJob.Claim.cs
            ClaimMode.PreDunning => $"""
                SELECT s.* FROM commerce."Subscriptions" s
                WHERE s."Status" = 'ACTIVE'
                  AND s."CancelAtPeriodEnd" IS NOT TRUE
                  AND (s."CollectionPausedUntil" IS NULL OR s."CollectionPausedUntil" <= NOW())
                  AND (s."DunningPausedUntil" IS NULL OR s."DunningPausedUntil" <= NOW())
                  AND s."NextBillingDate" IS NOT NULL
                  AND s."NextBillingDate" > NOW()
                  AND s."NextBillingDate" <= NOW() + INTERVAL '14 days'
```

In-memory claim is the same predicate (`s.Status == "ACTIVE"`, 161–168). Past-due claim is `PAST_DUE` only (120–129) — correct; trials are not past-due.

Campaign load is still all-tenants + `IgnoreQueryFilters` (`DunningEngineJob.cs` 66–73). Intentional. Not this bug.

Billing still owns conversion. `BillingEngineJob.cs` 329–340 comments that TRIALING never enters dunning and, after attempt 1 with no webhook, marks PAST_DUE. A trial that is simply “14 days left, no charge yet” gets no email from this engine.

`excludeIds` is now parameterized (`<> ALL({0})`, 100–102) — 178 closed the concat hole; it did not add TRIALING.

### Related files
- `apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/DunningEngineJob.Claim.cs` — the ACTIVE-only WHERE.
- `apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/DunningEngineJob.cs` — campaign load + `RunOnceAsync`.
- `apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/DunningEngineJob.PreDunning.cs` — step matching once a row is claimed.
- `apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/BillingEngineJob.cs` — trial convert / PAST_DUE fallback (02).
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/Workers/DunningEngineJobTests.cs` — pre-dunning tests use `Activate(...)`, never `ActivateTrial`.
- Issues 052 (dunning pause vs pre-dunning), 047 (trial convert stall), 002 (claim starve) — different bugs.

### Tests
- Existing tests that touch this path: `PreDunning_Minus3Email_DoesNotFireTenDaysOut_FiresAtThreeDays` (240–273), `PreDunning_PausedUntilFuture_NotClaimed`, `PreDunning_FlaggedActiveDueInThreeDays_DoesNotDispatchEmail`, `PreDunning_DoesNotAutoCharge`, `Snapshot_E9_PreDunning_LiveAddOfNegativeOffsetAlreadyInWindow_StillFires`. All seed `Subscription.Activate(...)` → `ACTIVE`.
- Whether any test would fail if the bug is still there: **no**. Adding a TRIALING fixture would be a new test; current ones stay green.
- First regression test: `ActivateTrial` with `NextBillingDate`/trial end in 3 days, campaign step `DayOffset = -3`, `RunOnceAsync`, assert a `reminder.dunning` fulfillment (or a dedicated `trial.ending` type if product wants a different template) and a reminder log. Assert a TRIALING row 10 days out still does not fire. Do not assert a charge.

### Reproduction today
Arrange: InMemory (or a tenant) with a TRIALING sub, trial/next billing in 3 days, active campaign with EMAIL step −3, CRM email present, WhatsApp off. Act: `DunningEngineJob.RunOnceAsync`. Assert: `ReminderLogs` empty; no `FulfillmentRequestedIntegrationEvent`. Repeat with an ACTIVE sub on the same dates: −3 mail fires (existing test). On the due tick, BillingEngine converts or PAST_DUE’s the trial without this engine having warned.

### Blast radius
Trial buyers and merchants who configured “remind 3 days before.” They get silence, then a convert or a PAST_DUE. Not a double-charge. Frequency: every TRIALING sub in the 14-day window. Money: none from this skip; churn/surprise convert is the cost. PII: none.

### Suggested fix
Widen the pre-dunning WHERE (SQL + InMemory) to `Status IN ('ACTIVE','TRIALING')` and keep `CancelAtPeriodEnd` / pause / 14-day window. Reuse existing EMAIL steps; do not invent WhatsApp. Do not emit `subscription.updated`. Do not change BillingEngine claim logic (02). Product copy can say “trial ends” in the template body; the engine only needs to claim the row. Tests as above.

### Evaluation notes
Still P2. Explicitly **not** a 02 claim bug and not 002’s starve. 052 (`DunningPausedUntil` vs pre-dunning) is adjacent — pause already excluded here. 197/334 are UTC date clocks, not this status filter. Do not close by “documenting that trials have no pre-dunning” unless product signs that; the campaign UI will still offer negative offsets.


