---
number: "235"
id: B05-L31
severity: P2
status: resolved
resolved_branch: fix/235-credit-hold-unique
source: plans/009-bugs/05-billing-ledger-refunds-disputes.md
head: "297ba98"
---

# 235 — B05-L31 — Credit hold: no unique correlation; `RELEASED` never written; exhaust stays `HELD`

- **Severity:** P2
- **Status:** resolved
- **Source:** `plans/009-bugs/05-billing-ledger-refunds-disputes.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/235-credit-hold-unique`

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B05-L31 — P2 — Credit hold: no unique correlation; `RELEASED` never written; exhaust stays `HELD`

See §7. Two reserves of the same broadcast deduct twice. Domain tests cover consume/release math, not the handler race.

---

## Evaluation (current tree, 2026-08-18)

### What the bug is
`ReserveCreditsCommandHandler` deducts the wallet and inserts a `CreditHold` without a unique `(OrganizationId, CorrelationId)` index. Two reserves with the same broadcast correlation create two rows and deduct twice. `CreditHold.Consume` reduces `RemainingAmount` but never changes `Status`; exhausting the hold (`Remaining = 0`) leaves `HELD`. `ReleaseRemaining` always writes `SETTLED` and never the documented `RELEASED` (XML on `Status` still lists `HELD, SETTLED, RELEASED`). If a caller consumes to zero and never calls release, the row stays `HELD` forever with remaining 0. Domain tests lock that exhaust-stays-`HELD` and release-is-`SETTLED` math. There is still no handler-level test for double reserve.

### Still present?
**STILL BROKEN**

Index is still non-unique (`BillingDbContext.cs:119` and migration `IX_CreditHolds_OrganizationId_CorrelationId` without `unique: true`):

```115:119:apps/lazuar-api/Modules/Billing/Infrastructure/BillingDbContext.cs
        modelBuilder.Entity<CreditHold>(builder =>
        {
            builder.ToTable("CreditHolds");
            builder.HasKey(x => x.Id);
            builder.HasIndex(x => new { x.OrganizationId, x.CorrelationId });
```

Reserve always inserts a new hold after deduct (`CreditHoldCommandHandlers.cs:38-41`). No lookup of an existing `HELD` row for the same correlation.

```19:20:apps/lazuar-api/Modules/Billing/Domain/Aggregates/CreditHold.cs
    /// <summary>HELD, SETTLED, RELEASED.</summary>
    public string Status { get; private set; } = "HELD";
```

```61:70:apps/lazuar-api/Modules/Billing/Domain/Aggregates/CreditHold.cs
    public int ReleaseRemaining()
    {
        if (Status != "HELD") throw new InvalidOperationException("Hold is no longer active.");
        var released = RemainingAmount;
        RemainingAmount = 0;
        Status = "SETTLED";
        ...
```

`Consume` does not settle at 0 (`CreditHold.cs:49-59`). `CreditHoldTests.Consume_ExactlyExhaustingHold_Succeeds` asserts `Status == "HELD"` at remaining 0 — the suite **locks** the exhaust residue.

**Caller note:** `ReserveCreditsCommand` / `ConsumeCreditHoldCommand` / `ReleaseCreditHoldCommand` have **no production `Send`** in this tree. Broadcast fan-out passes `CreditHoldId: broadcast.Id` (`BroadcastFanoutJob.cs:186`) without creating a `CreditHold`. The race is latent until something actually uses the hold commands (or a future WhatsApp/broadcast meter).

### Related files
- [`apps/lazuar-api/Modules/Billing/Domain/Aggregates/CreditHold.cs`](apps/lazuar-api/Modules/Billing/Domain/Aggregates/CreditHold.cs) — status machine; `RELEASED` is comment-only.
- [`apps/lazuar-api/Modules/Billing/Infrastructure/Commands/CreditHoldCommandHandlers.cs`](apps/lazuar-api/Modules/Billing/Infrastructure/Commands/CreditHoldCommandHandlers.cs) — deduct-then-insert; consume does not settle; release → `SETTLED`.
- [`apps/lazuar-api/Modules/Billing/Infrastructure/BillingDbContext.cs`](apps/lazuar-api/Modules/Billing/Infrastructure/BillingDbContext.cs) — non-unique correlation index.
- [`apps/lazuar-api/Modules/Billing/Contracts/Commands/CreditHoldCommands.cs`](apps/lazuar-api/Modules/Billing/Contracts/Commands/CreditHoldCommands.cs) — command shapes.
- [`apps/lazuar-api/tests/Modules.Billing.Tests/CreditHoldTests.cs`](apps/lazuar-api/tests/Modules.Billing.Tests/CreditHoldTests.cs) — domain only.
- [`apps/lazuar-api/Modules/Communications/Infrastructure/Workers/BroadcastFanoutJob.cs`](apps/lazuar-api/Modules/Communications/Infrastructure/Workers/BroadcastFanoutJob.cs) — does **not** call reserve (Wave 5 / WhatsApp out of this fix).

### Tests
- Existing: `Modules.Billing.Tests.CreditHoldTests` (`Constructor_SetsTotalAndRemaining`, `Consume_DecreasesRemaining`, `Consume_ExactlyExhaustingHold_Succeeds`, `ReleaseRemaining_ReturnsRemainderAndSettles`, `Consume_AfterRelease_Throws`, `ReleaseRemaining_Twice_Throws`). No `ReserveCreditsCommandHandler` tests. No unique-correlation test.
- Domain tests **pass while the bug is present** (`Consume_ExactlyExhaustingHold_Succeeds` requires `HELD` at 0). Nothing would fail on a double reserve.
- First regression: two concurrent `ReserveCreditsCommand` with the same `(org, correlation)` must yield one hold and one wallet deduct (unique violation treated as “return existing HELD id”). A second test: consume to 0 then release → one status (`SETTLED` or a real `RELEASED`), remaining 0, no second wallet move.

### Reproduction today
The HTTP surface does not expose reserve. Arrange a test host, wallet with 100 credits, send `ReserveCreditsCommand(org, 40, "broadcast-1", "broadcast")` twice. Assert: `AvailableCredits` is 20 (not 60) and two `CreditHolds` rows exist. Then `Consume` 40 on the first hold: status still `HELD`, remaining 0. Never call release: row stays `HELD`. Call `ReleaseRemaining` on a partial hold: status is `SETTLED`, never `RELEASED`.

### Blast radius
Latent. No production caller of `ReserveCreditsCommand` today. When broadcast/WhatsApp metering is wired, a retry or double-click reserve double-charges the wallet (money/credits). Exhaust-stays-`HELD` is an ops/reporting lie, not a second deduct. Do not “fix” by inventing a WhatsApp meter in this issue.

### Suggested fix
Make `IX_CreditHolds_OrganizationId_CorrelationId` unique for active holds (filtered unique on `Status = 'HELD'` is enough). `ReserveCreditsCommandHandler`: if a `HELD` row exists for the correlation, return its id (do not deduct again). Decide the status enum: either write `RELEASED` when `ReleaseRemaining` returns > 0 and `SETTLED` when remainder was 0, or delete `RELEASED` from the comment. Optionally auto-`SETTLED` when consume hits 0. Do not implement Wave 5 / WhatsApp dispatch here. No TypeSpec.

### Evaluation notes
009 §7 is still accurate. `CreditHoldTests` still “lie” in the 009 sense (domain only). Still P2 because unused. Blocked on a real caller, not on another issue. 161–200 fail-closed did not touch holds. Pair comment cleanup with **243**-style honesty if you drop `RELEASED`.

