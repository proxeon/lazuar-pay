---
number: "230"
id: B05-L26
severity: P2
status: open
source: plans/009-bugs/05-billing-ledger-refunds-disputes.md
head: "297ba98"
---

# 230 — B05-L26 — Summary is P&amp;L net, labelled cash, currency hardcoded MYR

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/05-billing-ledger-refunds-disputes.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B05-L26 — P2 — Summary is P&amp;L net, labelled cash, currency hardcoded MYR

`GetFinancialSummaryAsync` (`:137-151`):

```
Net_revenue = −SUM(REVENUE_GROSS)
            − SUM(CONTRA_REVENUE_REFUNDS)
            − SUM(EXPENSE_DISCOUNT)
            − SUM(EXPENSE_GATEWAY_FEE)
            − (−SUM(LIABILITY_TAX_PAYABLE))
```

Ignores `EXPENSE_SOFTWARE_SUBSCRIPTION` (Hub + packs), `EXPENSE_COMMISSION`, `ASSET_CASH`. Hardcodes `'MYR'`. Dates work if the caller passes them. The agent tool and ops “Net Cash in Bank” card do not.

`GetFinancialHealthAgentQueryHandler` fills `NetCashInBank` from `summary.Net_revenue`. README golden rule is broken at the type name.

`GetNetProfitAsync` **does** subtract commission, still ignores Hub/pack expense, still no `ASSET_CASH`.

Dashboard chrome is slice 09. The **wrong number is born here**.

---

