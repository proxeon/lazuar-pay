---
number: "051"
id: B03-C06
severity: P1
status: resolved
source: plans/009-bugs/03-commerce-dunning-arrears-portal.md
head: "297ba98"
resolved_branch: fix/051-arrears-row-reminder-only
---

# 051 — B03-C06 — Arrears `is_reminder_only` is gateway-derived; Stripe reminder-only is sold as “update card”

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/03-commerce-dunning-arrears-portal.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/051-arrears-row-reminder-only`

GET/POST arrears use `Subscription.IsReminderOnly`. ACTIVE reminder-only seats cannot update a card.

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B03-C06 — P1 — Arrears `is_reminder_only` is gateway-derived; Stripe reminder-only is sold as “update card”

**Evidence.** GET line 65; POST 109–113 uses the same helper. Row flag is `Subscription.IsReminderOnly`. `ProcessZeroAmountCheckoutCommand` still starts recurring subs with `reminderOnly: true` (line 93) even on Stripe (008 P0-3; 8b3567d vaulted a **different** $0 path). Portal hides the button using the **row** flag (`portal/page.tsx` 172). The update-payment **page** uses the GET DTO flag. A buyer who follows an email (always the GUID page) sees the RM 1 form. POST allows it. Success calls `StoreVaultedToken` which **clears** `IsReminderOnly` (`Subscription.cs` 279–284). Next cycle is a live off-session Gross of catalog price if `UnitAmount` is 0 (B03-C08).

**Repro.** 100% coupon Stripe monthly. Email “update payment”. Page says RM 1. Pay. Sub is no longer reminder-only.

**Blast.** Invoice-only Stripe buyers are converted to auto-debit without a hop-1 consent that said so.

**Tests.** 008 already named this. Still no test that GET `is_reminder_only` equals the row.

**Fix direction.** Return `s.IsReminderOnly`. POST must refuse `ACTIVE && sub.IsReminderOnly`, not just Billplz.

---

