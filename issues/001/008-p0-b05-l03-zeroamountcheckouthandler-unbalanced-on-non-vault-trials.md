---
number: "008"
id: B05-L03
severity: P0
status: resolved
source: plans/009-bugs/05-billing-ledger-refunds-disputes.md
head: "297ba98"
resolved_branch: fix/008-zero-amount-trial-ledger
---

# 008 — B05-L03 — `ZeroAmountCheckoutHandler` unbalanced on non-vault trials

- **Severity:** P0
- **Status:** resolved
- **Source:** `plans/009-bugs/05-billing-ledger-refunds-disputes.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/008-zero-amount-trial-ledger`

Billplz/reminder-only trials now publish `DiscountAmount = list`. Billing treats any zero-amount checkout as 100% off so the journal balances.

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B05-L03 — P0 — `ZeroAmountCheckoutHandler` unbalanced on non-vault trials

**Where.** Non-vaulting recurring (Billplz, etc.) with `TrialDays > 0` goes to `ProcessZeroAmountCheckoutCommand`, not Stripe setup.

```103:111:apps/lazuar-api/Modules/Commerce/Application/Commands/ProcessZeroAmountCheckoutCommand.cs
        await _eventBus.PublishAsync(new ZeroAmountCheckoutCompletedIntegrationEvent(
            session.OrganizationId,
            session.Id,
            session.ClientProfileId,
            lineGross,      // catalog unit * qty  (e.g. 150)
            lineDiscount,   // 0 when there is no coupon
            product.Currency,
            couponCode,     // "NONE"
            new Dictionary<string, string>()));
```

`isTrial` zeroes `finalPrice` so Commerce will complete the session, but it does **not** set `DiscountAmount = lineGross`.

`ZeroAmountCheckoutHandler`:

```33:39:apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/ZeroAmountCheckoutHandler.cs
        if (@event.OriginalAmount > 0)
        {
            entry.AddLine(AccountTypes.ExpenseDiscount, @event.DiscountAmount, ...);
            entry.AddLine(AccountTypes.RevenueGross, -@event.OriginalAmount, ...);
        }

        entry.ValidateBalanced();
```

`0 + (−150) ≠ 0`. Throws. Inbox retries into dead-letter. The trial subscription in Commerce is already `ACTIVE` (save is in Commerce, before Billing sees the event). Billing has no journal. A 100% coupon on the same path publishes `OriginalAmount == DiscountAmount` and balances (`CommerceProductCompletenessTests` locks `300 == 300`). A `$0`-priced product publishes `OriginalAmount = 0`, skips lines, writes an **empty** balanced header (`ProcessZeroAmount_Recurring_ActivatesReminderOnly` uses `Price = 0`).

**Tests.** There is **no** `ZeroAmountCheckoutHandler` test in `Lazuar.ModuleTests/Billing` or `Modules.Billing.Tests`. The unbalance is unguarded.

---

