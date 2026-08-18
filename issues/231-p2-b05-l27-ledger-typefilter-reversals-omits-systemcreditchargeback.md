---
number: "231"
id: B05-L27
severity: P2
status: resolved
resolved_branch: fix/231-ledger-type-filter
source: plans/009-bugs/05-billing-ledger-refunds-disputes.md
head: "297ba98"
---

# 231 — B05-L27 — Ledger `type_filter=reversals` omits `SYSTEM_CREDIT_CHARGEBACK`

- **Severity:** P2
- **Status:** resolved
- **Source:** `plans/009-bugs/05-billing-ledger-refunds-disputes.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/231-ledger-type-filter`

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B05-L27 — P2 — Ledger `type_filter=reversals` omits `SYSTEM_CREDIT_CHARGEBACK`

`BillingQueryService.cs:56-63`: `reversals` = `GATEWAY_REFUND` + `LHDN_CANCELLATION` only. Credit Notes page is this filter. Utility chargebacks do not appear. `sales` excludes those two and therefore **includes** chargebacks, SaaS fees, top-ups, commissions, zero-checkouts.

---

## Evaluation (current tree, 2026-08-18)

### What the bug is
`GET /admin/billing/ledger?type_filter=reversals` is the Credit Notes page query. The SQL allow-list is `ReferenceType IN ('GATEWAY_REFUND', 'LHDN_CANCELLATION')`. Utility-credit chargebacks persist as `SYSTEM_CREDIT_CHARGEBACK` (`ChargebackClawbackHandler` after 009). GMV lost disputes persist as `GATEWAY_DISPUTE` (086). Neither type is in the reversals list, so those rows never appear on Credit Notes. `type_filter=sales` is `NOT IN ('GATEWAY_REFUND', 'LHDN_CANCELLATION')`, so chargebacks, Hub SaaS fees (`SYSTEM_SAAS_FEE`), top-ups (`SYSTEM_CREDIT_TOPUP`), commissions (`COMMISSION_ACCRUED`), and `$0` coupons (`ZERO_AMOUNT_CHECKOUT`) all render on Tax Invoices / sales documents. Ops cannot find a utility claw in the CN list and may see it (or a SaaS fee) in the sales list.

### Still present?
**STILL BROKEN**

```56:63:apps/lazuar-api/Modules/Billing/Infrastructure/Services/BillingQueryService.cs
        if (typeFilter == "sales")
        {
            sqlBuilder.Append($@" AND e.""ReferenceType"" NOT IN ('{LedgerReferenceTypes.GatewayRefund}', '{LedgerReferenceTypes.LhdnCancellation}')");
        }
        else if (typeFilter == "reversals")
        {
            sqlBuilder.Append($@" AND e.""ReferenceType"" IN ('{LedgerReferenceTypes.GatewayRefund}', '{LedgerReferenceTypes.LhdnCancellation}')");
        }
```

Chargeback writer still uses the omitted type:

```160:198:apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/ChargebackClawbackHandler.cs
        var referenceType = LedgerReferenceTypes.SystemCreditChargeback;
        // ...
            "Posted SYSTEM_CREDIT_CHARGEBACK ledger reverse for tenant {TenantId} gateway tx {GatewayTxId}.",
```

```62:62:apps/lazuar-api/Modules/Billing/Domain/AccountTypes.cs
    public const string SystemCreditChargeback = "SYSTEM_CREDIT_CHARGEBACK";
```

`LedgerReferenceTypes.GatewayDispute` exists (`AccountTypes.cs:59`) after 086 and is also absent from both lists’ intent (it rides in **sales** via the NOT IN). Credit Notes page hard-codes the filter:

```40:40:apps/lazuar-ops/src/modules/invoicing/pages/CreditNotesPage.tsx
            type_filter: "reversals"
```

Sales documents:

```41:41:apps/lazuar-ops/src/modules/invoicing/pages/TaxInvoicesPage.tsx
            type_filter: "sales"
```

Endpoint is a pass-through (`AdminLedgerEndpoints.cs:24-32`). I found no Billing test that calls `GetLedgerEntriesAsync` with `type_filter`. `LedgerEntryAndAccountTypesTests.AccountTypes_ExposeExpectedChartCodes` only asserts the constant string.

