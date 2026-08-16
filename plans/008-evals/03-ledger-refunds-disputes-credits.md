# 03 — Billing money after Waves 0–4: ledger, refunds, disputes, Hub SaaS fee, credits

**Date:** 16 August 2026  
**Branch:** `feat/007-waves-1-4-implement` (per [008-evals/README.md](./README.md))  
**Scope:** code as it is now. `plans/007-feats` tracker cells and August 16 competitor inventory are historical. Wave `*-done.md` notes are used only as a map of what the waves *claimed*; every claim below is re-checked against the files.

This report is uncondensed. It is the evidence file for the parent evaluation’s money slice. It does not implement anything.

Surfaces in this slice:

| Layer | Job after Waves 0–4 |
|-------|---------------------|
| `Modules/Billing` | Double-entry `LedgerEntry` / `LedgerLine`, document series, Hub SaaS fee, utility wallet, financial summary SQL |
| `Modules/Commerce` | `RecordRefund`, transaction log statuses, CSV export, GMV dispute row, stats/MRR |
| `Modules/Payments` | `IssueRefundAsync`, `GatewayRefundRequested` adapter call, webhook allow-list (no inbound refund event) |
| `Modules/Lhdn` | Full-refund cancel &lt;72h / type-02 CN ≥72h; live credit deduct on `SubmitTaxDocumentCommand` |
| `Modules/Messaging` | WhatsApp dispatch; console transport is not billable |
| `lazuar-ops` | Sales Insights KPIs, Transactions + Refund modal, Disputes list, Plan & billing, Credit Notes (reversals filter) |

---

## 1. What this slice is (and is not)

Lazuar is BYOK. Guest GMV settles to the tenant’s Stripe / Billplz / CHIP / Razorpay / Xendit account. Billing is supposed to be the **tenant-facing journal** of that money, plus two **platform-collected** planes that use Lazuar’s keys:

- Plane U — `utility_credit_topup` (prepaid credits).  
- Plane S — `platform_saas_fee` (Hub software fee).

Those two strings live in `PlatformCheckoutTypes` (`apps/lazuar-api/Modules/Payments/Contracts/PlatformCheckoutTypes.cs:12-19`). They are the only values `IsPlatformCollected` returns true for. Commerce GMV metadata such as `saas_subscription` is **not** platform-collected and still books `GATEWAY_PAYMENT` / `REVENUE_GROSS` (`LedgerBalanceMatrixTests.CommerceSaasSubscriptionMetadata_StillTakesGmvPath`).

The Billing README still states the golden rule: never calculate MRR, net cash, or tax payable from Commerce logs alone (`apps/lazuar-api/Modules/Billing/README.md:60-64`). The live dashboard violates that rule for MRR (Commerce subscription snapshot) and violates it in a different way for “Net Cash in Bank” (it *does* use the ledger, but it labels ledger **net revenue** as cash). Both are documented in §10.

Waves 0–4 closed the worst pre-wave lies on this slice: refund amounts are no longer hard-coded to 0, `GatewayRefundFailed` now has a Commerce consumer, Billplz cannot pretend to API-refund, document numbers are `RCPT`/`INV`/`CN` instead of LHDN UUIDs, tax is reversed on refunds, WhatsApp console is not billed, and Hub SaaS is a separate plane that does not mint credits. They did **not** make the ledger audit-grade across refund + LHDN cancel, and they deliberately reused `GatewayRefundCompleted` as the GMV chargeback contra. That reuse is the largest remaining money bug.

---

## 2. Double-entry `LedgerEntry` and document numbers (`RCPT` / `INV` / `CN`)

### 2.1 The aggregate

`LedgerEntry` is the journal header (`apps/lazuar-api/Modules/Billing/Domain/Aggregates/LedgerEntry.cs:9-40`):

- `OrganizationId` (tenant).  
- `Timestamp` set to `DateTime.UtcNow` in the constructor (`:51`) — realization time, not subscription `NextBillingDate`.  
- `ReferenceType` + `ReferenceId` — idempotency key. Unique index is **global**, not per org (`BillingDbContext.cs:66`: `HasIndex(x => new { x.ReferenceType, x.ReferenceId }).IsUnique()`).  
- `CustomerDocumentNumber` — immutable customer-facing number. Assign methods use `??=` and never overwrite (`:72-73`, `:90`, `:102-103`).  
- `LhdnDocumentUuid` / `LhdnValidationStatus` / `ConsolidationStatus` — tax lifecycle, separate from the printed number.  
- `TaxInvoiceId` — **legacy dual-use**. Comment at `:18-23` says it used to hold receipt #, LHDN UUID, **and** consolidation ref. `UpdateLhdnStatus` still writes the UUID into `TaxInvoiceId` (`:142-147`). `MarkConsolidatedPending` overwrites `TaxInvoiceId` with the batch ref (`:129-134`) but not `CustomerDocumentNumber`. New readers must prefer `CustomerDocumentNumber` + `LhdnDocumentUuid`. Migration `20260804021522_SeparateReceiptAndConsolidationFields` exists specifically because of this.

`LedgerLine` is an immutable child (`apps/lazuar-api/Modules/Billing/Domain/Entities/LedgerLine.cs:7-33`): `AccountType`, signed `Amount`, `Currency`, signed `BaseCurrencyAmount`, `BaseCurrency`, `TaxTypeCode`, `MsicCode`. `BillingDbContext.SaveChangesAsync` (`:46-51`) forces `LedgerLine` `Modified` → `Added` so a line cannot be silently edited in place.

`AddLine` defaults every line to tax type `"06"` (Not Applicable) and MSIC `"004"` (consolidated B2C class) (`LedgerEntry.cs:57`). Callers that need SST `02` or B2B class `022` must override. `GatewayPaymentCompletedHandler` does that for B2B (`msic = isB2b ? "022" : "004"`, `GatewayPaymentCompletedHandler.cs:69`) and for SST metadata (`ResolveTaxType`, `:177-188`). Refund, top-up, SaaS, and clawback lines keep the default `06`/`004` unless they copy original line codes (clawback copies; refund does not).

### 2.2 Balance guard

`ValidateBalanced` (`LedgerEntry.cs:152-165`) sums `_lines.Sum(l => l.BaseCurrencyAmount)` and throws if the net is not exactly `0`. Comments call this “impossible for Lazuar to lose track of a single cent” and invoke 500-year-old double-entry.

What it actually checks:

- **Only** base-currency amounts. A line can have `Amount` and `BaseCurrencyAmount` that disagree; only the base sum is guarded.  
- **No** per-currency check. Multi-currency journals could balance in MYR and be unbalanced in the original currency.  
- **No** debit/credit classification. The chart is a signed convention: credit-normal accounts (revenue, tax payable, deferred) are booked **negative**; debit-normal (cash, fees, contra-refunds, expenses) are booked **positive**. The guard does not know that convention. A journal that puts cash and revenue on the same sign would still “balance” if the numbers cancel.  
- **No** rounding policy beyond whatever the caller put on the line. Refund tax uses `MidpointRounding.AwayFromZero` to 4 dp (`GatewayRefundCompletedHandler.cs:124`). Payment FX is `amount * fxRate` with no explicit round.

There is no unique test class named `LedgerEntryBalanceTests`. Coverage is `LedgerBalanceMatrixTests` (handler composition) plus `GatewayRefundCompletedHandlerTests` and `ValidateBalanced()` calls inside handlers. The method itself is a one-liner; the risk is **wrong but balanced** journals, not unbalanced ones.

### 2.3 Chart of accounts and reference types

`AccountTypes` (`apps/lazuar-api/Modules/Billing/Domain/AccountTypes.cs:7-24`):

| Constant | Typical sign on a sale | Typical sign on a refund |
|----------|------------------------|--------------------------|
| `ASSET_CASH` | + net settled | − cash outflow |
| `ASSET_ACCOUNTS_RECEIVABLE` | + on `INVOICE_ISSUED` (dead path) | — |
| `LIABILITY_TAX_PAYABLE` | − tax collected | + tax reversed |
| `LIABILITY_DEFERRED_REVENUE` | − on invoice-issued (dead) | — |
| `LIABILITY_AFFILIATE_PAYABLE` | − on commission | — |
| `REVENUE_GROSS` | − catalog/gross | not touched; contra used instead |
| `REVENUE_RECOGNIZED` | would be − when recognition runs | recognition job unregistered |
| `CONTRA_REVENUE_REFUNDS` | — | + gross refunded |
| `EXPENSE_GATEWAY_FEE` | + fee | − only if `RefundedFee > 0` (today always 0) |
| `EXPENSE_DISCOUNT` | + 100% coupon | — |
| `EXPENSE_COMMISSION` | + affiliate | — |
| `EXPENSE_SOFTWARE_SUBSCRIPTION` | + Hub fee / credit top-up (tenant paying Lazuar) | reversed on utility chargeback |

`LedgerReferenceTypes` (`AccountTypes.cs:55-66`):

| Type | Writer | Idempotency `ReferenceId` |
|------|--------|---------------------------|
| `GATEWAY_PAYMENT` | `GatewayPaymentCompletedHandler` | gateway transaction id |
| `GATEWAY_REFUND` | `GatewayRefundCompletedHandler` | `{PaymentRecordId:N}:{event.Id:N}` (per attempt) |
| `MANUAL_ENROLLMENT` | `ManualSubscriberEnrolledIntegrationEventHandler` | transaction log id (or subscription id if empty Guid) |
| `SYSTEM_CREDIT_TOPUP` | `PlatformTopUpEventHandler` | gateway transaction id |
| `SYSTEM_CREDIT_CHARGEBACK` | `ChargebackClawbackHandler` | gateway transaction id |
| `SYSTEM_SAAS_FEE` | `PlatformSaasFeeHandler` | gateway transaction id |
| `LHDN_CANCELLATION` | `LhdnDocumentCancelledIntegrationEventHandler` | LHDN internal reference (usually `INV-…`) |
| `INVOICE_ISSUED` | `InvoiceIssuedHandler` | invoice number — **event is never published in production** |
| `ZERO_AMOUNT_CHECKOUT` | `ZeroAmountCheckoutHandler` | checkout session id |
| `COMMISSION_ACCRUED` | `CommissionAccruedHandler` | commission id |

