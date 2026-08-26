---
number: "010"
id: B05-L05
severity: P0
status: resolved
source: plans/009-bugs/05-billing-ledger-refunds-disputes.md
head: "297ba98"
resolved_branch: fix/010-renewal-sst-tax-payable
---

# 010 — B05-L05 — Commerce SST on renewals never hits `LIABILITY_TAX_PAYABLE`

- **Severity:** P0
- **Status:** resolved
- **Source:** `plans/009-bugs/05-billing-ledger-refunds-disputes.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/010-renewal-sst-tax-payable`

Renewal checkout, PAST_DUE pay links, and off-session charges now stamp `sst_tax_amount` / `sst_tax_type`. Billing splits 108 into revenue 100 + tax payable 8 when the gateway TaxAmount is 0.

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B05-L05 — P0 — Commerce SST on renewals never hits `LIABILITY_TAX_PAYABLE`

**Where.** See §9. Hop-1 metadata is the only SST feed into Billing. Off-session, dunning, and renewal hosted checkouts charge `SubscriptionBillingAmount.Gross` (net + SST) and stamp no `sst_tax_amount`. Stripe PI parser sets `TaxAmount: 0`. Billing books the whole gross as `REVENUE_GROSS`.

A registered SST merchant’s month-2 charge of 108 (100 + 8) is:

| Account | Booked | Should be |
|---------|--------|-----------|
| `REVENUE_GROSS` | −108 | −100 |
| `LIABILITY_TAX_PAYABLE` | 0 | −8 |
| Summary net | 108 − fees | 100 − fees − 8 |

Tax payable is understated for the life of the subscription after month 1. This is the opposite of “tax booked twice”: Commerce collected SST, Billing recognized it as income.

`eba0741` (`fix(commerce): charge SST on renewals and dunning`) made the **charge** correct and left the **journal** blind.

**Tests.** No Billing test that a renewal-shaped event (no `sst_tax_*`, `TaxAmount = 0`, `AmountPaid = 108`) splits tax. `LedgerBalanceMatrixTests` always passes `TaxAmount: 8` on the hop-1-shaped event.

---

