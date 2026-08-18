---
number: "228"
id: B05-L24
severity: P2
status: resolved
resolved_branch: fix/228-validate-balanced
source: plans/009-bugs/05-billing-ledger-refunds-disputes.md
head: "297ba98"
---

# 228 — B05-L24 — `ValidateBalanced` is a one-sided toy

- **Severity:** P2
- **Status:** resolved
- **Source:** `plans/009-bugs/05-billing-ledger-refunds-disputes.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/228-validate-balanced`

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B05-L24 — P2 — `ValidateBalanced` is a one-sided toy

Base only. No per-currency. No sign convention. Empty line list sums to 0 (the `$0`-price zero-checkout header). Comments claim 500-year-old certainty. There is no `LedgerEntryBalanceTests`. Coverage is handler composition. The method will not catch B05-L01, L05, L12, L13, L14.

---

## Evaluation (current tree, 2026-08-18)

### What the bug is
`LedgerEntry.ValidateBalanced` is the domain’s only double-entry guard. It sums `_lines.Select(l => l.BaseCurrencyAmount)` and throws if the total is not exactly `0`. It does not group by `Currency` or `BaseCurrency`, so a USD +100 line and a MYR −100 line “balance”. It does not check that each currency’s native `Amount` also nets to 0. It does not enforce sign convention (assets/expenses debit-positive, revenue/liability credit-negative) — two reversed-sign lines that cancel still pass. An entry with **no lines** sums to 0 and passes. The comment above the method still claims it is “impossible for Lazuar to lose track of a single cent” and invokes 500-year-old bookkeeping. Wrong-but-balanced journals (the shape of L01 double-reverse, L05 SST-as-revenue, L12 refund FX, L13 last-slice tax, L14 uncapped second refund) sail through. Those five P1s were fixed in **handlers** (007 / 010 / 082 / 083 / 084); the domain method still would not have caught them.

### Still present?
**STILL BROKEN**

```150:163:apps/lazuar-api/Modules/Billing/Domain/Aggregates/LedgerEntry.cs
    // This guarantees that it is impossible for Lazuar to lose track of a
    // single cent.
    //
    // NOTE: Double-entry bookkeeping is a 500-year-old accounting rule: Every
    // financial transaction has equal and opposite reactions. Debits and
    // Credits must always equal zero.
    public void ValidateBalanced()
    {
        var netBaseAmount = _lines.Sum(l => l.BaseCurrencyAmount);
        if (netBaseAmount != 0)
        {
            throw new InvalidOperationException($"Ledger entry {Id} is unbalanced. Net base currency amount: {netBaseAmount}");
        }
    }
```

Empty-line still passes — `$0` priced `ProcessZeroAmount` still skips `AddLine` when `OriginalAmount <= 0` and then calls `ValidateBalanced()` (`ZeroAmountCheckoutHandler.cs:33-42`; issue 237 / B05-L33). There is still **no** `LedgerEntryBalanceTests.cs`. `LedgerEntryAndAccountTypesTests` calls `ValidateBalanced()` once on a pre-balanced MYR pair and never asserts throw cases. `LedgerBalanceMatrixTests.AssertEntryBalanced` re-implements the same one-sided sum (`LedgerBalanceMatrixTests.cs:64-67`). Handler composition is stronger than in August (L01/L05/L12–L14 shipped) but it is still not a domain test of this method.

### Related files
- `apps/lazuar-api/Modules/Billing/Domain/Aggregates/LedgerEntry.cs` — the toy.
- `apps/lazuar-api/Modules/Billing/Domain/Entities/LedgerLine.cs` — `Amount` + `BaseCurrencyAmount` + `Currency`.
- `apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/ZeroAmountCheckoutHandler.cs` — empty header still “balanced”.
- `apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/GatewayPaymentCompletedHandler.cs` — calls `ValidateBalanced` after composing cash/fee/gross/tax.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Billing/Domain/LedgerEntryAndAccountTypesTests.cs` — one happy call.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Billing/EventHandlers/LedgerBalanceMatrixTests.cs` — handler composition, same sum.
- `issues/007-p0-b05-l01-full-b2b-refund-72h-double-reverses-cash-and-tax.md`, `010`, `082`, `083`, `084`, `237` — bugs this method cannot see.

### Tests
- Existing tests: `LedgerEntryAndAccountTypesTests.AssignB2cReceipt_SetsCustomerDocument_AndPendingConsolidation` (calls `ValidateBalanced` on 100/−100 MYR). `LedgerBalanceMatrixTests` (payment / refund / top-up composition). Many handler tests call `entry.ValidateBalanced()` as a fixture setup, not as the unit under test.
- Whether any test would fail if the bug is still there: **no**. Nothing asserts “empty lines throw”, “USD 100 + MYR −100 throw”, or “two debit-positive revenue lines that cancel throw”.
- What a first regression test should assert (`LedgerEntryBalanceTests`): empty lines → throw (or a named `IsEmpty` skip used by 237). Two lines same `BaseCurrencyAmount` signs that cancel but different `Currency` → throw. Native `Amount` net ≠ 0 while base nets to 0 (FX mismatch) → throw. A balanced GMV sale fixture still passes. Do **not** re-test L01/L12 in this class — those are handler tests.

### Reproduction today
Arrange: `new LedgerEntry(...)` with no `AddLine` — `ValidateBalanced()` returns. Add `ASSET_CASH +100 USD` base 100 and `REVENUE_GROSS −100 MYR` base −100 — `ValidateBalanced()` returns. Add two `REVENUE_GROSS +50` / `REVENUE_GROSS −50` (wrong accounts, net 0) — returns. Add `ASSET_CASH +100` only — throws (the one case it catches). Assert the comment’s “impossible to lose a cent” is false for the first three.

### Blast radius
Every Billing writer (`GatewayPaymentCompletedHandler`, refund, chargeback, SaaS, top-up, LHDN cancel, commission, zero-amount, manual enroll, recognition job). A future handler can ship a cross-currency or empty journal and production will persist it. Money can look balanced in `GetFinancialSummaryAsync` (which also sums `BaseCurrencyAmount` only — 230). Not PII. Frequency: latent until the next handler bug; the August P1s proved the shape.

### Suggested fix
Tighten `ValidateBalanced` in-place: (1) reject `_lines.Count == 0` unless a caller opts into `allowEmpty` (do not silently accept 237); (2) require each `(Currency)` group’s `Amount` sum == 0 **and** each `(BaseCurrency)` group’s `BaseCurrencyAmount` sum == 0; (3) delete or rewrite the “impossible to lose a cent” comment. Optional sign-convention table is larger — do not block (1)+(2) on it. Add `LedgerEntryBalanceTests`. Do not reopen 007/010/082–084. No TypeSpec. No Xero.

### Evaluation notes
Duplicates: 237 (empty `$0` header is the empty-list case). L01/L05/L12–L14 are **fixed at handlers**; this issue is the remaining domain toy. Severity still **P2** (the method never was the money hole; it is a false safety net). Not blocked. Residual after 161-200 / 007–084: comments still over-claim; tests still do not pin the method’s weaknesses.