`HasEntryBeenProcessedAsync` (`LedgerRepository.cs:18-24`) is `IgnoreQueryFilters()` + `AnyAsync` on `(ReferenceType, ReferenceId)` **without** `OrganizationId`. Combined with the unique index, a second org that reused the same gateway id would fail the insert, not silently book a second journal. BYOK Stripe PaymentIntent ids are globally unique per account; the index is still the wrong grain.

### 2.4 Document series: `RCPT` / `INV` / `CN` (and `QT` / `SAAS`)

`DocumentSeries` (`apps/lazuar-api/Modules/Billing/Contracts/DocumentSeries.cs:9-46`):

```
RCPT → RCPT-{yyyy}
QT   → QT-{yyyy}
INV  → INV-{yyyy}
CN   → CN-{yyyy}
```

`Prefix` bakes the UTC year (`:16-17`). `CustomerFacingNumber` never returns a raw GUID; it falls back to non-GUID `TaxInvoiceId`, else `"PENDING"` (`:37-46`).

`GenerateNextSequenceNumberCommandHandler` (`apps/lazuar-api/Modules/Billing/Infrastructure/Commands/GenerateNextSequenceNumberCommandHandler.cs:21-42`) upserts `billing.DocumentSequences` on `(OrganizationId, Prefix)` and returns `{Prefix}-{value:D5}`. Example: prefix `RCPT-2026` → `RCPT-2026-00001`. The SQL is a single-statement `INSERT … ON CONFLICT DO UPDATE … RETURNING`. That is safe under concurrency for the **sequence table**.

It is **not** in the same transaction as the ledger insert. The handler opens its own Dapper connection (`:23-24`). If the subsequent `SaveChanges` on the ledger fails, the sequence has already moved. The comment at `:26-27` (“prevents sequence gaps during rollbacks”) is the opposite of what the code does. Gaps are acceptable for commercial numbers; claiming gap-free is not.

Who allocates what, after Waves 0–4 (W2-LP-101):

| Event | Series | Assign method | Consolidation |
|-------|--------|---------------|---------------|
| B2C gateway payment | `RCPT-yyyy-#####` | `AssignB2cReceipt` (`GatewayPaymentCompletedHandler.cs:91-93`) | `PENDING` + `B2C_RECEIPT` (`LedgerEntry.cs:67-78`). Amounts over `Lhdn:B2cIndividualThresholdMyr` (default 10000) become `NOT_REQUIRED` + `NEEDS_BUYER_TIN` (`GatewayPaymentCompletedHandler.cs:94-98`). |
| B2B gateway payment | `INV-yyyy-#####` | `AssignB2bInvoice` (`:102-104`) | `NOT_REQUIRED` |
| Manual enrollment B2C / B2B | same `RCPT` / `INV` | `ManualSubscriberEnrolledIntegrationEventHandler.cs:57-68` | B2B not required; B2C pending via `AssignB2cReceipt` |
| Gateway refund | `CN-yyyy-#####` | `AssignCustomerDocumentNumber` (`GatewayRefundCompletedHandler.cs:73-76`) | **not set** — stays `null` (see §2.6) |
| Custom checkout quote | `QT-yyyy-#####` | Commerce checkout session (W2-LP-101-done) | n/a |
| Hub SaaS fee | `SAAS-yyyy-#####` on **system** org id | `AssignPlatformDocumentNumber` (`PlatformSaasFeeHandler.cs:87-102`) | `NOT_REQUIRED`. Does **not** start B2C consolidation (`LedgerEntry.cs:81-92`). |

LHDN UUID is never a series value. That is the whole point of W2-LP-101 and of `CustomerDocumentNumber`.

### 2.5 Sale journal (the happy path this refund path mirrors)

`GatewayPaymentCompletedHandler` (`apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/GatewayPaymentCompletedHandler.cs:38-138`):

1. Skip if `metadata.type` is platform-collected (`:43-45`). Utility and Hub fee must not dual-post as creator GMV.  
2. Idempotent on `GATEWAY_PAYMENT` + gateway tx id (`:47-51`).  
3. Lines (`:72-84`):

   - `ASSET_CASH` = `NetAmount` (amount − fee, from the payment event).  
   - `EXPENSE_GATEWAY_FEE` = `GatewayFee` if &gt; 0.  
   - `REVENUE_GROSS` = `−(AmountPaid − tax)`.  
   - `LIABILITY_TAX_PAYABLE` = `−tax` if tax &gt; 0.

4. Tax comes from `event.TaxAmount` or metadata `sst_tax_amount` (`:159-175`).  
5. Then receipt/invoice number + PDF (`GenerateAndStoreDocumentCommand`). B2B also publishes `B2bTaxInvoiceRequestedIntegrationEvent` (`:128-136`).

`ValidateBalanced` holds when `NetAmount + GatewayFee == AmountPaid` and tax is a slice of `AmountPaid`. If a webhook ever sends `NetAmount + GatewayFee != AmountPaid`, the handler throws and the inbox retries into dead-letter. That is correct. Billplz often has fee 0 by construction; the journal then books the full paid amount as cash. That is honest about **our** numbers and dishonest about the bank payout (see §10).

### 2.6 Refund journal and the consolidation leak

`GatewayRefundCompletedHandler` (`apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/GatewayRefundCompletedHandler.cs:32-79`):

1. `RefundedAmount <= 0` → return (`:34-35`).  
2. Reference id is **per attempt**: `PaymentRecordId:N + ":" + event.Id:N` (`:37-39`). Same event redelivery is one row (`GatewayRefundCompletedHandlerTests.SecondEvent_IsIdempotent`). Two attempts with two event ids are two rows (`TwoAttempts_TwoLedgerRows`).  
3. Tax: prefer `event.TaxAmount`; else look up original `GATEWAY_PAYMENT` by **gateway transaction id** and scale (`:86-125`). Full refund (`RefundedAmount >= originalPaid`) reverses full tax. Partial: `Round(RefundedAmount / originalPaid * originalTax, 4, AwayFromZero)`.  
4. Lines (`:52-69`):

   - `ASSET_CASH` = `−(RefundedAmount − RefundedFee)`.  
   - `EXPENSE_GATEWAY_FEE` = `−RefundedFee` only if fee &gt; 0.  
   - `CONTRA_REVENUE_REFUNDS` = `RefundedAmount − taxRefund`.  
   - `LIABILITY_TAX_PAYABLE` = `+taxRefund` if &gt; 0.

5. Allocate `CN-yyyy-#####` and `AssignCustomerDocumentNumber`.  
6. **Does not** call `MarkConsolidationNotRequired`. Customer type defaults to `"B2C"` (`LedgerEntry` constructor `:46`). Consolidation status stays `null`. LHDN status stays `null`.

`B2cConsolidationJob.ProcessPeriodAsync` (`B2cConsolidationJob.cs:151-161`) selects B2C rows where `ConsolidationStatus == PENDING` **or** (`ConsolidationStatus == null` and (`LhdnValidationStatus == B2C_RECEIPT` **or** `LhdnValidationStatus == null`)). A `GATEWAY_REFUND` with both statuses null **matches**.

Same-month this is almost a feature: the job nets `REVENUE_GROSS` minus `CONTRA_REVENUE_REFUNDS` (`:269-274`). A refund in the sale month reduces the consolidated total.

Cross-month it is not a feature: the sale month already filed the full receipt. The refund month computes `grossRevenue = 0 − contra` which is **negative**, fails `if (grossRevenue > 0)` (`:280`), and if no positive groups remain, every row in that month’s batch is `MarkConsolidationIgnored` (`:300-306`). There is no type-02 CN from consolidation. B2C refunds after month-end do not legally reverse the filed batch.

W2-LP-101 allocated CN numbers. It did not teach the refund writer to mark consolidation, and it did not teach the consolidation job to exclude `GATEWAY_REFUND` headers. The net-in-month path works by accident of the SQL filter being too wide.

### 2.7 Other writers (needed to judge “the ledger”)

**Utility top-up** — `PlatformTopUpEventHandler.cs:28-83`. Only `type == utility_credit_topup`. Idempotent on `SYSTEM_CREDIT_TOPUP` + gateway tx. Highest package with `AmountMyr <= AmountPaid` (`:47-51`). Wallet `TopUp` + journal `EXPENSE_SOFTWARE_SUBSCRIPTION` / `ASSET_CASH −`. Tenant is paying Lazuar; cash goes **out** of the tenant’s Hub books. Overpay grants the next-lower pack only. Under-pack amounts grant 0 credits **and write no ledger row** (`if (credits > 0)` at `:53`). A RM 49 payment with packs at 50/100/200 is a silent no-op.

**Hub SaaS fee** — `PlatformSaasFeeHandler.cs:41-122`. Only `platform_saas_fee`. Rejects system org and empty tenant (`:47-51`). Amount **must** equal `Saas:Plan:AmountMyr` and currency (`:64-72`); mismatch logs and returns without activating. Books `SYSTEM_SAAS_FEE` as expense/cash on the **paying** tenant, allocates `SAAS-yyyy` on **system** org sequence (`:87-89`), generates a Lazuar PDF, **never** publishes `InvoiceIssuedIntegrationEvent` (`:116-121`).

**Utility chargeback reverse** — `ChargebackClawbackHandler.ReverseUtilityTopUpLedgerAsync` (`:110-166`). Copies every original top-up line with negated amounts. `SYSTEM_CREDIT_CHARGEBACK` + gateway tx. Does not allocate a CN (correct: this is not a customer document).

**LHDN cancel reverse** — `LhdnDocumentCancelledIntegrationEventHandler.cs:27-68`. Finds the original entry by customer number / `TaxInvoiceId` / `ReferenceId` (`LedgerLhdnLookup.cs:12-22`). Posts `LHDN_CANCELLATION` that **mirrors every original line** with opposite signs. Then `UpdateLhdnStatus(…, CANCELLED)` on the original. This is the double-reverse bomb when the original was already contra’d by `GATEWAY_REFUND` (§5.4, §13 P0-2).

