---
number: "334"
id: B10-X32
severity: P2
status: open
source: plans/009-bugs/10-tenancy-workers-contracts-tests.md
head: "297ba98"
---

# 334 — B10-X32 — Clock: invoice reminder UTC date vs `DueAt`

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/10-tenancy-workers-contracts-tests.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B10-X32 — P2 — Clock: invoice reminder UTC date vs `DueAt`

Quoted in §2.8. Offsets `[-3, 0, 3]` compare UTC calendar dates. A quote due “2026-08-20” stored as `2026-08-19T16:00:00Z` (00:00 MYT on the 20th) has `DueAt.Date == 2026-08-19` UTC. Day-0 mail goes out on the 19th UTC, i.e. the afternoon of the 19th in Malaysia — one local day early. The unique log then blocks a correct day-0 on the 20th.

No test uses a non-midnight `DueAt`. The three tests use in-process “today.”

---

## Evaluation (current tree, 2026-08-18)

### What the bug is
`InvoiceReminderJob` decides −3 / 0 / +3 by subtracting UTC calendar dates: `today = DateTime.UtcNow.Date` vs `session.DueAt.Value.Date`. A quote the merchant meant as “due 20 Aug MYT” stored as `2026-08-19T16:00:00Z` (00:00 MYT on the 20th) has `DueAt.Date == 2026-08-19`. Day-0 mail goes out on the 19th UTC — afternoon of the 19th in Malaysia, one local day early. The unique `(SessionId, DayOffset)` log then blocks a correct day-0 on the 20th. Offsets `[-3, 0, 3]` never fire on the merchant-local date.

### Still present?
**STILL BROKEN**

```64:87:apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/InvoiceReminderJob.cs
        var today = DateTime.UtcNow.Date;
        ...
            var dueDate = session.DueAt!.Value.Date;
            var dayOffset = (today - dueDate).Days;
            if (!Offsets.Contains(dayOffset))
            {
                continue;
            }
```

`DueAt` in the payload is also `yyyy-MM-DD` of that UTC date (115), so the email prints the early day.

Unique interlock is still UTC-offset based:

```267:272:apps/lazuar-api/Modules/Commerce/Infrastructure/CommerceDbContext.cs
        modelBuilder.Entity<InvoiceReminderDispatchLog>(builder =>
        {
            ...
            builder.HasIndex(x => new { x.SessionId, x.DayOffset }).IsUnique();
```

166 (`fix/166-reminder-expiry-claim`) added per-session `SaveChanges` + unique-violation swallow (134–143) so two replicas no longer roll back the whole batch. It did **not** change the clock. `GetRequiredService` for One/config (61–62) is 168’s sibling, not this.

Tests still use “now” as due:

```66:69:apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/Workers/InvoiceReminderJobTests.cs
    public async Task Day0Due_OpenCustom_SendsOnce()
    {
        var session = CustomOpen(_orgId, DateTime.UtcNow);
```

`CustomOpen` calls `SetDueAt(dueAt)` with that timestamp (143–152). There is still no test with `DueAt = 2026-08-19T16:00:00Z` vs a frozen clock. Four tests now (Day0, missing slug, completed, product session) — all in-process “today.”

197 (`fix/197-cycle-key-utc`) documented that **subscription** cycle keys / `NextBillingDate.Date` are UTC. That is the billing-engine clock, not this quote reminder. Related, not the same.

### Related files
- `apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/InvoiceReminderJob.cs` — UTC date math + offsets `[-3, 0, 3]`.
- `apps/lazuar-api/Modules/Commerce/Infrastructure/CommerceDbContext.cs` — unique `(SessionId, DayOffset)`.
- `apps/lazuar-api/Modules/Commerce/Domain/Entities/InvoiceReminderDispatchLog.cs` — log row.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/Workers/InvoiceReminderJobTests.cs` — midnight-shaped “today” only.
- Issue 197 (UTC cycle keys on subscriptions); 166 (claim/unique save); 168 (slug/config fail-open); 284 (reminder currency/template).

### Tests
- Existing tests that touch this path: `Day0Due_OpenCustom_SendsOnce`, `MissingWorkspaceSlug_DoesNotDispatchOrLog`, `Completed_IsSkipped`, `ProductSession_IsIgnored`.
- Whether any test would fail if the bug is still there: **no**. `DateTime.UtcNow` as DueAt is almost always “today UTC,” which is exactly the case that looks correct.
- First regression test: freeze clock at `2026-08-19T10:00:00Z`; set `DueAt = 2026-08-19T16:00:00Z` (00:00 Asia/Kuala_Lumpur on the 20th). Assert day-0 does **not** send on the 19th UTC if product wants MYT dates — **or**, if product doubles down on UTC (197’s stance), assert it **does** send and the email `due_at` is labeled UTC. Then advance to `2026-08-20T00:30:00Z` and assert the unique log blocks a second day-0. Pick one SSoT and lock it.

### Reproduction today
Arrange: OPEN custom checkout, `DueAt = 2026-08-19T16:00:00Z`, workspace slug present, job clock 2026-08-19 10:00 UTC (18:00 MYT). Act: `RunOnceAsync`. Assert: `InvoiceReminderDispatchLog` DayOffset 0 is inserted and `invoice.reminder` publishes — one local day early. Act again on 2026-08-20 00:30 UTC: `already` is true (89–93), no second mail.

### Blast radius
Quote / proforma buyers in MYT (and any TZ east of UTC whose due instant falls on the previous UTC date). They get “due today” on the afternoon before, then silence on the real due date. Unique log makes it unrecoverable without SQL. Frequency: every custom quote whose `DueAt` is not midnight UTC. Money: no double-charge; missed or early AR mail only. 197 already told merchants cycle keys are UTC — this job never documented that for quotes.

### Suggested fix
Pick a SSoT and do not invent a second calendar:

- **UTC (align with 197):** keep `UtcNow.Date` vs `DueAt.Date`, but stamp the email `due_at` with an explicit `Z` / “UTC” and ops copy that quote reminders are UTC dates. Add the non-midnight test so the early-MYT case is **known**.
- **Merchant local (MYT for Hub):** compute `dayOffset` in `Asia/Kuala_Lumpur` (same as B2C consolidation). Store DueAt as a date or as midnight MYT. The unique key stays `(SessionId, DayOffset)` but DayOffset is local.

Smallest change if product wants “don’t surprise MY merchants”: convert both sides with `TimeZoneInfo.FindSystemTimeZoneById("Asia/Kuala_Lumpur")` before `.Date`. Do not TypeSpec-regen. Do not emit `subscription.updated`. Do not reopen 166’s claim work.

### Evaluation notes
Still P2. **Not** 197 (cycle keys / period-end on subscriptions). 166 closed the no-claim / whole-batch rollback; clock remains. 284 is currency/template on the same job. Severity stays P2 unless Hub’s first tenants are all MYT quotes due at local midnight — then it fires every time. Do not mark resolved without a non-midnight `DueAt` test.


