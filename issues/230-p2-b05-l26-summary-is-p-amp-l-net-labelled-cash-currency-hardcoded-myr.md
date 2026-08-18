---
number: "230"
id: B05-L26
severity: P2
status: resolved
resolved_branch: fix/230-summary-cash-label
source: plans/009-bugs/05-billing-ledger-refunds-disputes.md
head: "297ba98"
---

# 230 — B05-L26 — Summary is P&amp;L net, labelled cash, currency hardcoded MYR

- **Severity:** P2
- **Status:** resolved
- **Source:** `plans/009-bugs/05-billing-ledger-refunds-disputes.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/230-summary-cash-label`

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

## Evaluation (current tree, 2026-08-18)

### What the bug is
`GetFinancialSummaryAsync` computes a signed P&amp;L-style net (gross − contra refunds − discounts − gateway fees − tax liabilities) and names the column `Net_revenue`. It ignores `EXPENSE_SOFTWARE_SUBSCRIPTION` (Hub SaaS + utility packs), `EXPENSE_COMMISSION`, and `ASSET_CASH`. The SQL literal `'MYR'` is the currency regardless of line currency. Dates work if the HTTP caller passes `from_date` / `to_date`. The agent tool `GetFinancialHealthAgentQuery` and the ops dashboard card do **not** pass dates, and they label that field **Net Cash in Bank**. Billing README golden rule is “never compute cash from Commerce logs” and “Net Profit = cash after fees”; the type name `NetCashInBank` is filled from `summary.Net_revenue`. `GetNetProfitAsync` subtracts commission (and still ignores Hub/pack expense, still ignores `ASSET_CASH`, still hardcodes MYR). Real cash in the ledger is `SUM(ASSET_CASH)`. A utility top-up books `EXPENSE_SOFTWARE_SUBSCRIPTION` + negative `ASSET_CASH` and never touches `Net_revenue` — so “cash in bank” does not move when the merchant spends RM 50 on credits. The wrong number is born in Billing; slice 09 only prints it.

### Still present?
**STILL BROKEN**

Summary SQL is unchanged:

```137:151:apps/lazuar-api/Modules/Billing/Infrastructure/Services/BillingQueryService.cs
        var sqlBuilder = new StringBuilder($@"
            SELECT 
                COALESCE(-SUM(CASE WHEN ""AccountType"" = '{AccountTypes.RevenueGross}' THEN ""BaseCurrencyAmount"" ELSE 0 END), 0) as ""Gross_revenue"",
                COALESCE(SUM(CASE WHEN ""AccountType"" = '{AccountTypes.ExpenseGatewayFee}' THEN ""BaseCurrencyAmount"" ELSE 0 END), 0) as ""Total_gateway_fees"",
                COALESCE(-SUM(CASE WHEN ""AccountType"" = '{AccountTypes.LiabilityTaxPayable}' THEN ""BaseCurrencyAmount"" ELSE 0 END), 0) as ""Total_tax_liabilities"",
                (
                    COALESCE(-SUM(CASE WHEN ""AccountType"" = '{AccountTypes.RevenueGross}' THEN ""BaseCurrencyAmount"" ELSE 0 END), 0)
                  - COALESCE(SUM(CASE WHEN ""AccountType"" = '{AccountTypes.ContraRevenueRefunds}' THEN ""BaseCurrencyAmount"" ELSE 0 END), 0)
                  - COALESCE(SUM(CASE WHEN ""AccountType"" = '{AccountTypes.ExpenseDiscount}' THEN ""BaseCurrencyAmount"" ELSE 0 END), 0)
                  - COALESCE(SUM(CASE WHEN ""AccountType"" = '{AccountTypes.ExpenseGatewayFee}' THEN ""BaseCurrencyAmount"" ELSE 0 END), 0)
                  - COALESCE(-SUM(CASE WHEN ""AccountType"" = '{AccountTypes.LiabilityTaxPayable}' THEN ""BaseCurrencyAmount"" ELSE 0 END), 0)
                ) as ""Net_revenue"",
                // ...
                'MYR' as ""Currency""
```

Agent still maps that onto cash, with **no date range**:

```10:16:apps/lazuar-api/Modules/Billing/Application/Queries/Agent/GetFinancialHealthAgentQuery.cs
[AgentTool("Retrieve the exact financial health of the workspace, including Gross Revenue, Net Cash in Bank, ...
    double NetCashInBank,
```

```33:36:apps/lazuar-api/Modules/Billing/Application/Queries/Agent/GetFinancialHealthAgentQuery.cs
        var summary = await _billingQueryService.GetFinancialSummaryAsync(request.OrganizationId);
        return new AgentFinancialHealthResult(
            (double)summary.Gross_revenue,
            (double)summary.Net_revenue,
```

Ops card still prints it as cash (`DashboardPage.tsx:27-34, 79` → `GET /admin/billing/summary` → `net_revenue` as “Net Cash in Bank”). `GetNetProfitAsync` (`BillingQueryService.cs:189-201`) subtracts `EXPENSE_COMMISSION` but not `EXPENSE_SOFTWARE_SUBSCRIPTION` / `ASSET_CASH`, currency still `'MYR'`. Integration test **locks** ignoring Hub/pack expense:

```21:21:apps/lazuar-api/tests/Lazuar.IntegrationTests/BillingQueryServiceTests.cs
    public async Task GetFinancialSummaryAsync_ShouldCalculateNetRevenueCorrectly_AndIgnoreOperationalExpenses()
```