**Invoice issued** — `InvoiceIssuedHandler.cs:19-43` books AR vs deferred revenue. Grep of `new InvoiceIssuedIntegrationEvent` in production code: **zero hits**. Only tests construct it. Lhdn’s `InvoiceIssuedIntegrationEventHandler` is a log-only ignore (`InvoiceIssuedIntegrationEventHandler.cs:22-26`: “MyInvois submit uses B2bSaleReadyForEinvoice only”). Dead consumer pair.

**Zero-amount checkout** — `ZeroAmountCheckoutHandler.cs:33-37`. If `OriginalAmount > 0`, `EXPENSE_DISCOUNT` + `REVENUE_GROSS −`. Balanced only when `DiscountAmount == OriginalAmount` (100% coupon). `MarkConsolidationNotRequired`.

**Manual enrollment** — cash + gross, no tax, no fee (`ManualSubscriberEnrolledIntegrationEventHandler.cs:51-52`). Offline money is booked as if it hit the bank at 100%.

**Commission** — expense vs affiliate payable (`CommissionAccruedHandler.cs:33-34`). `GetFinancialSummaryAsync` **does not** subtract this. `GetNetProfitAsync` does (`BillingQueryService.cs:199`). Dashboard uses summary, not net-profit.

**Revenue recognition** — `RevenueRecognitionJob` exists and would write `REVENUE_RECOGNIZED` lines. It is **unregistered** (`Billing DependencyInjection.cs:76-80`). `InvoiceIssuedHandler` comment (`:33-35`) says schedule writers are parked. `GetFinancialSummaryAsync`’s `recognized_revenue` and `deferred_revenue` are therefore almost always 0 unless someone inserted schedules by hand.

**Dead twin of top-up** — `ApiCreditPurchasedHandler.cs` still implements `IIntegrationEventHandler<ApiCreditPurchasedIntegrationEvent>` and would book another `SYSTEM_CREDIT_TOPUP` on the same gateway tx. It is **not** registered in `AddBillingModule` / `UseBillingSubscriptions`. Unique index would have saved us if it were. Leave it as residue, not a live path.

### 2.8 Query surfaces for the journal

`GET /admin/billing/ledger` (`AdminLedgerEndpoints.cs:20-34`) → `GetLedgerEntriesAsync`. `type_filter=sales` excludes `GATEWAY_REFUND` and `LHDN_CANCELLATION` only (`BillingQueryService.cs:56-63`). `type_filter=reversals` is those two types. `SYSTEM_CREDIT_CHARGEBACK` is **not** a “reversal” in this UI. Credit Notes page (`CreditNotesPage.tsx:34-41`) is this reversals filter. It is routed and in the ops sidebar (`Sidebar.tsx:28`, `:259-262`) after Wave 2 — ADR 023 hid LHDN-heavy product, not this commercial list.

`GET /admin/billing/summary` → `GetFinancialSummaryAsync` (`BillingQueryService.cs:126-178`). Signed sums, display polarity documented in comments `:131-136`. Formula for `Net_revenue`:

```
−SUM(REVENUE_GROSS)
− SUM(CONTRA_REVENUE_REFUNDS)
− SUM(EXPENSE_DISCOUNT)
− SUM(EXPENSE_GATEWAY_FEE)
− (−SUM(LIABILITY_TAX_PAYABLE))
```

That is **P&amp;L net after fees, discounts, refunds, and tax**, not `SUM(ASSET_CASH)`. It ignores `EXPENSE_SOFTWARE_SUBSCRIPTION` (Hub fee + credit packs), `EXPENSE_COMMISSION`, `ASSET_CASH`, AR, and deferred. Currency is hardcoded `'MYR'` (`:151`). Dates work if the caller passes them. The dashboard does not.

---

## 3. `RecordRefund`: full, partial, mark-refunded vs API refund

### 3.1 Command and HTTP

`RecordRefundCommand` (`apps/lazuar-api/Modules/Commerce/Contracts/Commands/RecordRefundCommand.cs:11-19`):

```
OrganizationId, TransactionLogId,
Amount?, GatewayName?, SubscriptionId?,
TaxAmount = 0, MarkRefunded = false, Reason?
```

Returns `string`: `"refund_requested"` or `"refunded"`.

`POST /admin/commerce/transactions/{id}/refund` (`TransactionEndpoints.cs:69-116`) binds TypeSpec `RecordRefundRequestDto` (`packages/api-spec/modules/commerce/models/subscriber.tsp:11-18`: `amount?`, `gateway_name?`, `subscription_id?`, `tax_amount?`, `mark_refunded?`, `reason?`). Maps `Mark_refunded == true` and `Tax_amount` default 0. OrgMember. Failures are RFC 7807 `ProblemDetails` with `title = code`.

Ops `RefundModal.tsx:61-69` sends `amount`, `reason`, `gateway_name` (only if missing on the row), `mark_refunded: true` for non-API rails, `subscription_id`. **It never sends `tax_amount`.** Billing therefore always takes the proportional-from-original-payment path unless some other client sets the field.

Copy in the modal is honest about rails (`RefundModal.tsx:37-54`): API rails say “sends money back at the processor”; Billplz says “no bill-refund API… mark it here”; offline says “mark only after you returned the money.” Footer: “Refund does not cancel” the subscription (`:171-172`).

### 3.2 Capability matrix

`PaymentGatewayCapabilities` (`apps/lazuar-api/Modules/Payments/Contracts/PaymentGatewayCapabilities.cs:18-55`):

| Method | True for |
|--------|----------|
| `SupportsApiRefund` | `STRIPE`, `CHIP`, `RAZORPAY`, `XENDIT` |
| `RequiresMarkRefunded` | blank, `BILLPLZ`, `OFFLINE`, `BANK_TRANSFER`, `CASH`, `MANUAL_OFFLINE`, `COMPED` |

Unknown names that are not in the mark-refunded list fall through to `GATEWAY_REFUND_UNSUPPORTED` (`RecordRefundCommandHandler.cs:108-111`). There is no silent Stripe default. Missing gateway on both request and log is `GATEWAY_REQUIRED` (`:162-164`). W1-LP-091 closed the old “default STRIPE, Billplz always fails” hole.

Adapters:

- Stripe `IssueRefundAsync` (`StripeGatewayAdapter.cs:277-296`): PaymentIntent refund in minor units, idempotency key `lazuar-refund:{pi}:{minor}` (`:331-337`). Treats Stripe status `succeeded` **or `pending`** as success (`:290`). A pending Stripe refund immediately publishes `GatewayRefundCompleted`. If Stripe later fails the pending refund, we have already booked Commerce + Billing as refunded. No `charge.refund.updated` consumer exists to unwind.  
- CHIP posts `/purchases/{id}/refund/`.  
- Razorpay `Payment.Fetch().Refund(...)`.  
- Xendit `POST /refunds`.  
- Billplz **always `false`**. Comment on the adapter: Payment Order is a new disbursement, not a reversal. Commerce must mark-refunded.

### 3.3 Status machine on `CommerceTransactionLog`

`apps/lazuar-api/Modules/Commerce/Domain/Entities/CommerceTransactionLog.cs:8-138`:

| Status | Meaning |
|--------|---------|
| `CONFIRMED` | money in, refundable |
| `REFUND_PENDING` | API refund requested, apply-lock |
| `PARTIALLY_REFUNDED` | `0 < RefundedAmount < Amount`, refundable again |
| `REFUNDED` | `RefundedAmount >= Amount` |
| `REFUND_FAILED` | adapter/config failed; retryable |
| `DISPUTED` | chargeback row stamped this log |

`RemainingAmount` (`:33`) is `Amount − RefundedAmount` floored at 0. `ApplyRefund` (`:108-122`) accumulates and caps at `Amount`. `MarkRefundPending` (`:93-96`) blindly sets pending (no guard that source was refundable — the command handler does that). `MarkRefundFailed` (`:98-106`) only from pending. `MarkDisputed` (`:135-138`) overwrites whatever was there, including `REFUNDED`.

Refundable sources in the command (`RecordRefundCommandHandler.cs:145-148`): `CONFIRMED`, `PARTIALLY_REFUNDED`, `REFUND_FAILED`. **Not** `DISPUTED`, `REFUND_PENDING`, `REFUNDED`.

### 3.4 Handler algorithm (the live money loop)

`RecordRefundCommandHandler.Handle` (`apps/lazuar-api/Modules/Commerce/Application/Commands/RecordRefundCommandHandler.cs:30-126`):

1. Load log; org mismatch → `InvalidOperationException` “Transaction log not found” (IDOR test: `RecordRefund_ForeignOrg_ThrowsNotFound`).  
2. Already `REFUNDED` or `RemainingAmount <= 0` → `ALREADY_REFUNDED`.  
3. Already `REFUND_PENDING` → `REFUND_PENDING`.  
4. Status not in the refundable set → `REFUND_NOT_ALLOWED`.  
5. Empty `ExternalReference` → `NO_GATEWAY_REFERENCE`.  
6. Amount = request amount or remaining. `<= 0` → `INVALID_AMOUNT`. `> remaining` → `AMOUNT_EXCEEDS_REMAINING`.  
7. Resolve gateway; persist name onto the log if the request overrode it. Persist reason (trim 255).  
8. `isFullRefund = amount == remaining` (remaining **this** call, so a second slice that finishes the balance is full).  
9. **Mark-refunded rail** (`RequiresMarkRefunded`):  
   - without `MarkRefunded` → `MARK_REFUNDED_REQUIRED`.  
   - with it: `ApplyRefund(amount)` **now**, publish `GatewayRefundCompleted` (fee 0, net = amount, tax from request, `IsFullRefund`), `SaveChanges`, audit `refund.created` status `refunded`, return `"refunded"`. **No adapter.**  
10. **API rail** (`SupportsApiRefund`): `MarkRefundPending()`, publish `GatewayRefundRequested` with amount/currency/gateway/tax/`IsFullRefund`, `SaveChanges`, audit status `refund_requested`, return `"refund_requested"`.  
11. Else `GATEWAY_REFUND_UNSUPPORTED`.

Publish happens **before** `SaveChanges` on `OutboxEventBus<CommerceDbContext>` (W1-LP-091). The outbox row and the status change commit together. That is the persist-before-adapter property: Payments sees `GatewayRefundRequested` only after Commerce has the pending lock.

