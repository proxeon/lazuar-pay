---
number: "237"
id: B05-L33
severity: P2
status: resolved
resolved_branch: fix/237-skip-empty-zero-journal
source: plans/009-bugs/05-billing-ledger-refunds-disputes.md
head: "297ba98"
---

# 237 — B05-L33 — `$0`-priced `ProcessZeroAmount` writes a no-line journal

- **Severity:** P2
- **Status:** resolved
- **Source:** `plans/009-bugs/05-billing-ledger-refunds-disputes.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/237-skip-empty-zero-journal`

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B05-L33 — P2 — `$0`-priced `ProcessZeroAmount` writes a no-line journal

`OriginalAmount = 0` → skip `AddLine` → `ValidateBalanced` on empty → header with `ZERO_AMOUNT_CHECKOUT` and no lines. Harmless noise. No test in Billing.

---

## Evaluation (current tree, 2026-08-18)

### What the bug is
`ZeroAmountCheckoutHandler` only adds discount/revenue lines when `OriginalAmount > 0`. A true `$0` list price (catalog amount 0, no coupon, not a trial-with-list) publishes `OriginalAmount = 0` from `ProcessZeroAmountCheckoutCommandHandler` (`lineGross = unitAmount * qty`). Billing then constructs a `ZERO_AMOUNT_CHECKOUT` header, skips `AddLine`, `ValidateBalanced` succeeds on an empty line set (net base 0), marks consolidation not required, and saves. The row is balanced noise: it burns a unique `(org, ZERO_AMOUNT_CHECKOUT, sessionId)` key and shows up on the ledger with no amounts. **008** fixed the *unbalanced* trial case (list > 0, discount 0) by treating any positive original as 100% off. That does not cover original = 0. InitiateCheckout sends any `lineNet == 0` non-vaulting product through `ProcessZeroAmount`.

### Still present?
**STILL BROKEN**

```33:45:apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/ZeroAmountCheckoutHandler.cs
        if (@event.OriginalAmount > 0)
        {
            // Zero-amount checkout is always 100% off (coupon or trial). Use list as discount
            // so a trial that published DiscountAmount=0 still balances.
            var discount = @event.OriginalAmount;
            entry.AddLine(AccountTypes.ExpenseDiscount, discount, @event.Currency, discount, @event.Currency);
            entry.AddLine(AccountTypes.RevenueGross, -@event.OriginalAmount, @event.Currency, -@event.OriginalAmount, @event.Currency);
        }

        entry.ValidateBalanced();
        entry.MarkConsolidationNotRequired();
        _repository.Add(entry);
        await _repository.SaveChangesAsync();
```

Publisher still sends `lineGross` even when it is 0 (`ProcessZeroAmountCheckoutCommand.cs:112-120`). There is now a Billing test class (`ZeroAmountCheckoutHandlerTests`) but it only covers `OriginalAmount: 150m` / `DiscountAmount: 0m` (the **008** balance case). Grep of tests for `OriginalAmount: 0` is empty.

### Related files
- [`apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/ZeroAmountCheckoutHandler.cs`](apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/ZeroAmountCheckoutHandler.cs) — skip-lines-then-save.
- [`apps/lazuar-api/Modules/Commerce/Application/Commands/ProcessZeroAmountCheckoutCommand.cs`](apps/lazuar-api/Modules/Commerce/Application/Commands/ProcessZeroAmountCheckoutCommand.cs) — publishes `lineGross` (0 for a $0 price).
- [`apps/lazuar-api/Modules/Commerce/Application/Commands/InitiateCheckoutCommandHandler.cs`](apps/lazuar-api/Modules/Commerce/Application/Commands/InitiateCheckoutCommandHandler.cs) — `lineNet == 0` → ProcessZeroAmount when not vaulting (`:359-396`).
- [`apps/lazuar-api/Modules/Billing/Domain/AccountTypes.cs`](apps/lazuar-api/Modules/Billing/Domain/AccountTypes.cs) — `ZeroAmountCheckout = "ZERO_AMOUNT_CHECKOUT"`.
- [`apps/lazuar-api/tests/Lazuar.ModuleTests/Billing/EventHandlers/ZeroAmountCheckoutHandlerTests.cs`](apps/lazuar-api/tests/Lazuar.ModuleTests/Billing/EventHandlers/ZeroAmountCheckoutHandlerTests.cs) — 008 only.
- [`apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/CommerceProductCompletenessTests.cs`](apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/CommerceProductCompletenessTests.cs) — ProcessZeroAmount / trial publish; not `$0` list.

### Tests
- Existing: `ZeroAmountCheckoutHandlerTests.TrialWithListPriceAndZeroDiscount_Balances` (008). Commerce: `ProcessZeroAmount_YearlyHundredPercentCoupon_DoesNotThrow`, `ProcessZeroAmount_BillplzTrial_PublishesMatchingDiscount`, `ProcessZeroAmount_Recurring_ActivatesReminderOnly`.
- None fail on a no-line `$0` header. 008’s test would fail if someone removed the `OriginalAmount > 0` body, not if the empty-header path stayed.
- First regression: handle `OriginalAmount = 0` and assert **no** `LedgerEntry` is added (preferred) or, if a header is required for idempotency, that it is explicitly marked ignored and excluded from summary/`type_filter=sales`.

### Reproduction today
Arrange a product with `Price = 0` (or a 100% coupon on a 0 list — still 0) on a non-vaulting gateway (Billplz). Act: initiate checkout so `lineNet == 0` and `ProcessZeroAmount` runs. Assert: `billing.LedgerEntries` has `ReferenceType = ZERO_AMOUNT_CHECKOUT`, `ReferenceId = sessionId`, `Lines.Count = 0`. `GET /admin/billing/ledger?type_filter=sales` includes it (sales filter only excludes refund/cancel). Summary totals stay 0.

### Blast radius
Noise on the merchant ledger and a burned unique key per $0 session. Not a cash or tax error (`ValidateBalanced` holds; consolidation not required). Frequency: free products / $0 price rows, not normal SST catalog. Still P2 / harmless as the audit said.

### Suggested fix
If `OriginalAmount <= 0`, return before `Add` (idempotent no-op). Optionally publish nothing from Commerce when `lineGross == 0` and it is not a coupon/trial write-off. Do not re-open **008** (keep the “use list as discount” branch for `OriginalAmount > 0`). Do not book these as `GATEWAY_PAYMENT` (**075** is the opposite bug). No TypeSpec.

### Evaluation notes
Audit said “No test in Billing” — **008** added a Billing test, but not for this empty-header case. Still P2. Not blocked. Distinct from **075** (`$0` Stripe setup booked as GMV — resolved) and **008** (unbalanced trial — resolved).