```105:107:apps/lazuar-api/tests/Lazuar.IntegrationTests/BillingQueryServiceTests.cs
            // Expected Net Revenue = Gross (100) - Fee (5) - Tax (10) = 85.
            // The 50 MYR operational expense (Top-Up) MUST be completely ignored.
            summary.Net_revenue.Should().Be(85);
```

README still sells net profit as “actual cash in the bank” (`Modules/Billing/README.md:9`). Prompt libraries in ops/admin still ask “exact Net Cash in Bank after deducting Stripe and Billplz gateway fees”.

### Related files
- `apps/lazuar-api/Modules/Billing/Infrastructure/Services/BillingQueryService.cs` — formula + hardcoded MYR.
- `apps/lazuar-api/Modules/Billing/Application/Queries/Agent/GetFinancialHealthAgentQuery.cs` — `NetCashInBank = Net_revenue`.
- `apps/lazuar-api/Modules/Billing/Infrastructure/Endpoints/AdminLedgerEndpoints.cs` — `/summary`, `/net-profit`.
- `apps/lazuar-api/Modules/Billing/README.md` — golden-rule copy.
- `apps/lazuar-api/Modules/Billing/Application/Llm/BillingPromptProvider.cs` / `Modules/Ops/Infrastructure/Services/LlmOrchestratorService.Prompts.cs` — CRITICAL RULE 6.
- `apps/lazuar-ops/src/modules/commerce/pages/DashboardPage.tsx` — card label.
- `apps/lazuar-ops/src/lib/prompt-library.ts` / `apps/lazuar-admin/src/lib/prompt-library.ts`.
- `apps/lazuar-api/tests/Lazuar.IntegrationTests/BillingQueryServiceTests.cs` — locks ignore-Hub.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Billing/EventHandlers/LedgerBalanceMatrixTests.cs` — mirrors the same net formula.
- `packages/api-types-dotnet/Lazuar.ApiContracts.cs` — `FinancialSummaryDto.Net_revenue` is `double` (224-class leak).

### Tests
- Existing tests: `BillingQueryServiceTests.GetFinancialSummaryAsync_ShouldCalculateNetRevenueCorrectly_AndIgnoreOperationalExpenses`; `GetFinancialSummaryAsync_AfterPartialRefund_NetsContraRevenueCorrectly` (net 40 after half refund). `LedgerBalanceMatrixTests.ComputeSummary` copies the same operators. No test that `NetCashInBank == SUM(ASSET_CASH)`. No frontend test of the dashboard card (no ops test tree).
- Whether any test would fail if the bug is still there: **no**. The integration test would **fail if you started subtracting Hub/pack expense** from `Net_revenue` without renaming the field.
- What a first regression test should assert: either (a) honesty — agent/dashboard field is `NetRevenue` / copy says “P&amp;L net, not cash”, and `ASSET_CASH` is a separate figure; or (b) cash — `NetCashInBank` equals `SUM(ASSET_CASH)` for a sale + a RM 50 top-up (top-up must move cash). Do not keep both the name “cash” and the ignore-operational-expenses assertion.

### Reproduction today
Arrange: tenant with one RM 100 sale (fee 5, tax 10) and one RM 50 utility top-up (already the integration fixture). Act: `GET /admin/billing/summary` (no dates) and open ops Dashboard. Assert `net_revenue == 85` and the card reads “Net Cash in Bank RM 85.00”. The ledger’s `ASSET_CASH` is `+85` on the sale (if fee was booked) **minus 50** on the top-up → 35, which nobody displays. Ask the agent “what is our net cash in bank?” — it returns 85.

### Blast radius
Every merchant who looks at ops Dashboard or asks the agent about cash. Utility-credit and Hub-SaaS spend is invisible on the card; commissions are invisible on `/summary` but present on `/net-profit`. Multi-currency tenants always see `MYR`. Not a fulfillment bug. Not PII. Frequency: every dashboard load. Inherited fee-0 lie from 222/239 makes even the P&amp;L net optimistic.

### Suggested fix
Smallest honesty fix (preferred, no TypeSpec regen): rename agent field usage and dashboard label to “Net revenue (after fees &amp; tax)” and stop saying “cash in bank”. Keep `Net_revenue` math (the integration test can stay). Add a separate `Cash_balance = SUM(ASSET_CASH)` if ops actually wants cash — that *would* see Hub/pack. Do not silently fold `EXPENSE_SOFTWARE_SUBSCRIPTION` into `Net_revenue` while the card still says cash (that would disagree with the locked test and still not be cash). `GetNetProfitAsync` can keep commission. Hardcoded `'MYR'`: use `MAX(BaseCurrency)` or fail if more than one base currency exists. Dashboard chrome is slice 09 — change the label there only after Billing exposes an honest name. No Xero. No Stripe `subscription.updated`.

### Evaluation notes
Duplicates: 222 / 239 (fee 0 inflates this net); 225 (README); 231 (sales filter mixes planes into the same ledger the card reads). Severity still **P2** (wrong label / incomplete P&amp;L, not lost capture). Not blocked. Residual after 161-200: 144 (member 403 not RM 0.00) fixed a different dashboard lie; this number is still born wrong. `LedgerBalanceMatrixTests` comment says the formula is what “ops net revenue stays believable” — it is believable as **net revenue**, not as cash.