Tests that lock this (`RecordRefundCommandHandlerTests.cs`): full request persists pending + outbox `GatewayRefundRequested` with `IsFullRefund=true` and the real amount; no Stripe default; CHIP uses log gateway; already refunded / pending / Billplz-without-mark / offline-without-mark; mark-refunded Billplz publishes `GatewayRefundCompleted` not requested; reason persisted; partial 40 on 100 is pending with `IsFullRefund=false`; omit after 40 uses remaining 60 as full; over remaining throws; second slice from `PARTIALLY_REFUNDED` allowed; `REFUNDED` still rejected; Billplz mark of 20 stays `PARTIALLY_REFUNDED`.

### 3.5 What “full” and “partial” mean in three modules

| Module | Full | Partial |
|--------|------|---------|
| Commerce command | `amount == remaining` at request time | `0 < amount < remaining` |
| Commerce log after apply | `RefundedAmount >= Amount` → `REFUNDED` | `PARTIALLY_REFUNDED` |
| Billing ledger | one `GATEWAY_REFUND` per attempt; tax full if `RefundedAmount >= originalPaid` | tax scaled; second attempt is a second row |
| LHDN | `IsFullRefund == true` → cancel or CN | **skip entirely** (`GatewayRefundCompletedIntegrationEventHandler.cs:49-55`) |

A 100% refund of a **previously partial** payment is `IsFullRefund=true` because remaining hit 0. LHDN will try to cancel/CN the **whole** original invoice, not the last slice. There is no partial MyInvois document. That is a product limitation, not a silent bug, but it is wrong if the first slice already should have produced a partial CN (it did not; LHDN skipped).

### 3.6 What is still not a refund loop

- Inbound processor refunds (dashboard / customer-portal / Radar). `ProcessGatewayWebhookCommandHandler` only accepts `PAYMENT_COMPLETED`, `DISPUTE_CREATED`, `PAYMENT_FAILED` (`ProcessGatewayWebhookCommandHandler.cs:83-88`). A Stripe `charge.refunded` / `refund.updated` is parsed by the adapter as a generic type and **dropped**. Domain state does not move unless someone hits `RecordRefund` or mark-refunded. W1-LP-092-done says this remains `LP-PAY-022`. After Wave 4 it is still true.  
- `RefundedFee` is hard-coded `0m` in both mark-refunded (`RecordRefundCommandHandler.cs:99`) and the Payments adapter success path (`GatewayRefundRequestedIntegrationEventHandler.cs:51-52`: “adapters currently do not return reclaimed fee”). Billing therefore never reverses `EXPENSE_GATEWAY_FEE`. `LedgerBalanceMatrixTests.PaymentThenFullRefund_NetsRevenueToZeroGrossMinusFees` **asserts** net = `−3` after a full refund of a 108/3-fee sale. That is the specified behaviour: fees stay as expense. Stripe often does not return the fee on refund; Billplz fee was 0 to begin with.  
- Refund does not cancel or pause the Commerce subscription. Modal says so. Chargeback also does not (§5).  
- `DISPUTED` is not refundable. If a dispute lands first, ops cannot mark-refund or API-refund that row. If a refund lands first, a later dispute still publishes a **second** `GatewayRefundCompleted` (§5.3).

---

## 4. `GatewayRefundCompleted` / `GatewayRefundFailed` consumers

### 4.1 The events

`GatewayRefundCompletedIntegrationEvent` (`apps/lazuar-api/Modules/Payments/Contracts/Events/GatewayRefundCompletedIntegrationEvent.cs:6-20`): `OrganizationId`, `SubscriptionId`, `PaymentRecordId`, `GatewayTransactionId`, `RefundedAmount`, `Currency`, `RefundedFee`, `NetRefundedAmount`, `TaxAmount = 0`, `IsFullRefund = false`. New `Id` is a version-7 GUID unless a publisher overwrites it.

`GatewayRefundFailedIntegrationEvent` (`GatewayRefundFailedIntegrationEvent.cs:6-14`): org, subscription, `PaymentRecordId`, `ErrorMessage`. No amount. No gateway id.

Pre-wave gap docs (`docs/001-gaps/06-payments-module.md`, `15-event-driven-architecture.md`) said Failed had **zero** subscribers and Completed was starved because Requested was never published. After W1-LP-091 both statements are false.

### 4.2 Who publishes Completed

| Publisher | When | `PaymentRecordId` | `event.Id` | `IsFullRefund` | `RefundedFee` |
|-----------|------|-------------------|------------|----------------|---------------|
| `RecordRefundCommandHandler` mark-refunded | Billplz/offline mark | transaction log id | new v7 | remaining==0 | 0 |
| `GatewayRefundRequestedIntegrationEventHandler` | adapter `true` | log id (from request) | new v7 | copied from request | 0 |
| `CommerceGatewayDisputeCreatedHandler` | GMV dispute, `AmountDisputed > 0` | **dispute id** | **forced to dispute id** | **default false** | 0 |

Three publishers. The third is the problem in §5.

### 4.3 Who publishes Failed

Only `GatewayRefundRequestedIntegrationEventHandler` (`:34-36`, `:41-43`, `:70-71`):

- config missing / no API key → Failed “Payment configuration not found or inactive.”  
- `Amount <= 0` → Failed “Refund amount must be greater than zero.”  
- `IssueRefundAsync` false → Failed “Gateway adapter failed to issue refund.”

Mark-refunded never fails at the adapter (there is none). Dispute never publishes Failed.

### 4.4 Consumers of Completed (three modules)

**Commerce** — `GatewayRefundCompletedIntegrationEventHandler.cs:19-43`.

- Match log by org + (`ExternalReference == GatewayTransactionId` OR `ExternalReference == PaymentRecordId.ToString()` OR `Id == PaymentRecordId`). The ExternalReference match is the real one (comment `:21`). PaymentRecordId match is what the API path uses (command puts `log.Id` on the event).  
- **Apply lock:** only if status is `REFUND_PENDING` (`:35-39`). Mark-refunded already applied; redelivery is a no-op. Dispute-originated Completed hits a `DISPUTED` (or `CONFIRMED`/`REFUNDED`) row and **no-ops**. That is why Wave 3 could reuse Completed for ledger without double-applying the Commerce log — Commerce ignores it.  
- Then `ApplyRefund(RefundedAmount)` + save.

Tests (`GatewayRefundCompletedIntegrationEventHandlerTests.cs`): full pending → `REFUNDED`; slice → `PARTIALLY_REFUNDED`; second slice from pending after a prior apply → `REFUNDED`; not-pending does not double-add.

**Billing** — `GatewayRefundCompletedHandler` (§2.6). No status lock. Any Completed with `RefundedAmount > 0` that has a fresh `(PaymentRecordId, event.Id)` posts a `GATEWAY_REFUND` and a `CN-` number. Dispute-originated events do this. That is the design W3-LP-094-done named: “Ledger contra is the existing Billing `GatewayRefundCompleted` consumer (event id = dispute id).”

**Lhdn** — `Modules/Lhdn/Infrastructure/EventHandlers/GatewayRefundCompletedIntegrationEventHandler.cs:47-139`.

- Partial (`!IsFullRefund`) → log and return. Dispute events default `IsFullRefund=false`, so **LHDN never sees chargebacks**.  
- Resolve original `TaxDocument` via Billing `FindPaymentByGatewayTransaction` then customer number / UUID / `TaxInvoiceId` / `PaymentRecordId.ToString()` (`:142-169`).  
- Only if `ValidationStatus == "VALID"` and UUID present. B2C receipts (`B2C_RECEIPT`) never cancel or CN.  
- ≤72 hours since `ValidatedAt` → `CancelTaxDocumentCommand` (`:68-74`). That command talks to MyInvois and, on success, publishes `LhdnDocumentCancelledIntegrationEvent` (`CancelTaxDocumentCommand.cs:61`), which Billing reverses **again** (§5.4).  
- &gt;72 hours → type `02` credit note via `SubmitTaxDocumentCommand` (`:103-139`), **not** a stub TIN: `LhdnBuyerMapper.TryCreatePayloadBuyer` must produce a real TIN or the handler returns (`:88-101`). Internal id is the Billing CN number if the refund ledger already exists (`:172-184`), else a **second** `GenerateNextSequenceNumberCommand` — two CN numbers if Billing has not committed yet (inbox order is not a guarantee). Idempotency key `cn:{org:N}:{PaymentRecordId:N}:{event.Id:N}` (`:138`).  
- CN line: `Unit_price = RefundedAmount`, `Tax_amount = event.TaxAmount` (usually 0 from our publishers), `Total_excluding_tax = RefundedAmount`, `Total_including_tax = RefundedAmount + TaxAmount` (`:126-135`). If `RefundedAmount` is the **gross paid** (it is: Commerce sends the cash amount, not net-of-tax), adding tax **again** overstates `Total_including_tax`. Classification `022`, tax type `06`.  
- `SubmitTaxDocumentCommand` is the live credit meter (§8). A ≥72h B2B full refund **charges LHDN credits** for the CN.

Tests (`Lhdn/GatewayRefundCompletedIntegrationEventHandlerTests.cs`): partial does not touch tax docs; missing doc no-op; full &lt;72h sends cancel; full &gt;72h submits type 02 with CRM TIN, not `IG1234567890`.

### 4.5 Consumer of Failed (one)

**Commerce only** — `GatewayRefundFailedIntegrationEventHandler.cs:19-37`. Match by org + `Id == PaymentRecordId`. Only if `REFUND_PENDING` → `MarkRefundFailed()`. Billing does nothing. Lhdn does nothing. Ops sees `REFUND_FAILED` on the next poll (Transactions page refetches every 2s while any row is pending, `TransactionsPage.tsx:47-50`). Retry is another `RecordRefund` from `REFUND_FAILED` (allowed source).

Pre-wave “ops blind, still PAID” is closed **for the API path**. It is still true for inbound dashboard refunds (no event) and for disputes (no Failed, and Completed is the wrong event).

### 4.6 Subscriptions