### Related files
- `apps/lazuar-api/Modules/Billing/Infrastructure/Services/BillingQueryService.cs` — the filter.
- `apps/lazuar-api/Modules/Billing/Domain/AccountTypes.cs` — `LedgerReferenceTypes`.
- `apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/ChargebackClawbackHandler.cs` — writes `SYSTEM_CREDIT_CHARGEBACK`.
- `apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/GatewayDisputeLostHandler.cs` — GMV `GATEWAY_DISPUTE` (086).
- `apps/lazuar-api/Modules/Billing/Infrastructure/Endpoints/AdminLedgerEndpoints.cs` — `/ledger`.
- `apps/lazuar-ops/src/modules/invoicing/pages/CreditNotesPage.tsx` — CN UI.
- `apps/lazuar-ops/src/modules/invoicing/pages/TaxInvoicesPage.tsx` — sales UI.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Billing/EventHandlers/ChargebackClawbackHandlerTests.cs` — posts the row, does not query the filter.
- `apps/lazuar-api/tests/Lazuar.IntegrationTests/BillingQueryServiceTests.cs` — summary only.
- `issues/009-p0-b05-l04-utility-chargeback-claw-is-not-idempotent.md`, `086`, `243`.

### Tests
- Existing tests: `ChargebackClawbackHandlerTests` (idempotent claw + ledger reverse). `LedgerEntryAndAccountTypesTests.AccountTypes_ExposeExpectedChartCodes` (`SystemCreditChargeback == "SYSTEM_CREDIT_CHARGEBACK"`). No `GetLedgerEntriesAsync` / `type_filter` test. No ops frontend test.
- Whether any test would fail if the bug is still there: **no**.
- What a first regression test should assert: seed `GATEWAY_REFUND`, `LHDN_CANCELLATION`, `SYSTEM_CREDIT_CHARGEBACK`, `GATEWAY_DISPUTE`, `GATEWAY_PAYMENT`, `SYSTEM_SAAS_FEE`. `type_filter=reversals` returns the first three (and dispute if product wants CN-like treatment) and **not** the sale / SaaS fee. `type_filter=sales` returns `GATEWAY_PAYMENT` only (or payment + manual enroll), **not** chargebacks / top-ups / SaaS / commission / zero-checkout.

### Reproduction today
Arrange: utility top-up then a Stripe `charge.dispute.created` so `ChargebackClawbackHandler` inserts `SYSTEM_CREDIT_CHARGEBACK` (009 path). Optionally a GMV lost dispute (`GATEWAY_DISPUTE`, 086) and a normal `GATEWAY_REFUND` CN. Act: open ops Credit Notes (`type_filter=reversals`). Assert the utility claw and GMV dispute rows are missing; only the refund / LHDN cancel show. Open Sales documents (`type_filter=sales`) — the claw / SaaS fee / top-up / `$0` coupon header appear in the same list as receipts.

### Blast radius
Ops finance users, not buyers. Utility chargebacks (and GMV dispute journals) are easy to miss when reconciling CNs; sales list is noisy. No money movement changes. No extra PII (same ledger rows, wrong tab). Frequency: every dispute/claw vs every CN page view. After 009 the claw **exists**; this issue is that the CN page cannot see it.

### Suggested fix
Extend the reversals `IN` list with `LedgerReferenceTypes.SystemCreditChargeback` (this issue’s title). Decide explicitly whether `GATEWAY_DISPUTE` belongs on CNs or a disputes tab — do not leave it in `sales` by accident. Tighten `sales` to an allow-list (`GATEWAY_PAYMENT`, `MANUAL_ENROLLMENT`) rather than a two-type NOT IN. Keep filter construction using the constants (already). No TypeSpec regen required if `type_filter` stays an untyped string. No Xero. Do not mark-refund in this change.

### Evaluation notes
Duplicates: 009 (claw exists); 086 (GMV dispute journal); 243 (stale “utility only” comment); 237 (`ZERO_AMOUNT_CHECKOUT` noise in sales). Severity still **P2** (visibility). Not blocked — 009/086 already write the rows. Residual after 161-200: none of the fail-closed work touched this query.