```
Payments  UsePaymentsSubscriptions  → GatewayRefundRequested → GatewayRefundRequestedIntegrationEventHandler
Commerce  UseCommerceSubscriptions  → GatewayRefundCompleted / Failed / GatewayDisputeCreated
Billing   UseBillingSubscriptions   → GatewayRefundCompleted / GatewayDisputeCreated
Lhdn      UseLhdnSubscriptions      → GatewayRefundCompleted
```

(`Payments/Infrastructure/DependencyInjection.cs:55-66`, `Commerce/Infrastructure/DependencyInjection.cs:62-87`, `Billing/Infrastructure/DependencyInjection.cs:62-104`, `Lhdn/Infrastructure/DependencyInjection.cs:63-100`.)

Cross-module delivery is outbox → global bus → per-module inbox. A Commerce-published Completed is seen by Billing and Lhdn. That is why the dispute handler’s publish is not a local no-op.

---

## 5. `CommerceGatewayDisputeCreatedHandler` — does it wrongly publish `GatewayRefundCompleted`?

**Yes.** After Wave 3 this is intentional in the ticket note and still the wrong event.

### 5.1 What the handler does

`apps/lazuar-api/Modules/Commerce/Infrastructure/EventHandlers/CommerceGatewayDisputeCreatedHandler.cs:33-121`.

1. If `metadata.type` is platform-collected (utility or Hub SaaS) → **return**. Billing owns those (`:35-40`). Tests: `UtilityType_NoOps`, `PlatformSaasFee_NoOps`.  
2. Existing `commerce.Disputes` row for `(OrganizationId, GatewayTransactionId)` → return (`:42-51`). Unique index `IX_Disputes_OrganizationId_GatewayTransactionId` (`20260820140000_AddCommerceDisputes.cs:40-45`). Replay is one OPEN row. Test: `Replay_SameGatewayTransactionId_PersistsOneRow` also asserts the bus received Completed **once**.  
3. Resolve `subscription_id` / `checkout_session_id` / `session_id` / `checkout_id` from metadata. If the Guid is not a subscription in this org, try checkout session and null the subscription (`:55-83`).  
4. Insert `CommerceDispute` (`StatusOpen` only — there is no WON/LOST/CLOSED, `CommerceDispute.cs:8-41`).  
5. If a transaction log has `ExternalReference == GatewayTransactionId`, `MarkDisputed()` (`:94-99`).  
6. **If `AmountDisputed > 0`, publish `GatewayRefundCompleted`** (`:101-114`) with:

   - `PaymentRecordId: dispute.Id`  
   - `GatewayTransactionId: event.GatewayTransactionId`  
   - `RefundedAmount = NetRefundedAmount = AmountDisputed`  
   - `RefundedFee = 0`  
   - `TaxAmount` default 0  
   - `IsFullRefund` default **false**  
   - `Id = dispute.Id` (so Billing’s attempt key is `disputeId:disputeId`)

7. Save. Log warning. **Do not cancel the subscription.** Test `Subscription_IsNotCanceled` asserts `ACTIVE` + log `DISPUTED` + dispute `OPEN`.

Ops `DisputesPage.tsx` lists `GET /admin/commerce/disputes` (`Commerce Endpoints.cs:67-77` → `GetDisputesAsync`). Description: “Open card chargebacks on Commerce payments. Access stays active until you cancel.” Empty copy says “No open disputes” even though every persisted row is `OPEN` forever — there is no closed state to filter.

Ingress: Stripe `charge.dispute.created` (`StripeGatewayAdapter.cs:193-229`) → `DISPUTE_CREATED` with amount = `dispute.Amount/100`, currency from Stripe (often `"myr"`), `GatewayTransactionId = PaymentIntentId`, metadata copied from the PaymentIntent (this is how `type` / `subscription_id` / `tenant_id` arrive). `ProcessGatewayWebhookCommandHandler.PublishParsedEventAsync` (`:168-178`) publishes `GatewayDisputeCreatedIntegrationEvent` using `AmountPaid` as `AmountDisputed`. CHIP / Billplz / Razorpay / Xendit adapters do **not** emit `DISPUTE_CREATED`. FPX chargebacks will not appear.

### 5.2 Why Wave 3 did this

`plans/007-feats/impl/W3-LP-094-analysis.md:7-8` invariant: a Stripe dispute on GMV “reverses the matching sales ledger (idempotent)” and flags the subscription, without auto-cancel.

`W3-LP-094-done.md:3`: “Ledger contra is the existing Billing `GatewayRefundCompleted` consumer (event id = dispute id).”

They reused Completed so they would not write a second Billing handler. The reuse buys: one tax-scale path, one CN number, one contra account, inbox idempotency on `dispute.Id`.

### 5.3 Why it is still the wrong event

A dispute is not a refund.

| Fact | Refund Completed (API / mark) | Dispute-as-Completed |
|------|-------------------------------|----------------------|
| Money movement | Processor (or human) returned funds | Funds are **held / claimed**, outcome unknown |
| Commerce log | `REFUND_PENDING` → `REFUNDED` / `PARTIALLY_REFUNDED` | `DISPUTED`; Completed consumer **does not** apply |
| `IsFullRefund` | computed from remaining | always false → LHDN skip |
| `PaymentRecordId` | transaction log id | dispute id |
| Unwind | none (no inbound refund.failed) | none (`dispute.closed` / won / lost not parsed) |
| Second event on same payment | blocked by remaining / already-refunded | blocked by unique dispute key **only for a second dispute**. A **refund then a dispute** is two Completed events with two ids. |

**Double-book path (P0):**

1. Ops marks or API-refunds a Stripe PaymentIntent. Billing posts `GATEWAY_REFUND` keyed `logId:refundEventId`. Commerce is `REFUNDED`.  
2. Customer also opens a Stripe dispute (or Stripe emits `charge.dispute.created` because the refund raced the dispute).  
3. Commerce handler finds no dispute row, inserts one, `MarkDisputed()` on the already-refunded log, publishes Completed with `Id = dispute.Id` and `RefundedAmount = AmountDisputed`.  
4. Billing `HasEntryBeenProcessed` is a **different** reference id. Second `GATEWAY_REFUND` + second `CN-`. Cash and contra and (scaled) tax reverse **twice**.  
5. LHDN still skips (not full). MyInvois is the only book that is not double-hit.

There is no test for refund-then-dispute. `Replay_SameGatewayTransactionId` only covers the same dispute event twice.

**Won-dispute path (missing):** if the merchant wins, nothing republishes a reversing journal. The first `GATEWAY_REFUND` from the dispute stays. Access stayed `ACTIVE` the whole time, so the only thing that was “wrong” on win is the books — which is the thing this product claims to be good at.

**Lost-dispute path:** the premature contra happens to match cash-out, but the document is a CN for a “refund”, not a chargeback, and LHDN was never told.

**Partial dispute:** `AmountDisputed` can be a slice. Billing scales tax if it finds the original payment. Commerce log becomes `DISPUTED` even if only part of the charge is disputed. Remaining is not tracked on the dispute row. A later refund is `REFUND_NOT_ALLOWED` because status is `DISPUTED`.

Comment on the handler (`:13-16`) says “Persists Commerce GMV disputes… Does not cancel the subscription.” It does not mention that it emits a refund-completed event. ChargebackClawback’s class comment (`ChargebackClawbackHandler.cs:18-25`) still says “Scope (A.6 / C.1 MVP): utility clawback only” and “Does NOT … reverse merchant GMV ledger entries.” That comment is half-true: *this* handler does not reverse GMV; Commerce’s handler does, via the refund event. README Billing §5 (`README.md:37`) still says dispute consume is “utility chargeback” only. Three comments, one lie by omission.

### 5.4 Compounding P0: full B2B refund &lt;72h already double-reverses without a dispute

Even if we ignore disputes, the **legitimate** refund path double-books when LHDN cancel runs.

Sequence:

1. B2B sale. `GATEWAY_PAYMENT` + `INV-2026-#####`. MyInvois `VALID`.  
2. Full refund. Payments adapter true → Completed `IsFullRefund=true`.  
3. Billing posts `GATEWAY_REFUND`: `ASSET_CASH −`, `CONTRA +`, `TAX +`.  
4. Lhdn ≤72h → `CancelTaxDocumentCommand` succeeds → `LhdnDocumentCancelledIntegrationEvent`.  
5. Billing `LhdnDocumentCancelledIntegrationEventHandler` finds the **original payment** by INV number and posts `LHDN_CANCELLATION` that negates **every payment line**: `ASSET_CASH −` (again), `REVENUE_GROSS +` (unwinds sale), `TAX +` (again).

Net after 1+3+5 on a 108 / 8 tax / 3 fee sale (fee not reversed on refund):

| Account | Payment | Refund | LHDN cancel | Net |
|---------|---------|--------|-------------|-----|
| `ASSET_CASH` | +105 | −108 | −105 | **−108** |
| `EXPENSE_GATEWAY_FEE` | +3 | 0 | −3 | 0 |
| `REVENUE_GROSS` | −100 | 0 | +100 | 0 |
| `CONTRA_REVENUE_REFUNDS` | 0 | +100 | 0 | +100 |
| `LIABILITY_TAX_PAYABLE` | −8 | +8 | +8 | **+8** |

Cash looks like we paid the customer twice. Tax payable flips sign (we appear to have a tax **asset**). Gross is zero but contra still sits there, so `GetFinancialSummaryAsync` net = `0 − 100 − 0 − 0 − (−8 wait: tax display is −SUM so leftover +8 becomes −8)` — the summary becomes garbage.

≥72h uses Submit CN instead of cancel, so this particular double reverse does **not** fire. The 72h window is the dangerous one, and it is the legally preferred IRBM path.

There is no matrix test that runs payment → refund → LHDN cancel.

### 5.5 Direct answer

`CommerceGatewayDisputeCreatedHandler` **does** publish `GatewayRefundCompleted`. Wave 3 named that as the GMV ledger contra. It is the wrong semantic event: it skips LHDN, it does not lock Commerce apply (so Commerce and Billing diverge: `DISPUTED` vs a posted refund), it cannot unwind a win, and it double-posts if a real refund already landed. The publish should have been a `GatewayDisputeCreated` consume inside Billing (mirror of `ChargebackClawbackHandler` for GMV) or a dedicated `GATEWAY_DISPUTE` reference type. Reusing Completed was cheaper than correct.

---

## 6. `ChargebackClawbackHandler` — utility + Hub SaaS only

`apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/ChargebackClawbackHandler.cs`.

Subscribed to `GatewayDisputeCreatedIntegrationEvent` (`Billing DependencyInjection.cs:104`). Same event Commerce consumes. The two handlers **partition on `metadata.type`**.

### 6.1 Branch table

| `metadata.type` | Behaviour |
|-----------------|-----------|
| missing | return (`:47-48`) |
| `platform_saas_fee` | `MarkSaasPastDueAsync` then return (`:50-54`). **No credit clawback. No SaaS ledger reverse.** |
| `utility_credit_topup` | package clawback + `SYSTEM_CREDIT_CHARGEBACK` reverse (`:57-82`) |
| anything else (`commerce_subscription`, GMV, blank-but-present, …) | return (`:57-58`). Test `NonUtility_IsNoOp`. |

Class comment (`:18-25`) is stale: it still says “utility clawback only” and never mentions SaaS `PAST_DUE`. The SaaS branch was added in W1-LP-004 (`W1-LP-004-done.md:25`).

### 6.2 Utility clawback

Needs `tenant_id` Guid (`:60-61`). Credits = highest pack with `AmountMyr <= AmountDisputed` (`:64-68`) — **same function as grant**, not “credits actually granted on that tx.” If packs are 50/500, 100/1100, 200/2500 and the dispute amount is 200, clawback is 2500 even if the original payment was a 50-pack (the grant side uses `AmountPaid`; the claw side uses `AmountDisputed`). A partial dispute of RM 40 on a RM 50 pack grants 0 clawback (`FirstOrDefault() ?? 0`).

`ClawbackCreditsCommand` → `wallet.Clawback` (`TenantCreditBalance.cs:70-78`): **clamps at zero**, does not throw. Tenant who already spent the credits goes to 0; we do not invoice them for the hole.

Ledger reverse requires the original `SYSTEM_CREDIT_TOPUP` for that gateway tx (`:123-137`). Missing original → warning, skip journal, credits may still have been clawed. Idempotent on `SYSTEM_CREDIT_CHARGEBACK` + gateway tx (`:116-121`).

### 6.3 Hub SaaS dispute

`MarkSaasPastDueAsync` (`:85-108`): find `WorkspaceSaasSubscriptions` by `tenant_id`, `MarkPastDue()`, save. No row → warning, credits unchanged. Test `PlatformSaasFeeDispute_MarksPastDue_DoesNotClawCredits` also asserts the starter wallet stays 50 and `ClawbackCreditsCommand` is not sent.

What it does **not** do:

- Reverse `SYSTEM_SAAS_FEE` (expense/cash stays). The tenant’s books still say they paid Lazuar for the period.  
- Shorten `CurrentPeriodEnd`. Access gating of Hub itself is not in this file; status is just `PAST_DUE`.  
- Emit Completed (Commerce already returned on platform type, so GMV refund-as-dispute does not fire).  
- Cancel.  

So: utility dispute tries to take the credits back and unwind the top-up journal. SaaS dispute only paints the subscription past-due. GMV dispute is not this handler’s job (and is done wrongly next door).

### 6.4 Scope sentence the user asked for

**ChargebackClawbackHandler is utility + Hub SaaS only.** Utility: claw credits + reverse `SYSTEM_CREDIT_TOPUP`. Hub SaaS: `PAST_DUE`, credits untouched, fee journal untouched. Merchant GMV: no-op here.

---

## 7. `Saas:Plan:AmountMyr = 0`

Repo config (`apps/lazuar-api/src/Lazuar.Api/appsettings.json:86-93`):

```json
"Saas": {
  "Plan": {
    "Code": "hub_starter",
    "Name": "Hub Starter",
    "AmountMyr": 0,
    "Interval": "mo",
    "Currency": "MYR"
  }
}
```

`SaasOptions.Plan.AmountMyr` defaults to 0 if unbound (`SaasOptions.cs:14`).

### 7.1 Checkout is forced off

`CreateSaasCheckoutCommandHandler.cs:47-49`:

```
if (plan.AmountMyr <= 0)
    throw new InvalidOperationException("Hub plan price is not configured.");
```

`POST /admin/billing/saas/checkout` maps that to HTTP 400 (`AdminSaasEndpoints.cs:27-41`). Ops Plan & billing disables Pay when `amount_myr` is not &gt; 0 (`BillingSettingsPage.tsx:56`, `:130`) and shows “Price is not configured yet.” (`:114-116`).

W1-LP-004-done is explicit: tracker stays **P** — the charge path is real; a live listed price is not. After Waves 0–4 that is still the repo state. An operator must set a positive MYR amount in config (or env overlay) before anyone can pay.

### 7.2 Pay path when an operator *does* set a price

1. Handler upserts `WorkspaceSaasSubscription` as `UNPAID` if missing (`CreateSaasCheckoutCommandHandler.cs:57-64`). Does not reset an `ACTIVE` row.  
2. Metadata `type=platform_saas_fee`, `tenant_id=paying org`, `plan_code`.  
3. `GenerateSystemCheckoutSessionQuery` on **system** keys, amount = `plan.AmountMyr` only (not a GMV %).  
4. Webhook → `PlatformSaasFeeHandler`. Amount must **equal** config (`:64-72`). A stale checkout for RM 49 after config moved to RM 99 will not activate.  
5. `ActivateFromPayment` (`WorkspaceSaasSubscription.cs:53-67`): period starts at `max(now, CurrentPeriodEnd)` so early renewal does not discard days. Interval `mo`/`yr` only (`SaasPlanInterval.cs:10-17`).  
6. Journal + `SAAS-yyyy` PDF. No MyInvois. No credits. No `InvoiceIssued`.

`GET /admin/billing/saas` (`GetWorkspaceSaasQueryHandler.cs:23-41`) returns `UNPAID` if no row, plus the **current** config plan (so the UI price is always whatever is in config now, not what was paid).

### 7.3 Public pricing honesty

`GetPublicPricingQueryHandler.cs:50-61`:

- `Gmv_take_percent` hardcoded `0`.  
- `Checkout_is_free = planAmount <= 0` — **true** in repo.  
- `Lhdn_credits_live = false` **hardcoded**, even though `SubmitTaxDocumentCommand` deducts when not test mode and cost &gt; 0.  
- `Whatsapp_credits_live = false` — matches console.  
- `Lhdn_submit_credits = 3`, `Whatsapp_send_credits = 0` from config.

W1-LP-006-done: page says checkout software is free today when `AmountMyr=0`. That sentence is true. The LHDN “not live” flag is a product-hide, not a meter-off.

### 7.4 Seller SST

`Saas:Seller.SstRate` is 0, reason “Supplier not SST-registered” (`appsettings.json:94-101`). Platform invoice factory prints that footnote. No SST is added at Hub checkout.

---

## 8. Credits: only live LHDN deducts; WhatsApp console must not

### 8.1 Config and cost table

`appsettings.json:65-85`:

```
Credits.Costs.WhatsAppSend = 0
Credits.Costs.LhdnSubmit   = 3
Packages: 50→500, 100→1100, 200→2500
StarterGrant: 50
```

No `EmailSend` / `BroadcastEmailPerRecipient` keys.

`CreditAction` enum (`ICreditCostService.cs:7-13`): `EmailSend`, `WhatsAppSend`, `LhdnSubmit`, `BroadcastEmailPerRecipient`.

`CreditCostService.GetCost` (`CreditCostService.cs:47`): missing / unparsed / unknown enum → **0**, not 1. W1-LP-005 closed the old “invent a cost of 1” bug. Tests lock appsettings `WhatsAppSend==0` and omitted-key → 0 (`CreditCostServiceTests`).

### 8.2 Wallet

`TenantCreditBalance` (`:43-78`):

- `TopUp` requires `credits > 0`.  
- `Deduct` requires `credits > 0` and sufficient balance; else `402` business rule.  
- `Clawback` clamps.  
- `xmin` xid concurrency token (`BillingDbContext.cs:100-105`).  
- Unique wallet per org (`:98`).  
- `CreditDeductionIdempotencyLogs` unique `(OrganizationId, IdempotencyKey)` (`:129-133`).

`DeductTenantCreditCommandHandler` (`:24-78`): re-check idempotency every attempt; deduct; insert log; retry `DbUpdateConcurrencyException` up to 3; unique-violation on the log is treated as success (peer won the race). `Deduct(0)` never reaches here if callers honour the domain (`Deduct` throws on `<= 0`). LHDN and WhatsApp both skip send when cost is 0.

Starter grant: `StarterCreditSeederHandler` on `AppEntitlementGranted` for `AppId == "BILLING"` (`:28-43`). Skips if wallet exists. Grant `<= 0` skips. One-time.

### 8.3 The only live deduct: LHDN submit

`SubmitTaxDocumentCommandHandler.Handle` (`apps/lazuar-api/Modules/Lhdn/Application/Commands/SubmitTaxDocumentCommand.cs:71-172`):

- `lhdnCost = GetCost(LhdnSubmit)` (3).  
- `shouldMeter = !isTestMode && lhdnCost > 0` (`:74-75`).  
- If metering: `HasSufficientCreditsAsync` or throw `402: Insufficient API Credits (3 required)` (`:77-83`).  
- Persist tax document + LHDN idempotency log first.  
- Then `DeductTenantCreditCommand` with key `lhdn:{request.IdempotencyKey}` or `lhdn:{taxDocument.Id}` (`:152-164`). Deduction failure is **logged**, not thrown — the document is already saved (`:166-169`). A wallet bug can give a free submit.  
- Test mode: no sufficiency check, no deduct (`LhdnSingleCreditPathTests`).  
- Cost 0: no `Deduct(0)` (`LhdnSingleCreditPathTests`).

`LhdnDocumentSubmittedIntegrationEventHandler` (`:7-32`) is **observability only**. Comment: a prior hard-coded deduct of 1 caused double charging. It must not deduct. It does not.

So the live meter is **one function**: accept of `SubmitTaxDocumentCommand` in live mode. That includes:

- Merchant (or future UI) type-01 submits.  
- B2C monthly consolidation: `ConsolidatedInvoiceIssuedIntegrationEventHandler.cs:56-60` builds a type-01 “General Public” document and `Send`s `SubmitTaxDocumentCommand`. One live consolidation batch therefore deducts 3 credits per org per closed month (new v7 idempotency key each fire — a **retry of the event** is a new key and can double-charge unless Lhdn’s own document idempotency stops the second persist; the credit key will not match).  
- Refund ≥72h type-02 CN (`GatewayRefundCompletedIntegrationEventHandler.cs:139`) — **yes, this deducts 3 credits** for a credit note the merchant did not click.

Public pricing still says `Lhdn_credits_live = false`. Ops Plan & billing copy is more honest (`BillingSettingsPage.tsx:148-149`): “Credits are deducted only when a live LHDN e-invoice submit is accepted… WhatsApp is not connected and is not billed.”

### 8.4 WhatsApp console must not deduct — and does not

`ConsoleMessagingService` (`:9-24`): `IsBillable => false`. Logs `[Local Dispatch] [MESSAGING/SMS]`. This is the registered transport.

`DispatchMessageIntegrationEventHandler` (`:60-177`):

1. `Messaging:WhatsAppEnabled` defaults **false** (`appsettings.json:103-105`). Flag off → skip WhatsApp, log `SKIPPED` “WhatsApp channel disabled”, `wantsWhatsApp = false` (`:69-76`).  
2. `whatsappCost = GetCost(WhatsAppSend)` then **forced to 0** if `_messagingService is ConsoleMessagingService || !IsBillable` (`:85-88`). Even if an operator sets `WhatsAppSend: 2` and `WhatsAppEnabled: true`, console transport does not invent a meter.  
3. Deduct only if `actualCost > 0 && !isSystemTenant && !billedViaHold` (`:163-177`). `actualCost` is incremented only after a successful send (`:153`).  
4. Email never calls `GetCost(EmailSend)` (tests assert this).  
5. Insufficient credits (only reachable with a **billable** transport and cost &gt; 0) skips WhatsApp and throws if email also did not send (`:179-185`).

W1-LP-005-done tests: flag on + **real** `ConsoleMessagingService` + cost 2 → **no** `DeductTenantCreditCommand`. That is the lock the user asked for.

There is no Meta Cloud adapter in-tree as a billable `IMessagingService`. Wave 4 LP-074/155 left WhatsApp as a stub. `Whatsapp_credits_live = false` is true.

### 8.5 What is not a credit path

- Email / Resend: tenant’s own key, no deduct.  
- Broadcast: enum exists, cost 0, no deduct site found in this pass that would fire for console.  
- Hub SaaS pay: 0 credits (explicit tests).  
- Guest GMV: 0 credits.  
- `ApiCreditPurchasedHandler`: unregistered.

---

## 9. CSV reconciliation export

After W1-LP-097 the export **exists**. The 007 tracker cell that still says `N` is stale.

### 9.1 What it is

`GET /admin/commerce/transactions/export` (`TransactionEndpoints.cs:22-48`):

- Query: `from`, `to`, `status`.  
- Default window: last **31 days** ending `now` if omitted (`:30-31`). Swaps if from &gt; to.  
- `ExportTransactionsAsync` (`CommerceQueryService.Transactions.cs:98-145`): org + created-at range + optional status, `ORDER BY CreatedAt DESC`, `LIMIT HardCap+1`.  
- `HardCap = 50_000` (`TransactionExportCsv.cs:11`). Overflow sets `X-Export-Truncated: true` and truncates.  
- Body: UTF-8 **BOM** (`:54-57`).  

Locked columns (`TransactionExportCsv.cs:12-13, 29-49`):

```
id,created_at,status,amount,fee_amount,net_amount,currency,
customer_name,customer_email,product_name,recorded_by,external_reference
```

Ops Transactions **CSV** button (`TransactionsPage.tsx:122-148`) uses the last 31 days, passes the status filter, not the gateway filter. Filename `transactions_{from}_{to}.csv`. Page description (`:76`): “Audit Hub-recorded money rows. Match external_reference to Billplz bill id / Stripe PaymentIntent / CHIP id. Fees are Hub-recorded, not the bank payout file.”

Subscriber CSV is a different export (`SubscriberEndpoints.cs`) and is unchanged (W1-LP-097-done: “Subscriber export unchanged. Ledger flatten deferred”).

### 9.2 What it is not

It is **not** a ledger dump. There is no `GET /admin/billing/ledger/export`. Billing has no CSV at all (grep of Billing for `csv` / `export` is empty).

Missing from the Commerce file, for anyone actually reconciling:

- `refunded_amount`, `remaining_amount`  
- `gateway_name`  
- `refund_reason`  
- `subscription_id`  
- document numbers (`RCPT`/`INV`/`CN`)  
- fee honesty (Billplz fee is often 0 here and non-zero on the Billplz payout CSV)  
- disputes (status `DISPUTED` is exportable if you filter it; the default 31-day dump includes it as just another status string)

W1-LP-097-done claimed tracker **N → Y** “on commerce file alone.” That is the honest cell: a transaction-log extract, not Stripe Sigma / a payout recon file.

Tests: `TransactionExportCsvTests` (header, quoting `ada,pay@example.com`, ISO timestamps).

---

## 10. Honesty of dashboard cash vs MRR

Two different APIs sit on one “Sales Insights” page (`apps/lazuar-ops/src/modules/commerce/pages/DashboardPage.tsx`).

### 10.1 The cards

```76:83:apps/lazuar-ops/src/modules/commerce/pages/DashboardPage.tsx
    { label: "Net Cash in Bank", value: formatMYR(financials?.net_revenue || 0), icon: DollarSign },
    { label: "MRR", value: formatMYR(stats?.mrr || 0), icon: DollarSign, tip: "Committed monthly equivalent of active memberships. Not cash. Past-due is excluded." },
    { label: "ARR", value: formatMYR(stats?.arr ?? ((stats?.mrr || 0) * 12)), icon: DollarSign, tip: "Committed monthly equivalent of active memberships. Not cash. Past-due is excluded." },
    { label: "Active Subscribers", value: stats?.active_subscribers || 0, icon: Users },
    { label: "Past Due", value: stats?.past_due_subscribers || 0, icon: AlertTriangle, alert: (stats?.past_due_subscribers || 0) > 0 },
    { label: "Cancellation Rate", value: `${stats?.churn_rate_percentage || 0}%`, icon: Activity },
    { label: "Recovered (lifetime)", value: formatMYR(stats?.recovered_revenue || 0), icon: RotateCcw },
```

Glossary under the grid (`:190-196`): “MRR / ARR is the committed monthly equivalent of active memberships. Not cash. Past-due is excluded. Recovered is campaign-lifetime cash collected while PAST_DUE or SUSPENDED, not this month.”

**MRR labelling is honest. “Net Cash in Bank” is not.** There is no tooltip on that card.

### 10.2 What MRR actually is

`CommerceMrr.MonthlyEquivalent` (`apps/lazuar-api/Modules/Commerce/Application/CommerceMrr.cs:11-38`):

- Status must be `ACTIVE` (ordinal ignore-case). `PAST_DUE`, `TRIALING`, `SUSPENDED`, `CANCELED` → 0. Tests lock this.  
- Collection-paused (`CollectionPausedUntil > utcNow`) → 0.  
- Interval only `mo` or `yr`. Yearly ÷ 12.  
- Line = `(unitAmount > 0 ? unitAmount : fallbackUnit) * max(1, quantity)`.  
- Comment: “Committed monthly equivalent on ACTIVE rows using the subscription snapshot (LP-161). Not cash.”

`GetStatsAsync` (`CommerceQueryService.Stats.cs:46-54`) sums that over all non-`PENDING` subscriptions. W3-LP-161-done: catalog edits do not move MRR until a successful period payment refreshes `UnitAmount`.

Tracker title in 007 said “Honest MRR / ARR **(ledger-based)**.” The implementation is **not** ledger-based. Billing `REVENUE_RECOGNIZED` is parked. MRR is a Commerce snapshot. That is the right grain for SaaS committed revenue and the wrong grain if someone believed W3 delivered ledger MRR.

**ARPU hole:** `activeSubs` includes `PAST_DUE` (`Stats.cs:46`) and is the denominator for ARPU (`:61`: `mrr / activeSubs.Count`). Numerator excludes past-due. ARPU is understated when anyone is past-due. The “Active Subscribers” card is also not “active for MRR” — it includes past-due, who have their own card.

ARR is `mrr * 12` (`:121`). Not a separate contract.

### 10.3 What “Net Cash in Bank” actually is

`GET /admin/billing/summary` with **no dates** (all-time). Value = `FinancialSummaryDto.net_revenue` = the P&amp;L formula in §2.8.

It is **not**:

- `SUM(ASSET_CASH)`.  
- A bank balance.  
- A payout file.  
- This month.  
- Net of Hub SaaS / credit-pack cash the tenant paid Lazuar (those are `EXPENSE_SOFTWARE_SUBSCRIPTION` and are excluded — `LedgerBalanceMatrixTests.TopUp_PostsBalancedExpense_AndDoesNotAffectMerchantNetRevenue` **asserts** top-up net stays 0).  
- Net of affiliate commission (summary ignores it; `/net-profit` does not).  
- Safe after the refund+LHDN-cancel double reverse (§5.4).

Root README still sells it (`README.md:61-62`): “giving founders a true ‘Net Cash in Bank’ metric.” The agent tool repeats the lie in the type name (`GetFinancialHealthAgentQuery.cs:10-19`: `NetCashInBank` is filled from `summary.Net_revenue`, `:35-36`). LLM prompt rule 6 (`LlmOrchestratorService.Prompts.cs`) tells the model to distinguish Gross vs Net Cash vs tax — and then the tool it is given **names net revenue “Net Cash in Bank.”**

After a clean sale of 108 / 8 tax / 3 fee, summary net = 100 − 3 − 8 = 89. `ASSET_CASH` is 105. The card shows 89. Tax is subtracted (money we still have, but owe). That is a reasonable **take-home estimate** if fees are real. It is not cash in the bank. Billplz fee is often 0 in our journal and non-zero at Billplz — then the card is closer to gross−tax than to the bank.

After a full refund of that sale (no LHDN cancel), matrix says net = −3 (fee remains). The card shows −RM 3.00 “in the bank.”

### 10.4 Revenue trend / “Total” is a third number

`total_revenue_collected` and `cash_flow_trend` (`Stats.cs:63-84`) sum `TransactionLogs` where status is **exactly** `CONFIRMED`.

Consequences:

- A `PARTIALLY_REFUNDED` RM 100 of which RM 20 came back contributes **0** to all-time collected and to the month bar. Not 80.  
- `REFUNDED` contributes 0 (correct-ish) but also vanishes from the month it was originally confirmed (the bar is by `CreatedAt` of the log, filtered by **current** status — a refund this month deletes last month’s bar).  
- `DISPUTED` contributes 0.  
- `REFUND_PENDING` contributes 0 while the adapter runs.  
- Fees are ignored (gross of the log, not net).  
- Chart title in the UI is “Revenue Trend” with “Total {total_revenue_collected}” (`DashboardPage.tsx:201-203`).

So the page shows three incompatible money numbers:

1. Ledger P&amp;L net, labelled cash.  
2. Committed ACTIVE snapshot, labelled MRR (honest).  
3. Sum of logs that still happen to say `CONFIRMED`, labelled revenue trend.

Billing README’s golden rule is broken twice (MRR not from ledger; trend not from ledger) and the one number that *is* from the ledger is misnamed.

### 10.5 Recovered

`Recovered_revenue` is `SUM(DunningCampaigns.RecoveredRevenue)` lifetime (`Stats.cs:108-116`). Glossary is honest. It is not this month and not in the trend.

---

## 11. What Waves 0–4 actually closed on this slice

Checked against code, not tracker cells.

| Wave / ID | Claim | Code now |
|-----------|--------|----------|
| W1-LP-091 | Full refund loop, no Stripe default, Failed consumer, mark-refunded | **True.** §3–4. |
| W1-LP-092 | Partial remaining-amount machine, per-attempt Billing row, LHDN only if full | **True.** |
| W1-LP-093 | Ops modal honest about rails | **True** (`RefundModal.tsx`). |
| W1-LP-097 | Commerce CSV | **True.** Ledger flatten not done. |
| W1-LP-004 | Hub SaaS plane, `AmountMyr=0` in repo | **True.** Checkout 400 until priced. |
| W1-LP-005 | Only live LHDN deducts; console WhatsApp must not | **True.** §8. |
| W1-LP-006 | Public page: checkout free when amount 0; GMV 0% | **True.** `Lhdn_credits_live=false` is a hide, not a meter-off. |
| W2-LP-101 | `RCPT`/`INV`/`CN`/`QT`; UUID not printed | **True** on writers. Refund rows not marked `NOT_REQUIRED` for consolidation. |
| W3-LP-094 | GMV disputes first-class | **Partial.** Row + UI + no auto-cancel are real. Ledger contra via Completed is the defect this report exists to name. |
| W3-LP-161 | Honest MRR | **True as committed ACTIVE snapshot.** Not ledger-based. Dashboard glossary is honest. |
| W0-LP-090 | Inbound webhook verify + business-key idempotency | **True for payment/fail/dispute.** Refund events still not in the allow-list. |
| Parked Phase 17 | Revenue recognition | **Still parked.** |

Pre-wave gap docs that are now **wrong** if cited as current truth:

- “`GatewayRefundFailed` has no consumers.”  
- “Refund amounts always 0.”  
- “Requested is never published.”  
- “No CSV.”  
- “ChargebackClawback is the only dispute consumer.”  
- “WhatsApp deducts 1.”  

Do not copy those sentences forward.

---

## 12. P0 / P1

### P0 — money can be wrong in the books

**P0-1. GMV dispute publishes `GatewayRefundCompleted`.**  
`CommerceGatewayDisputeCreatedHandler.cs:101-114`. Treats a chargeback as a completed refund in Billing. LHDN skipped (`IsFullRefund` default false). Commerce log `DISPUTED` vs ledger `GATEWAY_REFUND`+`CN`. No won/lost unwind. **Refund then dispute double-posts** two `GATEWAY_REFUND` rows (different event ids). Wave 3 documented the reuse; it is still wrong.

**P0-2. Full B2B refund ≤72h double-reverses.**  
Billing `GATEWAY_REFUND` already contra’d cash/tax/revenue. Lhdn cancel then `LHDN_CANCELLATION` mirrors the **original payment** (`LhdnDocumentCancelledIntegrationEventHandler.cs:41-62` + `CancelTaxDocumentCommand.cs:61`). Cash and tax payable go through the looking-glass. No test covers payment+refund+cancel.

**P0-3. Inbound refund webhooks are dropped.**  
`ProcessGatewayWebhookCommandHandler.cs:83-88`. Stripe Dashboard / customer-portal / pending-then-failed refunds never move Commerce or Billing unless someone clicks our modal. Stripe `IssueRefundAsync` treats `pending` as success (`StripeGatewayAdapter.cs:290`) with no later reconcile.

### P1 — honesty, completeness, or next-wrong-number

**P1-1. Dashboard “Net Cash in Bank” is ledger net revenue, all-time, tax-out, Hub-fee-ignored.**  
`DashboardPage.tsx:76` + `BillingQueryService.cs:137-148`. README and the agent tool repeat the name. Relabel or show `SUM(ASSET_CASH)` **and** say it is still not the bank.

**P1-2. Revenue trend / `total_revenue_collected` counts only `CONFIRMED`.**  
`CommerceQueryService.Stats.cs:70-72`. Partial refunds, pending refunds, and disputes fall out of history. Not reconcilable to the ledger card on the same page.

**P1-3. ARPU and “Active Subscribers” include `PAST_DUE`; MRR does not.**  
`Stats.cs:46-61` vs `CommerceMrr.cs:20-23`. Glossary is right about MRR and silent about the active count.

**P1-4. `RefundedFee` always 0.**  
API and mark paths. Fees remain expense after a full refund (matrix asserts −3). Fine if labelled; not fine if we sell “exact gateway fees.”

**P1-5. Refund `GATEWAY_REFUND` rows are B2C with null consolidation and get pulled into `B2cConsolidationJob`.**  
Same-month they net contra (lucky). Later-month they do not produce a negative consolidation; the filed B2C batch stays high. Writer should `MarkConsolidationNotRequired` (or a new `REVERSED`).

**P1-6. LHDN type-02 CN amounts.**  
`Total_including_tax = RefundedAmount + TaxAmount` (`Lhdn GatewayRefundCompletedIntegrationEventHandler.cs:133-135`) while `RefundedAmount` is already the cash/gross figure. Ops never sends `tax_amount`. CN ≥72h also meters 3 LHDN credits.

**P1-7. Sequence allocation is not in the ledger transaction.**  
`GenerateNextSequenceNumberCommandHandler` uses its own connection. Comment about gap-free rollbacks is false.

**P1-8. Unique ledger key is global `(ReferenceType, ReferenceId)`.**  
No org. Fine for Stripe PI ids; wrong grain.

**P1-9. `Saas:Plan:AmountMyr = 0`.**  
Not a bug — a dark switch. Do not sell a Hub subscription. Checkout 400. Public page “free today” is correct.

**P1-10. Public `Lhdn_credits_live = false` while live submit deducts.**  
Hide vs meter. Plan & billing copy is the one to keep.

**P1-11. CSV is not a recon file.**  
No refunded amount, no gateway, no ledger lines, 31-day default, 50k cap. Good as a log extract.

**P1-12. Disputes have no closed/won/lost, no CHIP/Billplz ingress, no outbound webhook, no access flag.**  
OPEN forever. GMV chargeback does not `PAST_DUE` the Commerce subscription (only Hub SaaS does, in Billing).

**P1-13. Dead / parked residue that can confuse the next editor.**  
`ApiCreditPurchasedHandler` unregistered. `InvoiceIssued` never published. `RevenueRecognitionJob` unregistered. Chargeback and Billing README comments still say “utility only.”

**P1-14. Utility clawback uses dispute amount vs pack table, not credits actually granted.**  
Wrong pack / partial dispute → 0 or the wrong integer.

---

## 13. Verdict

After Waves 0–4 the **operator-initiated refund loop is real**. A merchant can refund Stripe/CHIP/Razorpay/Xendit in-product (pending → adapter → Completed/Failed) or mark Billplz/offline refunded. Amounts are the remaining machine. Billing posts a balanced `GATEWAY_REFUND` with a `CN-yyyy-#####` and reverses tax. WhatsApp console cannot steal credits. LHDN live submit is the only meter. Hub SaaS is a separate plane that does not take GMV and does not mint credits, and in this repo it cannot be purchased because `AmountMyr` is 0. MRR on the dashboard is finally a sentence a CFO would accept (“committed, not cash, past-due out”).

The ledger is **not** audit-grade.

It balances **per entry**. It does not stay true **across** the two events we already emit on a single economic fact (refund + LHDN cancel; refund + dispute). It names P&amp;L net “cash in bank.” It books a chargeback as a refund and then refuses to file the tax document for that chargeback. It never hears a refund that did not start in our modal. Recognition is parked. The CSV cannot be tied to a payout. `AmountMyr = 0` means we do not even have a live Hub invoice in default config.

Sell: “we record your gateway payments and you can refund them from the console; receipts have numbers; credits are for MyInvois if you turn it on.”

Do not sell: “absolute financial truth,” “Net Cash in Bank,” “chargebacks handled,” “CFO OS,” a priced Hub subscription, or WhatsApp as a metered product.

**Next money work (not this file):** stop publishing `GatewayRefundCompleted` from the dispute handler (P0-1); make LHDN cancel not mirror a payment that already has a `GATEWAY_REFUND` (P0-2); allow-list inbound refund events or stop treating Stripe `pending` as terminal (P0-3); rename the cash card (P1-1). Until those four move, the Billing module is a careful journal with two known ways to lie.
