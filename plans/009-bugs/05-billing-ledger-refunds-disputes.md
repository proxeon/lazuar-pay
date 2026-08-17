# 05 — Billing money: double-entry ledger, booking, refunds, disputes, Hub SaaS fee, credits, series, tax

**Date:** 17 August 2026  
**Branch:** `feat/007-waves-1-4-implement`  
**Assigned SHA:** `297ba98`. Working-tree HEAD when read: `30d07d2` (docs-only after `297ba98`; Billing money files unchanged). Dispute fix `e18edbe` is an ancestor.  
**Agent:** bug-audit only. No fixes. No commit. Out of scope: Commerce dunning chrome (03), adapter HTTP (04), LHDN XML submit (06).

This report is uncondensed. Evidence for `plans/009-bugs/README.md` row 05. Not a rewrite of `plans/008-evals/03-ledger-refunds-disputes-credits.md`.

---

## 1. Files table (everything this slice actually is)

| Path | Role now |
|------|----------|
| `apps/lazuar-api/Modules/Billing/README.md` | Module contract. Golden rule: never compute cash / tax / MRR from Commerce logs. Dispute consume listed as utility chargeback only. |
| `apps/lazuar-api/Modules/Billing/Domain/Aggregates/LedgerEntry.cs` | Journal header. `ValidateBalanced` on **base** sums only. Document assign `??=`. `TaxInvoiceId` still dual-use. |
| `apps/lazuar-api/Modules/Billing/Domain/Entities/LedgerLine.cs` | Immutable child. **Not** `IMustHaveTenant`. No `OrganizationId` column. |
| `apps/lazuar-api/Modules/Billing/Domain/AccountTypes.cs` | Chart + consolidation + LHDN statuses + `LedgerReferenceTypes`. No `GATEWAY_DISPUTE`. |
| `apps/lazuar-api/Modules/Billing/Domain/Aggregates/TenantCreditBalance.cs` | Wallet. `Deduct` throws 402. `Clawback` clamps at 0. |
| `apps/lazuar-api/Modules/Billing/Domain/Aggregates/CreditHold.cs` | Reserve → consume → release. Status comment lists `RELEASED`; code only writes `HELD` / `SETTLED`. |
| `apps/lazuar-api/Modules/Billing/Domain/Aggregates/WorkspaceSaasSubscription.cs` | Plane S. `UNPAID` / `ACTIVE` / `PAST_DUE` / `CANCELED`. |
| `apps/lazuar-api/Modules/Billing/Domain/Aggregates/DeferredRevenueSchedule.cs` | Parked amortization entity. |
| `apps/lazuar-api/Modules/Billing/Domain/Aggregates/TenantBillingProfile.cs` | Merchant legal + SST id. |
| `apps/lazuar-api/Modules/Billing/Domain/Entities/DocumentSequence.cs` | `(OrganizationId, Prefix)` sequence row. |
| `apps/lazuar-api/Modules/Billing/Domain/Entities/CreditLedger.cs` | Wallet movement. No org column. |
| `apps/lazuar-api/Modules/Billing/Domain/SaasPlanInterval.cs` | `mo` / `yr` only. |
| `apps/lazuar-api/Modules/Billing/Contracts/DocumentSeries.cs` | `RCPT` / `QT` / `INV` / `CN` + UTC year prefix. |
| `apps/lazuar-api/Modules/Billing/Contracts/Commands/GenerateNextSequenceNumberCommand.cs` | Allocates `{Prefix}-{value:D5}`. |
| `apps/lazuar-api/Modules/Billing/Contracts/Commands/DeductTenantCreditCommand.cs` | Live meter entry. |
| `apps/lazuar-api/Modules/Billing/Contracts/Commands/CreditHoldCommands.cs` | Reserve / consume / release. |
| `apps/lazuar-api/Modules/Billing/Contracts/Commands/CreateSaasCheckoutCommand.cs` | Hub checkout. |
| `apps/lazuar-api/Modules/Billing/Contracts/Events/B2bTaxInvoiceRequestedIntegrationEvent.cs` | Paid B2B → MyInvois type 01. |
| `apps/lazuar-api/Modules/Billing/Contracts/Events/InvoiceIssuedIntegrationEvent.cs` | Dead. Never published in production. |
| `apps/lazuar-api/Modules/Billing/Contracts/Events/ManualPaymentRecordedIntegrationEvent.cs` | Dead. No consumer. |
| `apps/lazuar-api/Modules/Billing/Application/ILedgerRepository.cs` | Idempotency is `(referenceType, referenceId)` **without org**. |
| `apps/lazuar-api/Modules/Billing/Application/Queries/Agent/GetFinancialHealthAgentQuery.cs` | Names `Net_revenue` “Net Cash in Bank”. |
| `apps/lazuar-api/Modules/Billing/Infrastructure/BillingDbContext.cs` | Schema `billing`. Unique `(ReferenceType, ReferenceId)` **global**. Lines forced Added-on-Modified. |
| `apps/lazuar-api/Modules/Billing/Infrastructure/Repositories/LedgerRepository.cs` | `HasEntryBeenProcessedAsync` = `IgnoreQueryFilters` + type/id, **no** `OrganizationId`. |
| `apps/lazuar-api/Modules/Billing/Infrastructure/DependencyInjection.cs` | Subscriptions. `RevenueRecognitionJob` parked. `ApiCreditPurchasedHandler` **not** registered. |
| `apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/GatewayPaymentCompletedHandler.cs` | GMV sale journal. Skips platform-collected. Does **not** skip `$0`. |
| `apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/GatewayRefundCompletedHandler.cs` | Per-attempt `GATEWAY_REFUND` + `CN-`. No FX. No remaining cap. No consolidation mark. |
| `apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/ChargebackClawbackHandler.cs` | Utility claw + SaaS `PAST_DUE`. GMV no-op. Claw **before** reverse idempotency. |
| `apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/PlatformSaasFeeHandler.cs` | Plane S. Exact `Saas:Plan:AmountMyr`. Expense / cash out. |
| `apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/PlatformTopUpEventHandler.cs` | Plane U. Highest pack `<= AmountPaid`. Under-pack is a silent no-op. |
| `apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/LhdnDocumentCancelledIntegrationEventHandler.cs` | Mirrors **every** original line. Does not look for an existing `GATEWAY_REFUND`. |
| `apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/LhdnDocumentValidatedIntegrationEventHandler.cs` | Stamps UUID. Regenerates PDF except `B2C-CONS-`. |
| `apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/LhdnDocumentSubmittedIntegrationEventHandler.cs` | Log only. Must not deduct. Does not. |
| `apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/ZeroAmountCheckoutHandler.cs` | Discount vs original. Unbalanced when they disagree. **No Billing test.** |
| `apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/ManualSubscriberEnrolledIntegrationEventHandler.cs` | Cash = revenue. No tax. No fee. |
| `apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/InvoiceIssuedHandler.cs` | AR vs deferred. Event never published. |
| `apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/CommissionAccruedHandler.cs` | Expense vs affiliate payable. Summary ignores it. |
| `apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/StarterCreditSeederHandler.cs` | One-time grant on `AppId == BILLING`. |
| `apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/ApiCreditPurchasedHandler.cs` | Dead twin of top-up. Unregistered. |
| `apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/LedgerLhdnLookup.cs` | Match by customer number / `TaxInvoiceId` / `ReferenceId`. `FirstOrDefault` has no type preference. |
| `apps/lazuar-api/Modules/Billing/Infrastructure/Commands/GenerateNextSequenceNumberCommandHandler.cs` | Own Dapper connection. Comment claims gap-free rollbacks. False. |
| `apps/lazuar-api/Modules/Billing/Infrastructure/Commands/DeductTenantCreditCommandHandler.cs` | xmin retry + unique-key success. |
| `apps/lazuar-api/Modules/Billing/Infrastructure/Commands/ClawbackCreditsCommandHandler.cs` | **No** idempotency key. |
| `apps/lazuar-api/Modules/Billing/Infrastructure/Commands/CreditHoldCommandHandlers.cs` | Reserve deducts immediately. Consume does not touch wallet. |
| `apps/lazuar-api/Modules/Billing/Infrastructure/Commands/CreateSaasCheckoutCommandHandler.cs` | `AmountMyr <= 0` throws. |
| `apps/lazuar-api/Modules/Billing/Infrastructure/Commands/GenerateAndStoreDocumentCommandHandler.cs` | PDF from `REVENUE_GROSS` / `REVENUE_RECOGNIZED` only. Contra-only refunds render empty. |
| `apps/lazuar-api/Modules/Billing/Infrastructure/Commands/GenerateAndStorePlatformSaasInvoiceCommandHandler.cs` | Hub PDF. Never `InvoiceIssued`. |
| `apps/lazuar-api/Modules/Billing/Infrastructure/Services/BillingQueryService.cs` | Org-scoped Dapper. Summary polarity. Lines fetched by entry id (no org on `LedgerLines`). |
| `apps/lazuar-api/Modules/Billing/Infrastructure/Services/CreditCostService.cs` | Missing key → **0**, not 1. |
| `apps/lazuar-api/Modules/Billing/Infrastructure/Services/SaasOptions.cs` | `AmountMyr` defaults 0. |
| `apps/lazuar-api/Modules/Billing/Infrastructure/Workers/B2cConsolidationJob.cs` | Pending **or** (null + receipt/null). `alreadyConsolidated` **without** `IgnoreQueryFilters`. |
| `apps/lazuar-api/Modules/Billing/Infrastructure/Workers/RevenueRecognitionJob.cs` | Unregistered. If hosted, worker filter would hide all schedules. |
| `apps/lazuar-api/Modules/Billing/Infrastructure/Endpoints/AdminLedgerEndpoints.cs` | `/ledger`, `/summary`, `/net-profit`, document presign. |
| `apps/lazuar-api/Modules/Billing/Infrastructure/Endpoints/AdminCreditsEndpoints.cs` | Wallet + top-up checkout. Min RM 50. |
| `apps/lazuar-api/src/Lazuar.Api/appsettings.json` | `Saas:Plan:AmountMyr = 0`. Credits 50/100/200 → 500/1100/2500. `LhdnSubmit = 3`. |
| `apps/lazuar-api/BuildingBlocks/Infrastructure/PlatformDbContext.cs` | Fail-closed `OrganizationId == TenantId`. Empty tenant matches **no** rows. |
| `apps/lazuar-api/Modules/Commerce/Infrastructure/EventHandlers/CommerceGatewayDisputeCreatedHandler.cs` | After `e18edbe`: persist OPEN, `MarkDisputed`, `HasOpenDispute`. **Does not** publish `GatewayRefundCompleted`. |
| `apps/lazuar-api/Modules/Commerce/Application/Commands/RecordRefundCommandHandler.cs` | Remaining machine. `DISPUTED` is refundable. Already-`REFUNDED` rejected. |
| `apps/lazuar-api/Modules/Commerce/Domain/Entities/CommerceTransactionLog.cs` | Statuses + `ApplyRefund` cap. `MarkDisputed` overwrites any status. |
| `apps/lazuar-api/Modules/Commerce/Domain/Aggregates/Subscription.cs` | `HasOpenDispute` set-only. Never cleared. |
| `apps/lazuar-api/Modules/Commerce/Application/SstTaxMath.cs` | SST `02` only if merchant has SST id + type + rate + net > 0. |
| `apps/lazuar-api/Modules/Commerce/Application/SubscriptionBillingAmount.cs` | Gross = net + SST. Used by billing engine / renewal checkout. |
| `apps/lazuar-api/Modules/Commerce/Application/Commands/InitiateCheckoutCommandHandler.cs` | **Only** writer of `sst_tax_amount` metadata. `$0` vault stamps `type=trial` or `commerce_subscription`. |
| `apps/lazuar-api/Modules/Commerce/Application/Commands/ProcessZeroAmountCheckoutCommand.cs` | Non-vault `$0`. Trial publishes `OriginalAmount = catalog`, `DiscountAmount = 0`. |
| `apps/lazuar-api/Modules/Commerce/Application/CommerceCheckoutMetadata.cs` | `IsCommerceSubscriptionType` is `commerce_subscription` **or** `saas_subscription`. **Not** `trial`. |
| `apps/lazuar-api/Modules/Commerce/Application/RenewalCheckoutIssuer.cs` | Gross includes SST. Metadata has **no** `sst_tax_*`. |
| `apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/BillingEngineJob.cs` | Off-session amount is SST-gross. Event has **no** tax field. |
| `apps/lazuar-api/Modules/Commerce/Infrastructure/EventHandlers/GatewayRefundCompletedIntegrationEventHandler.cs` | Apply lock: only `REFUND_PENDING`. |
| `apps/lazuar-api/Modules/Payments/Contracts/PlatformCheckoutTypes.cs` | `utility_credit_topup`, `platform_saas_fee`. |
| `apps/lazuar-api/Modules/Payments/Contracts/Events/GatewayPaymentCompletedIntegrationEvent.cs` | Has `TaxAmount`, `FxRate`, `BaseCurrency`. |
| `apps/lazuar-api/Modules/Payments/Contracts/Events/GatewayRefundCompletedIntegrationEvent.cs` | **No** `FxRate`. **No** `BaseCurrency`. |
| `apps/lazuar-api/Modules/Payments/Application/Commands/ProcessGatewayWebhookCommandHandler.cs` | Allow-list: `PAYMENT_COMPLETED` / `DISPUTE_CREATED` / `PAYMENT_FAILED`. Refund events dropped. Fee args hardcoded `0`. Publishes `$0` sessions as Completed. |
| `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/StripeGatewayAdapter.cs` | `$0` + vault = Checkout `setup` mode. Session completed → `PAYMENT_COMPLETED`, `AmountTotal ?? 0`. `IssueRefundAsync` treats `pending` as success. Off-session metadata has no SST. |
| `apps/lazuar-api/Modules/Payments/Infrastructure/EventHandlers/GatewayRefundRequestedIntegrationEventHandler.cs` | Adapter success → Completed, `RefundedFee = 0`. |
| `apps/lazuar-api/Modules/Payments/Infrastructure/EventHandlers/ExecuteOffSessionChargeIntegrationEventHandler.cs` | Charges `event.Amount` (already SST-gross). Does not stamp tax metadata. |
| `apps/lazuar-api/Modules/Lhdn/Application/Commands/CancelTaxDocumentCommand.cs` | On MyInvois success publishes `LhdnDocumentCancelledIntegrationEvent`. |
| `apps/lazuar-api/Modules/Lhdn/Application/Commands/SubmitTaxDocumentCommand.cs` | Live deduct after persist; deduct failure **logged**, not thrown. |
| `apps/lazuar-api/Modules/Lhdn/Infrastructure/EventHandlers/GatewayRefundCompletedIntegrationEventHandler.cs` | Full only. ≤72h cancel. ≥72h type-02 CN. `Total_including_tax = RefundedAmount + TaxAmount`. |
| `apps/lazuar-api/Modules/Lhdn/Infrastructure/EventHandlers/B2bTaxInvoiceRequestedIntegrationEventHandler.cs` | Uses event `TaxAmount` as MyInvois tax. |
| `apps/lazuar-api/tests/Lazuar.ModuleTests/Billing/**` | Handler matrix, refund tax, SaaS, top-up, clawback, series, domain. |
| `apps/lazuar-api/tests/Modules.Billing.Tests/**` | Wallet + hold domain only. |
| `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/CommerceGatewayDisputeCreatedHandlerTests.cs` | Locks “did not publish Completed”. |
| `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/RecordRefundCommandHandlerTests.cs` | Locks `DISPUTED` → real refund path. |
| `apps/lazuar-api/tests/Lazuar.IntegrationTests/BillingQueryServiceTests.cs` | Summary polarity on live-or-ignore Postgres. |

---

## 2. Intended journal rules (what the code claims)

The Billing README still sells the module as financial truth:

> The `Billing` module is the **Core Domain for Financial Truth**.  
> … Double-entry `LedgerEntry` / `LedgerLine` …  
> Never calculate MRR, net cash, or tax payable by querying gateway tables or Commerce payment-log rows alone.

`LedgerEntry.ValidateBalanced` restates the same promise in stronger language:

```152:165:apps/lazuar-api/Modules/Billing/Domain/Aggregates/LedgerEntry.cs
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

That is the **only** invariant the aggregate enforces. It does not check:

- original-currency sums (`Amount` can disagree with `BaseCurrencyAmount`);
- per-currency balance on a multi-currency header;
- debit/credit sign convention (cash/fees/contra are booked positive; revenue/tax/deferred negative);
- that a second journal on the same economic fact has not already contra’d the first.

A journal that puts cash and revenue on the **same** sign still “balances” if the numbers cancel. The risk in this module is **wrong but balanced** books, not unbalanced ones — except the one writer that actually throws (`ZeroAmountCheckoutHandler`, B05-L03).

Signed convention the live writers use:

| Account | Sale | Refund | Utility top-up | Hub SaaS fee | Utility chargeback | LHDN cancel |
|---------|------|--------|----------------|--------------|--------------------|-------------|
| `ASSET_CASH` | + net | − (refund − fee) | − paid | − paid | + paid (mirror) | − original cash |
| `EXPENSE_GATEWAY_FEE` | + fee | − fee **if** `RefundedFee > 0` (today always 0) | — | — | — | − original fee |
| `REVENUE_GROSS` | − (paid − tax) | not touched | — | — | — | + original gross |
| `CONTRA_REVENUE_REFUNDS` | — | + (refund − tax) | — | — | — | — |
| `LIABILITY_TAX_PAYABLE` | − tax | + tax | — | — | — | + original tax |
| `EXPENSE_SOFTWARE_SUBSCRIPTION` | — | — | + paid | + paid | − paid | — |
| `EXPENSE_DISCOUNT` | — | — | — | — | — | — |
| `ASSET_ACCOUNTS_RECEIVABLE` / `LIABILITY_DEFERRED_REVENUE` | invoice-issued only (dead) | — | — | — | — | — |

Idempotency grain (`LedgerReferenceTypes` + `ReferenceId`):

| Type | Writer | `ReferenceId` |
|------|--------|---------------|
| `GATEWAY_PAYMENT` | `GatewayPaymentCompletedHandler` | gateway transaction id |
| `GATEWAY_REFUND` | `GatewayRefundCompletedHandler` | `{PaymentRecordId:N}:{event.Id:N}` **per attempt** |
| `MANUAL_ENROLLMENT` | `ManualSubscriberEnrolledIntegrationEventHandler` | transaction log id (subscription id if empty Guid) |
| `SYSTEM_CREDIT_TOPUP` | `PlatformTopUpEventHandler` | gateway transaction id |
| `SYSTEM_CREDIT_CHARGEBACK` | `ChargebackClawbackHandler` | gateway transaction id |
| `SYSTEM_SAAS_FEE` | `PlatformSaasFeeHandler` | gateway transaction id |
| `LHDN_CANCELLATION` | `LhdnDocumentCancelledIntegrationEventHandler` | LHDN internal id (usually `INV-…`) |
| `INVOICE_ISSUED` | `InvoiceIssuedHandler` | invoice number — **event never published** |
| `ZERO_AMOUNT_CHECKOUT` | `ZeroAmountCheckoutHandler` | checkout session id |
| `COMMISSION_ACCRUED` | `CommissionAccruedHandler` | commission id |

There is still **no** `GATEWAY_DISPUTE` / `GATEWAY_CHARGEBACK` reference type. After `e18edbe` that is intentional: a GMV dispute is a Commerce flag, not a journal. Cash only reverses when a **real** `GatewayRefundCompleted` arrives.

Unique index (`BillingDbContext.cs:66`):

```csharp
builder.HasIndex(x => new { x.ReferenceType, x.ReferenceId }).IsUnique();
```

Global. Not per organization. Combined with `HasEntryBeenProcessedAsync` (also no org), two tenants that ever share a gateway id collide on insert rather than silently double-book. The grain is still wrong.

Document series (`DocumentSeries.cs`):

```
RCPT → RCPT-{yyyy} → RCPT-2026-00001
QT   → QT-{yyyy}
INV  → INV-{yyyy}
CN   → CN-{yyyy}
SAAS → SAAS-{yyyy} on system org (not a DocumentSeries constant; PlatformSaasFeeHandler inlines it)
```

Year is **UTC** (`Prefix` uses `DateTime.UtcNow`). Malaysia is UTC+8. A payment at 08:00 MYT on 1 January is still the previous UTC year.

Tax on a sale is supposed to be a **slice** of `AmountPaid`, not an add-on:

```
grossRevenue = AmountPaid − tax
ASSET_CASH + FEE + (−grossRevenue) + (−tax) = 0
iff NetAmount + GatewayFee == AmountPaid
```

Commerce SST is supposed to arrive as metadata `sst_tax_amount` / `sst_tax_type=02` when Stripe `TaxAmount` is 0 (we do not run Stripe Tax). `ResolveTaxAmount` prefers `event.TaxAmount` then metadata.

Planes that must **not** take the GMV path (`PlatformCheckoutTypes.IsPlatformCollected`):

- `utility_credit_topup` → wallet + `SYSTEM_CREDIT_TOPUP` only.
- `platform_saas_fee` → Hub period + `SYSTEM_SAAS_FEE` only.

`saas_subscription` is **Commerce GMV**, not plane S. `LedgerBalanceMatrixTests.CommerceSaasSubscriptionMetadata_StillTakesGmvPath` locks that.

---

## 3. Quoted walk — sale (`GatewayPaymentCompleted`)

`ProcessGatewayWebhookCommandHandler.PublishParsedEventAsync` publishes every verified `PAYMENT_COMPLETED`, including setup-mode sessions whose `AmountPaid` is 0. There is no amount floor.

`GatewayPaymentCompletedHandler.HandleAsync`:

1. If `metadata.type` is platform-collected → return. Utility and Hub must not dual-post as creator GMV.  
2. Idempotent on `GATEWAY_PAYMENT` + gateway tx id.  
3. `isB2b` from `is_b2b_required == "true"`.  
4. Lines (`:72-84`):

```72:86:apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/GatewayPaymentCompletedHandler.cs
        entry.AddLine(AccountTypes.AssetCash, @event.NetAmount, @event.Currency, @event.NetAmount * fxRate, baseCurrency);

        if (@event.GatewayFee > 0)
        {
            entry.AddLine(AccountTypes.ExpenseGatewayFee, @event.GatewayFee, @event.Currency, @event.GatewayFee * fxRate, baseCurrency);
        }

        entry.AddLine(AccountTypes.RevenueGross, -grossRevenue, @event.Currency, -grossRevenue * fxRate, baseCurrency, taxType, msic);

        if (taxAmount > 0)
        {
            entry.AddLine(AccountTypes.LiabilityTaxPayable, -taxAmount, @event.Currency, -taxAmount * fxRate, baseCurrency, taxType, msic);
        }
```

5. `ValidateBalanced`. If a webhook ever sends `NetAmount + GatewayFee != AmountPaid`, the handler throws and the inbox retries into dead-letter. That is the correct fail-closed for a broken adapter. Billplz fee is 0 by construction (`ProcessGatewayWebhookCommandHandler` passes `estimatedFeePercentage: 0`, `fixedFee: 0`), so Billplz books the full paid amount as cash. Honest about **our** numbers; dishonest about the Billplz payout file.  
6. Allocate `RCPT-yyyy-#####` (`AssignB2cReceipt` → `PENDING` + `B2C_RECEIPT`) or `INV-yyyy-#####` (`AssignB2bInvoice` → `NOT_REQUIRED`). Amounts over `Lhdn:B2cIndividualThresholdMyr` (default 10000) become `NOT_REQUIRED` + `NEEDS_BUYER_TIN`.  
7. `SaveChanges`.  
8. `GenerateAndStoreDocumentCommand` (Official Receipt or Tax Invoice). B2B also publishes `B2bTaxInvoiceRequestedIntegrationEvent` with `grossRevenue` (resolved) and **`@event.TaxAmount` (raw, not resolved)**.

Tax resolution (`:159-188`):

```159:188:apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/GatewayPaymentCompletedHandler.cs
    private static decimal ResolveTaxAmount(GatewayPaymentCompletedIntegrationEvent @event)
    {
        if (@event.TaxAmount > 0)
        {
            return @event.TaxAmount;
        }

        if (@event.Metadata != null
            && @event.Metadata.TryGetValue("sst_tax_amount", out var raw)
            && decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            && parsed > 0)
        {
            return parsed;
        }

        return 0m;
    }
```

`sst_tax_type` becomes line code `02` only when it is exactly `"02"` **and** tax > 0. Otherwise default `06` / MSIC `004` (B2C) or `022` (B2B).

Happy-path 108 / 8 tax / 3 fee (what `LedgerBalanceMatrixTests.Payment_PostsBalancedSale_AndIsIdempotent` locks):

| Account | Amount |
|---------|--------|
| `ASSET_CASH` | +105 |
| `EXPENSE_GATEWAY_FEE` | +3 |
| `REVENUE_GROSS` | −100 |
| `LIABILITY_TAX_PAYABLE` | −8 |
| Net base | 0 |
| Summary `net_revenue` | 100 − 3 − 8 = **89** |

`$0` is not a skip. `AmountPaid = 0`, `NetAmount = 0`, `grossRevenue = 0` still adds cash 0 and revenue 0, still `ValidateBalanced`, still allocates a `RCPT`, still generates an Official Receipt PDF, still marks B2C `PENDING`. That is the setup-intent path (B05-L02).

`type=trial` is **not** platform-collected and **not** `IsCommerceSubscriptionType`. Billing books it. Commerce’s payment handler returns early. Split-brain: phantom RM 0 receipt, no subscription activation from that webhook.

---

## 4. Quoted walk — refund (`RecordRefund` → `GatewayRefundCompleted`)

Commerce remaining machine (`CommerceTransactionLog`):

```
RemainingAmount = max(0, Amount − RefundedAmount)
ApplyRefund accumulates and caps at Amount
REFUNDED iff RefundedAmount >= Amount
```

`RecordRefundCommandHandler` rejects:

- org mismatch → “Transaction log not found”;
- `REFUNDED` or `RemainingAmount <= 0` → `ALREADY_REFUNDED`;
- `REFUND_PENDING` → `REFUND_PENDING`;
- status not in `{CONFIRMED, DISPUTED, PARTIALLY_REFUNDED, REFUND_FAILED}` → `REFUND_NOT_ALLOWED`;
- empty external ref → `NO_GATEWAY_REFERENCE`;
- amount ≤ 0 → `INVALID_AMOUNT`;
- amount > remaining → `AMOUNT_EXCEEDS_REMAINING`.

`DISPUTED` is **refundable**. That is the post-`e18edbe` contract: a chargeback is a flag; the later real refund uses this path. Test `Handle_FromDisputed_MarkRefunded_PublishesCompleted` locks Billplz mark-refunded of a `DISPUTED` row → `REFUNDED` + outbox `GatewayRefundCompleted` + `IsFullRefund=true`.

Mark-refunded rails (`BILLPLZ`, `OFFLINE`, `BANK_TRANSFER`, `CASH`, `MANUAL_OFFLINE`, `COMPED`): `ApplyRefund` now, publish Completed with `RefundedFee = 0`, `TaxAmount = request.TaxAmount` (ops modal **never** sends `tax_amount`, so 0).

API rails (`STRIPE`, `CHIP`, `RAZORPAY`, `XENDIT`): `MarkRefundPending`, publish `GatewayRefundRequested`. Payments adapter success republishes Completed, also `RefundedFee = 0`. Stripe `IssueRefundAsync` returns true for status `succeeded` **or `pending`**. There is no `charge.refund.updated` consumer.

Commerce Completed consumer applies **only** if status is `REFUND_PENDING`. Mark-refunded already applied; redelivery is a no-op. A dispute-originated Completed (no longer published) would have no-op’d here anyway.

Billing `GatewayRefundCompletedHandler`:

```32:79:apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/GatewayRefundCompletedHandler.cs
        if (@event.RefundedAmount <= 0)
            return;

        var referenceType = LedgerReferenceTypes.GatewayRefund;
        // Per refund attempt (event id), not per capture. A second slice must post a new row.
        var referenceId = @event.PaymentRecordId.ToString("N") + ":" + @event.Id.ToString("N");

        if (await _repository.HasEntryBeenProcessedAsync(referenceType, referenceId))
            return;

        var taxRefund = await ResolveTaxRefundAmountAsync(@event);
        ...
        var cashOutflow = @event.RefundedAmount - @event.RefundedFee;
        var grossRefund = @event.RefundedAmount - taxRefund;

        entry.AddLine(AccountTypes.AssetCash, -cashOutflow, @event.Currency, -cashOutflow, @event.Currency);

        if (@event.RefundedFee > 0)
        {
            entry.AddLine(AccountTypes.ExpenseGatewayFee, -@event.RefundedFee, @event.Currency, -@event.RefundedFee, @event.Currency);
        }

        entry.AddLine(AccountTypes.ContraRevenueRefunds, grossRefund, @event.Currency, grossRefund, @event.Currency);
        ...
        entry.AssignCustomerDocumentNumber(creditNoteNumber);
```

What this does **not** do:

- call `MarkConsolidationNotRequired` — constructor default `CustomerType = "B2C"`, both LHDN and consolidation statuses stay `null`;
- apply `FxRate` — the refund event has no FX fields; base amount = original-currency amount, base currency = event currency;
- ask whether this payment has already been refunded up to `AmountPaid`;
- copy original `TaxTypeCode` / `MsicCode` (defaults `06` / `004` even when the sale was `02` / `022`).

Tax reverse (`:86-125`): prefer `event.TaxAmount`; else load original `GATEWAY_PAYMENT` by **gateway tx + org** and scale. Full if `RefundedAmount >= originalPaid` (`originalGross + originalTax` from **original-currency** `Amount`, not base). Partial: `Round(RefundedAmount / originalPaid * originalTax, 4, AwayFromZero)`.

Each attempt scales from the **original** independently. Two 33 + one 42 on a 108 / 8 sale:

```
33/108 * 8 = 2.4444
33/108 * 8 = 2.4444
42/108 * 8 = 3.1111
sum = 7.9999  ≠  8
```

There is no “remaining tax” on the last slice. `GatewayRefundCompletedHandlerTests.PartialRefund_50Percent_ReversesHalfTax` only locks the 54/108 = 4 case.

`TwoAttempts_TwoLedgerRows` posts two 54-refunds as two rows because each event has a new `Id`. That is the specified per-attempt grain. It is also how a duplicate Completed (new Guid) double-contras: Billing will happily book `108 + 108` against a 108 sale. Commerce remaining is the only cap, and it only exists on the `RecordRefund` path.

Full refund of the 108 / 8 / 3 sale (fee not reversed):

| Account | Payment | Refund | Net |
|---------|---------|--------|-----|
| `ASSET_CASH` | +105 | −108 | −3 |
| `EXPENSE_GATEWAY_FEE` | +3 | 0 | +3 |
| `REVENUE_GROSS` | −100 | 0 | −100 |
| `CONTRA_REVENUE_REFUNDS` | 0 | +100 | +100 |
| `LIABILITY_TAX_PAYABLE` | −8 | +8 | 0 |
| Summary net | 89 | | **−3** |

`LedgerBalanceMatrixTests.PaymentThenFullRefund_NetsRevenueToZeroGrossMinusFees` **asserts** net = −3. Fees stay as expense. Specified, not labelled on the dashboard.

Lhdn on the same Completed: `!IsFullRefund` → return. A 100% refund of a **previously partial** payment is `IsFullRefund=true` because remaining hit 0; LHDN then tries to cancel/CN the **whole** original invoice. Partial MyInvois does not exist. ≤72h → `CancelTaxDocumentCommand` → `LhdnDocumentCancelledIntegrationEvent` → Billing mirrors the **sale** (B05-L01). ≥72h → type 02 with `Total_including_tax = RefundedAmount + TaxAmount` while `RefundedAmount` is already the cash/gross figure (B05-L23). Ops never sends `tax_amount`, so CN tax is usually 0 even when Billing reversed SST from the original payment.

---

## 5. Quoted walk — disputes after `e18edbe`

### 5.1 Commerce GMV (the fix)

`CommerceGatewayDisputeCreatedHandler` class comment now says the truth:

```12:15:apps/lazuar-api/Modules/Commerce/Infrastructure/EventHandlers/CommerceGatewayDisputeCreatedHandler.cs
/// <summary>
/// Persists Commerce GMV disputes. Platform utility / Hub SaaS types are owned by Billing.
/// Does not cancel the subscription or book the dispute as a refund.
/// </summary>
```

Handle:

1. Platform-collected type → return.  
2. Existing `(OrganizationId, GatewayTransactionId)` dispute → `TryMarkHasOpenDisputeAsync` (heal the flag) → return. One OPEN row.  
3. Resolve `subscription_id` / checkout session. If the Guid is a subscription in this org, `MarkHasOpenDispute()`.  
4. Insert `CommerceDispute` (`StatusOpen` only — there is still no WON / LOST / CLOSED).  
5. If a transaction log has `ExternalReference == GatewayTransactionId`, `log.MarkDisputed()`.  
6. `SaveChanges`. Log warning. **Do not publish `GatewayRefundCompleted`.** **Do not cancel.**

`HasOpenDispute` (`Subscription.cs:201-205`) is a one-way latch. There is no `ClearHasOpenDispute`. A won dispute, a later refund, a cancel — the bit stays true.

`MarkDisputed` (`CommerceTransactionLog.cs:135-138`) is `Status = DISPUTED` with no guard. It overwrites `REFUNDED`, `PARTIALLY_REFUNDED`, `REFUND_PENDING`. `RefundedAmount` is left alone. Remaining after a full refund is still 0, so `RecordRefund` returns `ALREADY_REFUNDED`. The row **looks** disputed after a successful refund.

Tests (`CommerceGatewayDisputeCreatedHandlerTests`):

- `Replay_SameGatewayTransactionId_PersistsOneRow_AndHealsHasOpenDispute` — one row, heals flag, `AssertDidNotReceiveGatewayRefundCompleted` (outbox count 0).  
- `UtilityType_NoOps` / `PlatformSaasFee_NoOps`.  
- `Subscription_IsNotCanceled` — `ACTIVE` + `HasOpenDispute` + log `DISPUTED` + dispute `OPEN` + **no Completed**.  
- `NoMetadata_PersistsDispute_NoSubMutation`.

This is a real lock, not a rename. Grep of `CommerceGatewayDisputeCreatedHandler.cs` for `GatewayRefundCompleted` is empty.

### 5.2 Later real refund of a `DISPUTED` row

`IsRefundableSourceStatus` includes `StatusDisputed`. Remaining is still the unrefunded cash (`MarkDisputed` does not touch `RefundedAmount`). Ops can API-refund or mark-refund. Billing posts one `GATEWAY_REFUND` keyed `logId:refundEventId`. That is **not** a double reverse. 008’s P0-1 double-book path (refund then dispute → second Completed with `Id = dispute.Id`) is **gone**.

Dispute **then** refund is the intended path. Refund **then** dispute stamps `DISPUTED` on an already-refunded log and does not book a second journal. Status honesty is B05-L17, not a second contra.

### 5.3 What a GMV dispute does **not** do to the ledger

`ChargebackClawbackHandler` on `commerce_subscription` / missing type / anything that is not utility or Hub SaaS:

```56:58:apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/ChargebackClawbackHandler.cs
        // Utility-credit top-ups only — commerce chargebacks are intentionally out of scope for MVP.
        if (type != PlatformCheckoutTypes.UtilityCreditTopup)
            return;
```

No `GATEWAY_DISPUTE` row. No contra. `ASSET_CASH` still says the money is there. If the merchant **loses** and Stripe pulls the charge without a Lazuar `RecordRefund`, inbound `charge.refunded` is dropped (`ProcessGatewayWebhookCommandHandler` allow-list). The books stay sold. That is B05-L16 + B05-L15, not a regression of `e18edbe`. `e18edbe` correctly stopped booking a **held** chargeback as a completed refund. It did not add a won/lost journal.

CHIP / Billplz / Razorpay / Xendit adapters still do not emit `DISPUTE_CREATED`. Only Stripe `charge.dispute.created`.

### 5.4 Utility + Hub SaaS (Billing’s own dispute handler)

`ChargebackClawbackHandler` is still subscribed to `GatewayDisputeCreatedIntegrationEvent`.

| `metadata.type` | Behaviour |
|-----------------|-----------|
| missing | return |
| `platform_saas_fee` | `MarkSaasPastDueAsync`. No credit claw. **No** `SYSTEM_SAAS_FEE` reverse. |
| `utility_credit_topup` | pack-table claw + `SYSTEM_CREDIT_CHARGEBACK` mirror of original top-up lines |
| else | return |

Utility claw **order**:

```63:82:apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/ChargebackClawbackHandler.cs
        var creditsToClawback = _creditOptions.Packages
            .Where(p => p.AmountMyr <= @event.AmountDisputed)
            .OrderByDescending(p => p.AmountMyr)
            .Select(p => (int?)p.Credits)
            .FirstOrDefault() ?? 0;

        if (creditsToClawback > 0)
        {
            await _mediator.Send(new ClawbackCreditsCommand(
                tenantId,
                creditsToClawback,
                $"Chargeback clawback: {@event.GatewayTransactionId}"));
            ...
        }

        await ReverseUtilityTopUpLedgerAsync(tenantId, @event);
```

`ClawbackCreditsCommandHandler` has **no** idempotency key. `ReverseUtilityTopUpLedgerAsync` **is** idempotent on `SYSTEM_CREDIT_CHARGEBACK` + gateway tx. Inbox retry after a successful claw + successful reverse: ledger no-ops, **wallet claws again**. `Clawback` clamps at 0, so the second pass eats starter grant and any later top-up. That is B05-L04.

Claw amount is “highest pack with `AmountMyr <= AmountDisputed`”, the same function as grant, **not** credits actually granted on that tx. A RM 40 partial dispute on a RM 50 pack grants 0 claw (`FirstOrDefault() ?? 0`) but still reverses the full top-up journal if the original row exists. A RM 200 dispute against packs 50/500, 100/1100, 200/2500 claws 2500 even if the original payment bought the 50-pack. B05-L18.

Hub SaaS dispute: `PAST_DUE`, period dates untouched, `SYSTEM_SAAS_FEE` stays. Tenant books still say they paid Lazuar for the period. B05-L36.

Class comment (`:18-25`) still says “utility clawback only” and never mentions SaaS `PAST_DUE`. Stale (B05-L39).

---

## 6. Quoted walk — Hub SaaS fee (`Saas:Plan:AmountMyr`)

Repo config (`appsettings.json:87-94`):

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

`SaasOptions.Plan.AmountMyr` defaults to 0 if unbound.

`CreateSaasCheckoutCommandHandler`:

```47:49:apps/lazuar-api/Modules/Billing/Infrastructure/Commands/CreateSaasCheckoutCommandHandler.cs
        if (plan.AmountMyr <= 0)
            throw new InvalidOperationException("Hub plan price is not configured.");
```

`POST /admin/billing/saas/checkout` maps that to HTTP 400. `GET /admin/billing/saas` returns `UNPAID` when no row exists, plus the **current** config plan (UI price is whatever is in config now, not what was paid). In this repo every workspace is Hub-unpaid unless an operator overlays a positive MYR amount.

That is not a crash. It is a dark switch. Do not sell a Hub subscription against default config. Public pricing `Checkout_is_free = planAmount <= 0` is true. B05-L20.

When an operator **does** set a price:

1. Upsert `WorkspaceSaasSubscription` as `UNPAID` if missing. Does **not** reset an `ACTIVE` row.  
2. Metadata `type=platform_saas_fee`, `tenant_id=paying org`, `plan_code`.  
3. `GenerateSystemCheckoutSessionQuery` on **system** keys, amount = `plan.AmountMyr` only.  
4. Webhook → `PlatformSaasFeeHandler`. Amount **must** equal config and currency (`:64-72`). Stale checkout after a price change logs and returns without activating.  
5. `ActivateFromPayment`: period starts at `max(now, CurrentPeriodEnd)`. Interval `mo`/`yr` only.  
6. Journal on the **paying** tenant: `EXPENSE_SOFTWARE_SUBSCRIPTION +` / `ASSET_CASH −`. `SYSTEM_SAAS_FEE` + gateway tx. `SAAS-yyyy-#####` allocated on **system** org sequence. `MarkConsolidationNotRequired`. PDF via `GenerateAndStorePlatformSaasInvoiceCommand`. **Never** `InvoiceIssuedIntegrationEvent` (the handler holds a dummy `typeof` + unused bus so reviewers can see the refusal).  
7. `GatewayPaymentCompletedHandler` skipped (platform-collected). `PlatformTopUpEventHandler` skipped (wrong type). No credits.

Seller SST is 0 (`Saas:Seller.SstRate`, reason “Supplier not SST-registered”). No SST on the Hub invoice.

SaaS dispute does not reverse this journal (above). A later successful re-pay extends from `CurrentPeriodEnd` even if status is `PAST_DUE` (`ActivateFromPayment` does not check status).

---

## 7. Quoted walk — credits / wallet

Config:

```
WhatsAppSend = 0
LhdnSubmit   = 3
Packages: 50→500, 100→1100, 200→2500
StarterGrant: 50
```

`CreditCostService.GetCost`: missing / unparsed / unknown enum → **0**. Locked by `CreditCostServiceTests` including a live read of `appsettings.json`.

Wallet:

- `TopUp` requires `credits > 0`.  
- `Deduct` requires `credits > 0` and sufficient balance; else 402.  
- `Clawback` clamps; does not throw.  
- `xmin` concurrency token.  
- Unique wallet per org.  
- Deduct idempotency unique `(OrganizationId, IdempotencyKey)`. Concurrent same-key unique-violation is treated as success.

Starter: `StarterCreditSeederHandler` on `AppEntitlementGranted` / `AppId == BILLING`. Skip if wallet exists. Grant `<= 0` skip. One-time. **No** double-entry journal for the free grant (credits appear; `ASSET` / `EXPENSE` do not). That is acceptable for a promo if labelled; `GetCreditBalanceWithHistoryAsync` will show it as a top-up-shaped `CreditLedger` row.

Utility purchase: `PlatformTopUpEventHandler`. Highest pack `AmountMyr <= AmountPaid`. `credits > 0` required to write **anything**. A RM 49 payment against packs at 50/100/200 grants 0 credits **and writes no ledger row**. The Stripe/system payment succeeded. Billing has no trace. B05-L19. Overpay grants the next-lower pack only (RM 99 → 500 credits, not 1100).

`POST /admin/billing/credits/top-up` refuses `< 50`, so the HTTP path cannot create the 49-ring. A hand-built system checkout or a future UI that sends 49 still hits the handler.

Live meter is **one** function: `SubmitTaxDocumentCommand` in live mode when `LhdnSubmit > 0`. Deduct key `lhdn:{idempotencyKey}` or `lhdn:{taxDocument.Id}`. Document is saved **first**. Deduct failure is logged:

```148:169:apps/lazuar-api/Modules/Lhdn/Application/Commands/SubmitTaxDocumentCommand.cs
        // Deduct credits for the submission. Idempotent on the LHDN idempotency key (or document id),
        // so a retried command cannot double-charge. Test mode and a configured cost of 0 skip
        // Deduct (domain forbids Deduct(0)). The document is already persisted; a deduction
        // failure is logged rather than failing the submission.
        if (shouldMeter)
        {
            try
            {
                ...
                await _mediator.Send(new DeductTenantCreditCommand(...), ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LHDN document {DocId} saved for tenant {OrganizationId} but credit deduction failed.", ...);
            }
        }
```

A wallet bug or a missing wallet gives a **free** MyInvois submit. Sufficiency is checked up front, so the common case is “check passed, then a concurrent deduct emptied the wallet, then this deduct throws 402, then we swallow it.” B05-L21.

`LhdnDocumentSubmittedIntegrationEventHandler` does not deduct. Comment records a prior hard-coded deduct of 1. Tests in `LhdnSingleCreditPathTests` lock live deduct 3, test-mode no deduct, cost 0 no `Deduct(0)`, not hardcoded 1.

WhatsApp console is not this slice’s meter (Communications). Cost 0 + `IsBillable => false` on `ConsoleMessagingService`. Not re-opened here.

Credit holds: `ReserveCreditsCommand` deducts then inserts `CreditHold`. Index is `(OrganizationId, CorrelationId)` **not unique**. Two reserves of the same broadcast correlation create two holds and deduct twice. `Consume` reduces remaining; status stays `HELD` even at 0. `ReleaseRemaining` always sets `SETTLED` (never `RELEASED`). If consume exhausts the hold and nobody releases, the row stays `HELD` forever with remaining 0. B05-L31.

`ApiCreditPurchasedHandler` would book another `SYSTEM_CREDIT_TOPUP` on the same gateway tx. It is **not** in `AddBillingModule` / `UseBillingSubscriptions`. Unique index would have saved a double journal; the wallet `TopUp` has no such index. Leave as residue.

---

## 8. Quoted walk — document series races

`GenerateNextSequenceNumberCommandHandler`:

```23:42:apps/lazuar-api/Modules/Billing/Infrastructure/Commands/GenerateNextSequenceNumberCommandHandler.cs
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        // Atomically upserts and returns the incremented sequence value. 
        // This is safe under concurrency and prevents sequence gaps during rollbacks.
        const string sql = @"
            INSERT INTO billing.""DocumentSequences"" ...
            ON CONFLICT (""OrganizationId"", ""Prefix"") 
            DO UPDATE SET ""CurrentValue"" = ... + 1
            RETURNING ""CurrentValue"";";
```

The SQL is safe under concurrency **for the sequence table**. It is a **different** connection from `BillingDbContext`. It commits before `LedgerEntry.SaveChanges`.

If `SaveChanges` fails after allocation:

- the number is burned (gap);
- inbox retry allocates the **next** number;
- the first number never appears on a ledger row.

Gaps are acceptable for commercial numbers. The comment “prevents sequence gaps during rollbacks” is the opposite of what the code does. B05-L09.

Year prefix is UTC. First eight hours of 1 January MYT still mint `RCPT-2025-#####`. B05-L25.

Assign methods use `??=`. A retry that somehow loaded the same in-memory entry would keep the first number. A retry that constructs a **new** `LedgerEntry` (the live handlers always do) asks the sequence again. Combined with `HasEntryBeenProcessed` **before** allocate, a crash **after** save does not double-allocate. A crash **between** allocate and save does.

`CustomerFacingNumber` never returns a raw GUID; fallback is non-GUID `TaxInvoiceId`, else `"PENDING"`. Locked by `DocumentSeriesTests`.

`UpdateLhdnStatus` still writes the MyInvois UUID into `TaxInvoiceId` (`LedgerEntry.cs:142-147`). `MarkConsolidatedPending` overwrites `TaxInvoiceId` with `B2C-CONS-…` (`:129-134`). `CustomerDocumentNumber` is preserved. New readers must prefer `CustomerDocumentNumber` + `LhdnDocumentUuid`. `LedgerLhdnLookup` still searches all three, so a UUID lookup can hit a validated sale. Fine. A consolidation-ref lookup hits every receipt in the batch (`LhdnDocumentValidated` uses that on purpose).

`GenerateAndStoreDocumentCommandHandler` builds line items from `REVENUE_GROSS` and `REVENUE_RECOGNIZED` only. A `GATEWAY_REFUND` has `CONTRA_REVENUE_REFUNDS`. If anything asks for a Credit Note PDF of that row (LHDN validate path would, if a CN were validated against the refund’s customer number), subtotal is 0, tax is `abs(tax line)`, total is tax only. The refund writer does **not** call `GenerateAndStoreDocumentCommand` today, so this is latent. B05-L34.

Payment path: allocate → save → PDF. If PDF throws, inbox retries, `HasEntryBeenProcessed` is true, handler returns, **PDF never generated**. B05-L22.

---

## 9. Quoted walk — tax on ledger vs Commerce SST

Commerce SST math (`SstTaxMath.Compute`): only if the merchant billing profile has an SST registration number, product type is `02`, rate > 0, net > 0. Rounded to 2 dp, AwayFromZero. `SubscriptionBillingAmount.Gross` = net + that tax.

**Hop-1 checkout** (`InitiateCheckoutCommandHandler:336-340`) is the **only** writer of:

```
metadata["sst_tax_type"] = sstType;
metadata["sst_tax_amount"] = (unitTax * quantity).ToString("0.00");
metadata["sst_rate_percent"] = ...
```

Amount sent to the gateway is `unitGross` (net + SST). `AmountPaid` on the webhook is that gross. Billing `ResolveTaxAmount` splits it. Ledger is correct **for the first charge that carried metadata**.

**Every later charge does not stamp `sst_tax_*`:**

- `BillingEngineJob` publishes `ExecuteOffSessionChargeIntegrationEvent(Amount = SubscriptionBillingAmount.Gross(...))`. The event has no tax field.  
- `StripeGatewayAdapter.BuildOffSessionMetadata` writes `type`, `subscription_id`, `tenant_id`, `receipt`, optional dunning/attempt ids. No SST.  
- `payment_intent.succeeded` parser sets `TaxAmount: 0`.  
- `RenewalCheckoutIssuer` sends SST-gross amount with metadata `{type, subscription_id, tenant_id}` only.  
- Dunning `AUTO_CHARGE` is the same off-session event.

Billing then books:

```
grossRevenue = AmountPaid − 0 = full SST-inclusive cash
REVENUE_GROSS = −(net + SST)
LIABILITY_TAX_PAYABLE = (absent)
```

SST collected from the customer is **revenue**, not tax payable. `GetFinancialSummaryAsync` net does not subtract it. A merchant who is SST-registered and who actually remits 8% is looking at a journal that says they earned the 8%. First-month hop-1 is right; month 2+ is wrong. That is B05-L05. It is the live “tax booked twice / not at all” bug in this slice: Commerce charged SST **and** Billing absorbed it into gross. Not a second `LIABILITY_TAX_PAYABLE` line — worse, **zero** tax lines on the renewals that are most of the life of a subscription.

B2B first charge that **did** split tax on the ledger still publishes:

```128:136:apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/GatewayPaymentCompletedHandler.cs
            await _eventBus.PublishAsync(new B2bTaxInvoiceRequestedIntegrationEvent(
                @event.OrganizationId,
                entry.Id,
                entry.CustomerDocumentNumber!,
                @event.GatewayTransactionId,
                grossRevenue,
                @event.TaxAmount,   // RAW, not ResolveTaxAmount
                @event.Currency,
                correlation));
```

Stripe Checkout `TaxAmount` is `session.TotalDetails.AmountTax`, usually 0 because we do not enable Stripe Tax. `grossRevenue` used the **resolved** metadata tax. MyInvois therefore gets `Total_excluding_tax = net`, `Total_tax = 0`, `Total_including_tax = net`. The legal invoice **understates** the cash by the SST amount. Ledger and MyInvois disagree. B05-L06.

If Stripe Tax were ever enabled **and** we still stamped `sst_tax_amount`, `ResolveTaxAmount` would prefer Stripe’s number and ignore metadata. `AmountPaid` would be Commerce-gross plus Stripe Tax if Stripe added tax on top of a tax-inclusive line. Not the current default. Noted, not filed as P0.

Manual enrollment books `AmountPaid` entirely to `REVENUE_GROSS`. Offline SST is invisible. B2B manual still fires `B2bTaxInvoiceRequested` with `TaxAmount: 0m` hardcoded (`ManualSubscriberEnrolledIntegrationEventHandler.cs:87`). B05-L29.

Refund tax reverse **can** recover hop-1 SST from the original `GATEWAY_PAYMENT` lines even when the Completed event has `TaxAmount = 0`. Renewals that never split tax have nothing to reverse: a full refund of a renewal books the entire cash as `CONTRA_REVENUE_REFUNDS` and leaves `LIABILITY_TAX_PAYABLE` at 0. Internally consistent with the (wrong) sale. Still wrong vs the tax authority.

LHDN cancel of a hop-1 B2B invoice that **did** split tax, after a full refund, is the double reverse in the next section.

---

## 10. Bug catalog

### B05-L01 — P0 — Full B2B refund ≤72h double-reverses cash and tax

**Where.** `GatewayRefundCompletedHandler` already contra’d the sale. Lhdn `GatewayRefundCompletedIntegrationEventHandler` on `IsFullRefund` and `hoursSinceValidation <= 72` sends `CancelTaxDocumentCommand`. That command, on MyInvois success (`CancelTaxDocumentCommand.cs:61`), publishes `LhdnDocumentCancelledIntegrationEvent`. Billing `LhdnDocumentCancelledIntegrationEventHandler` finds the **original payment** by INV / `TaxInvoiceId` / `ReferenceId` and posts `LHDN_CANCELLATION` that negates **every** original line. It does not look for an existing `GATEWAY_REFUND` on the same gateway tx.

**Walk** (108 / 8 tax / 3 fee):

| Account | Payment | `GATEWAY_REFUND` | `LHDN_CANCELLATION` | Net |
|---------|---------|------------------|---------------------|-----|
| `ASSET_CASH` | +105 | −108 | −105 | **−108** |
| `EXPENSE_GATEWAY_FEE` | +3 | 0 | −3 | 0 |
| `REVENUE_GROSS` | −100 | 0 | +100 | 0 |
| `CONTRA_REVENUE_REFUNDS` | 0 | +100 | 0 | +100 |
| `LIABILITY_TAX_PAYABLE` | −8 | +8 | +8 | **+8** |

Cash looks like we paid the customer twice. Tax payable flips sign (we appear to have a tax **asset**). Summary net uses `−SUM(REVENUE_GROSS) − SUM(CONTRA) − fees − (−SUM(TAX))` → `0 − 100 − 0 − (−(+8))` = **−108**. Garbage.

≥72h uses Submit CN, not cancel, so this particular double reverse does not fire. The 72h window is the legally preferred IRBM path.

**Tests.** There is **no** `LhdnDocumentCancelledIntegrationEventHandler` test class. There is no matrix test payment → refund → cancel. `LedgerBalanceMatrixTests.PaymentThenFullRefund_*` stops at the refund.

**008.** P0-2. **Still open.**

---

### B05-L02 — P1 — `$0` Stripe setup booked as GMV `GATEWAY_PAYMENT`

**Where.** `StripeGatewayAdapter.CreateCheckoutSessionOptions` (`:454-472`): `amount == 0 && setupFutureUsage` → Checkout `mode = setup`. `ParseWebhookAsync` on `checkout.session.completed` sets `AmountPaid = (session.AmountTotal ?? 0) / 100`, `EventType = PAYMENT_COMPLETED`, `GatewayTransactionId = PaymentIntentId ?? SetupIntentId ?? session.Id`. `ProcessGatewayWebhookCommandHandler` publishes Completed with no amount floor.

`InitiateCheckoutCommandHandler` uses that path for trials and 100% coupons on vaulting rails, and **overwrites** `type` to `"trial"` for trials (`:299`).

`GatewayPaymentCompletedHandler` does not skip `AmountPaid == 0`. It books cash 0 / revenue 0, allocates `RCPT-yyyy-#####`, generates an Official Receipt, marks B2C `PENDING`.

`CommerceCheckoutMetadata.IsCommerceSubscriptionType("trial")` is false. Commerce’s payment handler returns before opening the session. Trial vault webhook: Billing issues a receipt for RM 0; Commerce does not activate from that event.

100% coupon vault keeps `type=commerce_subscription`. Commerce activates **and** Billing issues a RM 0 receipt.

B2C consolidation later sees `PaidAmount = 0` and, if it is the only row, `MarkConsolidationIgnored`. The `RCPT` number is still burned.

**Tests.** No Billing test that `$0` / `type=trial` is skipped. `GatewayPaymentCompletedHandlerTests` only cover 100-unit B2C/B2B ordering.

---

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

### B05-L04 — P0 — Utility chargeback claw is not idempotent

**Where.** `ChargebackClawbackHandler` sends `ClawbackCreditsCommand` **before** `ReverseUtilityTopUpLedgerAsync`. `ClawbackCreditsCommandHandler` has no idempotency log. Reverse is idempotent on `SYSTEM_CREDIT_CHARGEBACK` + gateway tx.

Inbox redelivery after a full success: ledger count stays 1, wallet claws again. `TenantCreditBalance.Clawback` clamps at 0, so the second pass takes leftover starter credits (50) and any unrelated top-up.

**Lying test.** `ChargebackClawbackHandlerTests.UtilityChargeback_IsIdempotent_OnSecondDispute` only asserts ledger count == 1. It does **not** assert `ClawbackCreditsCommand` was sent once. Name says idempotent. Wallet is not.

`UtilityChargeback_ReversesSystemCreditTopupLedger` uses `Received(1)` because it calls Handle once. A two-call assertion on the mediator would fail today.

---

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

### B05-L06 — P1 — B2B MyInvois tax is raw `event.TaxAmount`, not resolved SST

**Where.** `GatewayPaymentCompletedHandler` publishes `B2bTaxInvoiceRequestedIntegrationEvent(..., grossRevenue, @event.TaxAmount, ...)`. `B2bTaxInvoiceRequestedIntegrationEventHandler` copies that into `Total_tax` / line `Tax_amount`. Stripe session tax is usually 0. Ledger used metadata. Legal invoice excluding+tax ≠ cash. Slice 06 owns XML; the **wrong number is born in Billing**.

---

### B05-L07 — P1 — `GATEWAY_REFUND` rows are B2C/null consolidation and enter `B2cConsolidationJob`

**Where.** Refund writer never calls `MarkConsolidationNotRequired`. Constructor default `CustomerType = "B2C"`. Both statuses null.

`B2cConsolidationJob` selects B2C where `ConsolidationStatus == PENDING` **or** (`ConsolidationStatus == null` and (`LhdnValidationStatus == B2C_RECEIPT` **or** `null`)) (`:157-160`, same predicate at `:111-114`).

A refund header **matches**.

Same-month: the job nets `REVENUE_GROSS − CONTRA_REVENUE_REFUNDS` (`:269-274`). Almost a feature.

Cross-month: the sale month already filed. The refund month computes `grossRevenue = 0 − contra` which is negative, fails `if (grossRevenue > 0)` (`:280`), and if no positive groups remain, **every** row in that month’s batch is `MarkConsolidationIgnored` (`:300-306`). There is no type-02 CN from consolidation. B2C refunds after month-end do not legally reverse the filed batch. If the refund month also has real sales, those sales still consolidate; the refund is simply omitted from the total (filed batch stays high).

W2-LP-101 allocated CN numbers. It did not teach the refund writer to mark consolidation, and it did not teach the job to exclude `GATEWAY_REFUND`.

`B2cConsolidationJobTests` never seeds a refund row.

---

### B05-L08 — P1 — `alreadyConsolidated` check is fail-closed-blind

**Where.** `B2cConsolidationJob.ProcessOrgPeriodAsync:209-211`:

```csharp
        var alreadyConsolidated = await db.LedgerEntries.AnyAsync(e =>
            e.OrganizationId == orgId
            && e.TaxInvoiceId == consolidationRef, ct);
```

No `IgnoreQueryFilters()`. Workers run with empty ambient `TenantId`. `PlatformDbContext` filter is `OrganizationId == TenantId`. `AnyAsync` is **always false** in production.

The job still “works” for the happy path because already-`CONSOLIDATED` rows fail the **select** predicate (which **does** `IgnoreQueryFilters`). The `alreadyConsolidated` short-circuit is dead.

If leftover `PENDING` / null-status rows appear later in a period that already issued `B2C-CONS-{yyyyMM}-{org}` (B05-L07 refunds; late backfill), the job will publish a **second** `ConsolidatedInvoiceIssuedIntegrationEvent` with the **same** `InternalReferenceId`. Lhdn idempotency on that key is the only thing between us and a second type-01. That is slice 06’s problem if it happens; the Billing job is the one that emits the duplicate.

`SecondRun_SamePeriod_IsIdempotent` passes because the first run flipped status, not because `alreadyConsolidated` worked. The test uses empty-tenant InMemory, so the check is false there too.

---

### B05-L09 — P1 — Sequence allocation is not in the ledger transaction; comment lies

See §8. Own Dapper connection. Comment at `:26-27` claims gap-free rollbacks. Gaps are the actual behaviour. Two increments on retry after a failed `SaveChanges`.

---

### B05-L10 — P1 — Unique ledger key is global `(ReferenceType, ReferenceId)`

`BillingDbContext.cs:66`. No `OrganizationId`. Fine for Stripe PaymentIntent ids (globally unique per account). Wrong grain for anything we mint (`MANUAL_ENROLLMENT` uses a Guid so it is safe; a reused Billplz bill id across two tenants on the same Billplz collection is the theoretical collision). Second org fails the insert; inbox dead-letters. Not a silent steal, but a stuck journal.

---

### B05-L11 — P1 — `HasEntryBeenProcessedAsync` ignores tenant

```18:24:apps/lazuar-api/Modules/Billing/Infrastructure/Repositories/LedgerRepository.cs
        return await _context.LedgerEntries
            .IgnoreQueryFilters()
            .AnyAsync(e => e.ReferenceType == referenceType && e.ReferenceId == referenceId, ct);
```

Same grain as the unique index. Workers **must** `IgnoreQueryFilters` (empty tenant). They also **must** then filter by org; this method does not. Org A’s `GATEWAY_PAYMENT` / `pi_123` makes org B’s same id look “already processed” **if** the unique insert did not already throw. The check and the index agree with each other and disagree with tenancy.

Refund tax lookup (`GatewayRefundCompletedHandler:94-100`) **does** filter `OrganizationId`. Inconsistent, and the one that matters for money on refunds is the org-scoped one. The sale idempotency path is the unscoped one.

---

### B05-L12 — P1 — Refund journals drop FX

Sale: `BaseCurrencyAmount = amount * fxRate`, `BaseCurrency = event.BaseCurrency`.  
Refund event: no `FxRate`, no `BaseCurrency`.  
Refund lines: `Amount = BaseCurrencyAmount = cashOutflow`, `BaseCurrency = event.Currency`.

A USD sale booked at `fxRate = 4.7` into MYR is reversed as if USD **were** MYR. `ValidateBalanced` still passes. `GetFinancialSummaryAsync` hardcodes display currency `'MYR'` (`BillingQueryService.cs:151`) and sums `BaseCurrencyAmount`. The refund’s “base” is the wrong currency. Net MYR after a full USD refund is garbage.

No test uses `FxRate != 1` on a refund. Matrix tests are all `MYR` / `1`.

---

### B05-L13 — P1 — Partial refund tax is independently scaled; last slice does not take remainder

See §4. 4 dp AwayFromZero per attempt. No remaining-tax field. Odd splits leak or overshoot the original `LIABILITY_TAX_PAYABLE`. Tests only cover 50%.

---

### B05-L14 — P1 — Billing will book a second full refund if a second Completed arrives

Per-attempt key includes `event.Id` (new v7 every publish). Commerce remaining is the only cap. Mark-refunded + a later inbound refund (if we ever allow-list it) would be two Completeds. Two ops clicks cannot happen (`ALREADY_REFUNDED`). Stripe `pending` treated as success plus a later dashboard confirm is one Completed from us; a later inbound event would be a second if allow-listed. Today inbound is dropped (B05-L15), so the latent double-contra is “any second Completed with a new Guid”.

`TwoAttempts_TwoLedgerRows` celebrates this grain. It does not assert `sum(refund) <= originalPaid`.

---

### B05-L15 — P1 — Inbound refund webhooks are dropped; Stripe `pending` is terminal

`ProcessGatewayWebhookCommandHandler.cs:83-88` only accepts `PAYMENT_COMPLETED`, `DISPUTE_CREATED`, `PAYMENT_FAILED`. Stripe Dashboard / customer-portal / Radar / `charge.refunded` / `refund.updated` never move Commerce or Billing unless someone hits `RecordRefund`.

`StripeGatewayAdapter.IssueRefundAsync` (`:313`) returns true for `pending`. We publish Completed immediately. If Stripe later fails the pending refund, we have already booked Commerce + Billing as refunded. No unwind.

008 P0-3. Still open. Payments ingress is slice 04’s HTTP; the **money lie** is this slice.

---

### B05-L16 — P1 — Lost GMV chargeback never journals unless ops refunds

After `e18edbe` this is the remaining chargeback hole, not a double reverse. OPEN forever. No won/lost. No `GATEWAY_DISPUTE`. Stripe loss that auto-refunds at the processor is B05-L15. Access stays `ACTIVE` (`HasOpenDispute` is a bit, not a gate). Books stay sold.

---

### B05-L17 — P1 — `HasOpenDispute` never clears; `MarkDisputed` overwrites `REFUNDED`

Set-only latch. Refund-then-dispute paints a fully refunded log `DISPUTED` while `RefundedAmount == Amount`. Remaining 0 → `ALREADY_REFUNDED`. Ops list and CSV show `DISPUTED` after the money already went back. Not a second journal. Status lie.

---

### B05-L18 — P1 — Utility clawback uses dispute amount vs pack table, not credits granted

Same `FirstOrDefault` pack function as grant, keyed on `AmountDisputed`. Partial dispute → 0 claw + full ledger reverse (if original exists). Oversize dispute → wrong (larger) pack. Missing original top-up → warning, skip journal, credits may still have been clawed (`ChargebackClawbackHandler:132-137`).

---

### B05-L19 — P1 — Under-pack utility payment is a silent no-op

`PlatformTopUpEventHandler:53` `if (credits > 0)`. RM 49 against min pack 50: no wallet, no ledger, no error. System checkout collected money. `HandleAsync_Skips_When_GatewayTransactionId_Empty` and the already-processed test exist; there is **no** test that 49 MYR is either rejected or booked as unmatched cash.

---

### B05-L20 — P1 — `Saas:Plan:AmountMyr = 0` means unpaid Hub

See §6. Checkout 400. GET returns `UNPAID`. Public page “free today” is true. Not a money-corruption bug. Do not sell plane S against this repo’s default config. Tests lock the throw (`CreateSaasCheckoutCommandHandlerTests.Handle_AmountNotConfigured_Throws`) and the unpaid view.

---

### B05-L21 — P1 — Live LHDN deduct can fail open after persist

See §7. Sufficiency check then persist then deduct. Concurrent empty-wallet → logged, document kept. `LhdnSingleCreditPathTests` do not cover the catch. Meter can under-charge.

---

### B05-L22 — P1 — PDF after `SaveChanges` is not retried

`GatewayPaymentCompletedHandler` and `ManualSubscriberEnrolledIntegrationEventHandler` save first (correct: number must exist), then PDF. Retry hits `HasEntryBeenProcessed` and returns. Receipt/invoice number exists; R2 object may not. Email `DocumentPublished` never fires. Operator sees `PENDING` / broken download. `HandleAsync_WhenB2C_SavesChangesBeforeGeneratingDocument` **locks this order** and does not lock a retry-generates-PDF property.

---

### B05-L23 — P1 — LHDN type-02 CN overstates `Total_including_tax`

`Lhdn GatewayRefundCompletedIntegrationEventHandler.cs:126-135`: `Unit_price = RefundedAmount`, `Total_excluding_tax = RefundedAmount`, `Total_including_tax = RefundedAmount + TaxAmount`. `RefundedAmount` is Commerce cash (gross). Adding tax again is wrong when `TaxAmount > 0`. Today ops sends 0, so the CN is tax-free and **understates** SST reverse vs the Billing journal (which did scale tax from the original payment). Both directions are wrong depending on whether anyone starts sending `tax_amount`. ≥72h also meters 3 LHDN credits for a CN the merchant did not click.

Slice 06 owns the XML. The amounts are born on this Completed event.

---

### B05-L24 — P2 — `ValidateBalanced` is a one-sided toy

Base only. No per-currency. No sign convention. Empty line list sums to 0 (the `$0`-price zero-checkout header). Comments claim 500-year-old certainty. There is no `LedgerEntryBalanceTests`. Coverage is handler composition. The method will not catch B05-L01, L05, L12, L13, L14.

---

### B05-L25 — P2 — Document year is UTC, not MYT

`DocumentSeries.Prefix` uses `DateTime.UtcNow`. Consolidation periods are MYT. A 1 Jan 02:00 MYT sale can be `RCPT-2025-#####` and fall in the 2026-01 consolidation month. Ugly, not a cent-wrong.

---

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

### B05-L27 — P2 — Ledger `type_filter=reversals` omits `SYSTEM_CREDIT_CHARGEBACK`

`BillingQueryService.cs:56-63`: `reversals` = `GATEWAY_REFUND` + `LHDN_CANCELLATION` only. Credit Notes page is this filter. Utility chargebacks do not appear. `sales` excludes those two and therefore **includes** chargebacks, SaaS fees, top-ups, commissions, zero-checkouts.

---

### B05-L28 — P2 — `RefundedFee` is always 0

Mark-refunded hard-codes 0. Payments adapter success hard-codes 0 (“adapters currently do not return reclaimed fee”). Billing never reverses `EXPENSE_GATEWAY_FEE`. Matrix asserts −3 after a full refund. Fine if labelled. Not fine if we sell “exact gateway fees”.

---

### B05-L29 — P2 — Manual enrollment is 100% cash, 0 tax, 0 fee

`ManualSubscriberEnrolledIntegrationEventHandler:51-52`. Offline money is booked as if it hit the bank at 100% with no SST split. B2B still requests a type-01 with `TaxAmount: 0m`. Tests lock save-before-PDF and per-log-id idempotency, not tax.

---

### B05-L30 — P2 — Dead / parked writers that will confuse the next editor

- `InvoiceIssuedHandler` subscribed; `new InvoiceIssuedIntegrationEvent` in production: **zero**.  
- `ManualPaymentRecordedIntegrationEvent`: contract only, no handler.  
- `RevenueRecognitionJob` unregistered. If someone hosts it, `DeferredRevenueSchedules.Where(...)` **without** `IgnoreQueryFilters` sees 0 rows under empty worker tenant.  
- `ApiCreditPurchasedHandler` unregistered.  
- Recognition would write `REVENUE_RECOGNIZED`; summary `recognized_revenue` / `deferred_revenue` are almost always 0.

---

### B05-L31 — P2 — Credit hold: no unique correlation; `RELEASED` never written; exhaust stays `HELD`

See §7. Two reserves of the same broadcast deduct twice. Domain tests cover consume/release math, not the handler race.

---

### B05-L32 — P2 — `LedgerLine` and `CreditLedger` have no `OrganizationId`

Not `IMustHaveTenant`. No global filter on the child table. `GetLedgerEntriesAsync` loads lines by `LedgerEntryId = ANY(@EntryIds)` after the header query filtered by org — safe on that path. A future raw `FROM billing.LedgerLines` without a join is a cross-tenant read. `CreditLedgers` history is loaded by `TenantCreditBalanceId` from an org-scoped wallet — safe on that path.

Admin document download (`AdminLedgerEndpoints:36-46`) does not load the ledger row at all; it presigns `vault/{ctx.TenantId}/documents/{id}.pdf`. Guessing another org’s entry id looks in **your** prefix. Not an IDOR on their PDF. Guessing your own missing id returns a signed 404-from-R2.

---

### B05-L33 — P2 — `$0`-priced `ProcessZeroAmount` writes a no-line journal

`OriginalAmount = 0` → skip `AddLine` → `ValidateBalanced` on empty → header with `ZERO_AMOUNT_CHECKOUT` and no lines. Harmless noise. No test in Billing.

---

### B05-L34 — P2 — Credit-note PDF builder ignores contra lines

See §8. Latent until something generates a document for a `GATEWAY_REFUND` row. LHDN validate of a type-02 keyed by the Billing CN number would.

---

### B05-L35 — P2 — Billplz fee is always 0 in the journal

Adapter formula uses `estimatedFeePercentage` / `fixedFee`. Webhook handler always passes 0, 0, 0 (`ProcessGatewayWebhookCommandHandler:74-76`). Cash = full paid. Payout CSV will not match. Same class of honesty hole as B05-L28.

---

### B05-L36 — P2 — Hub SaaS dispute does not reverse `SYSTEM_SAAS_FEE`

`PAST_DUE` only. Expense/cash stay. A later win has nothing to unwind because nothing was reversed. A later loss that Stripe refunds is B05-L15. Period dates still grant access time.

---

### B05-L37 — P2 — Platform invoice fallback can print a Guid slice

`GenerateAndStorePlatformSaasInvoiceCommandHandler:63-65`: `CustomerDocumentNumber ?? TaxInvoiceId ?? entry.Id.ToString()[..8]`. Sequence usually ran first. If sequence returned whitespace, `AssignPlatformDocumentNumber` is skipped (`string.IsNullOrWhiteSpace` check on the refund path; SaaS always assigns if the mediator returns). Low likelihood.

---

### B05-L38 — P2 — `TaxInvoiceId` is still the dual-use dumping ground

UUID overwrite after validate. Consolidation ref overwrite after batch. `CustomerDocumentNumber` is the real commercial number. Lookup still searches `TaxInvoiceId`. `FirstOrDefault` on multiple matches has no type preference (`LedgerLhdnLookup`). A cancel whose internal id collides with a UUID-shaped `TaxInvoiceId` on the wrong row is theoretical.

---

### B05-L39 — P2 — `ChargebackClawbackHandler` comment still says “utility only”

The SaaS `PAST_DUE` branch has been there since W1-LP-004. Comment at `:18-25` never mentions it. Next editor will miss the branch.

---

### B05-L40 — P2 — `ManualPaymentRecordedIntegrationEvent` has no consumer

Contract exists. Billing README §5 still lists “From B2B/Invoicing: `InvoiceIssuedIntegrationEvent`, `ManualPaymentRecordedIntegrationEvent`.” Manual **enrollment** is a different event and **is** consumed. The recorded-payment event is a lie in the README.

---

## 11. 008 re-verify

`plans/008-evals/03-ledger-refunds-disputes-credits.md` (16 August 2026) is the previous money file. Checked against this tree, not against the ticket notes.

| 008 claim | Now |
|-----------|-----|
| **P0-1.** `CommerceGatewayDisputeCreatedHandler` publishes `GatewayRefundCompleted`. Refund then dispute double-posts two `GATEWAY_REFUND` rows. | **CLOSED** at `e18edbe`. Handler no longer publishes. Tests assert outbox 0. `DISPUTED` is refundable so a later real refund uses the refund path once. |
| **P0-2.** Full B2B refund ≤72h → LHDN cancel → `LHDN_CANCELLATION` mirrors the sale. | **OPEN.** Same handler, same publish at `CancelTaxDocumentCommand.cs:61`, still no test. B05-L01. |
| **P0-3.** Inbound refund webhooks dropped; Stripe `pending` = success. | **OPEN.** Allow-list unchanged. B05-L15. |
| Dispute-as-Completed is “the design W3-LP-094 named.” | **Superseded.** `e18edbe` deliberately undid that reuse. Do not copy 008 §5.2–5.3 forward as current truth. |
| `DISPUTED` is **not** refundable. | **False now.** `IsRefundableSourceStatus` includes it. `Handle_FromDisputed_MarkRefunded_PublishesCompleted` exists. |
| ChargebackClawback is utility + Hub SaaS only; GMV no-op. | **Still true.** |
| `Saas:Plan:AmountMyr = 0`. | **Still true.** |
| Only live LHDN deducts; console WhatsApp must not. | **Still true** (WhatsApp not re-audited here). |
| Sequence not in the ledger transaction; comment lies. | **Still true.** B05-L09. |
| Unique ledger key global. | **Still true.** B05-L10. |
| Refund rows pulled into B2C consolidation. | **Still true.** B05-L07. |
| `RefundedFee` always 0. | **Still true.** B05-L28. |
| Net Cash in Bank is ledger net revenue. | **Still true.** B05-L26. |
| Utility claw uses dispute amount vs pack table. | **Still true.** B05-L18. Plus the **new** retry double-claw (B05-L04) 008 did not name. |
| No test for payment → refund → LHDN cancel. | **Still true.** |
| Wave 3 reused Completed so they would not write a Billing dispute handler. | **Historical.** Current tree has no GMV Billing dispute handler either. The reuse is gone; the dedicated journal was not added. |

Do **not** copy these 008 sentences forward:

- “`CommerceGatewayDisputeCreatedHandler` **does** publish `GatewayRefundCompleted`.”  
- “Wave 3 named that as the GMV ledger contra.” (as a description of **this** tree)  
- “If a refund lands first, a later dispute still publishes a second `GatewayRefundCompleted`.”  
- “`DISPUTED` is not refundable.”

---

## 12. Lying tests

A test lies when its name or comment claims a property the assertions do not lock, or when it green-lights a known wrong journal.

| Test | Why it lies |
|------|-------------|
| `ChargebackClawbackHandlerTests.UtilityChargeback_IsIdempotent_OnSecondDispute` | Name: idempotent. Asserts: one `SYSTEM_CREDIT_CHARGEBACK` row. Does **not** assert `ClawbackCreditsCommand` once. Second Handle **does** send claw again. B05-L04. |
| `LedgerBalanceMatrixTests.PaymentThenFullRefund_NetsRevenueToZeroGrossMinusFees` | Honest about fees-remain, **silent** about the next event on the same fact (`LHDN_CANCELLATION`). Reads as “full refund nets clean” if you stop at the test name. |
| `LedgerBalanceMatrixTests` entire class | Comment: “Asserts double-entry balance and net-revenue math operators can trust for ops dashboards.” Does not cover `$0` setup, trial zero-checkout, FX, renewal-without-`sst_tax_*`, refund+cancel, refund+dispute, under-pack top-up, two full Completeds. Operators cannot trust the matrix for those paths. |
| `GatewayRefundCompletedHandlerTests.TwoAttempts_TwoLedgerRows` | Correct for slices. Does not cap `sum <= original`. Documents the grain that enables B05-L14. |
| `GatewayRefundCompletedHandlerTests.PartialRefund_50Percent_ReversesHalfTax` | 54/108 is exact. Does not lock remainder on 33+33+42. |
| `B2cConsolidationJobTests.SecondRun_SamePeriod_IsIdempotent` | Passes because status flipped, **not** because `alreadyConsolidated` can see rows under empty tenant. Name implies the short-circuit works. B05-L08. |
| `GatewayPaymentCompletedHandlerTests.HandleAsync_WhenB2C_SavesChangesBeforeGeneratingDocument` | Locks order. Does not lock “retry still generates PDF.” Green while B05-L22 is open. |
| `CreateSaasCheckoutCommandHandlerTests` / `GetWorkspaceSaas_UnpaidThenActive` | Honest that 0 throws and unpaid is the empty state. Do not treat them as “Hub billing works in default config.” |
| `ChargebackClawbackHandlerTests.NonUtility_IsNoOp` | Honest. Combined with 008-era comments, a reader might think GMV contra happens somewhere else. After `e18edbe` it happens **nowhere**. The test does not lie; the **suite** has no “GMV dispute does not post `GATEWAY_REFUND`” Billing test (Commerce tests cover the publisher). |
| `LhdnSingleCreditPathTests.Handle_UsesCreditCostService_AndDeductsConfiguredAmountOnce` | Deduct is mocked to succeed. Does not lock the persist-then-log-failure path (B05-L21). |
| `Modules.Billing.Tests.CreditHoldTests` | Domain only. Does not lock unique correlation or handler double-reserve. |
| **Missing classes that 008 already wanted** | No `LhdnDocumentCancelledIntegrationEventHandler` tests. No `ZeroAmountCheckoutHandler` tests. No Billing test that `type=trial` / `AmountPaid=0` is skipped. No payment→refund→cancel matrix. No claw-retry wallet assertion. |

InMemory vs Postgres: `CreditDeductionConcurrencyTests` is honest (skips without Docker; comments that InMemory cannot exercise xmin). `BillingQueryServiceTests` is honest (`Assert.Ignore` if Postgres is down). Those are not lies.

---

## 13. Unread / not fully executed

I read the Billing tree (Domain, Contracts, Application, Infrastructure handlers/commands/workers/endpoints/services, README), the Commerce/Payments/Lhdn files that write this journal, and the Billing + dispute/refund module tests listed in §1. I did **not**:

- run the test suite (`dotnet test`); claims about what a test **asserts** are from source, not from a green run on this machine;
- open every Billing migration designer / snapshot beyond the unique index and `HasOpenDispute` default;
- re-audit WhatsApp dispatch, email, or broadcast (08) beyond “cost 0 / console not billable”;
- re-audit adapter HTTP signatures, CHIP/Xendit/Razorpay refund HTTP, or webhook EventId namespacing (04);
- re-audit LHDN UBL XML, signing, or MyInvois submit transport (06) beyond the amounts Billing/Commerce hand them;
- re-audit ops Sales Insights chrome (09) beyond the agent query that **names** net revenue “cash”;
- read `RevenueRecognitionJob.ProcessRecognitionsAsync` past the query-filter landmine;
- read every line of `GenerateDraftDocumentQueryHandler` after the quote-number fallback;
- read `InvoiceDocumentFactory` / QuestPDF layout;
- read `BillingInboxConsumerJob` / outbox retry policy (10) beyond “handler throw → retry → DLQ”;
- prove a live Stripe setup-mode fixture (code path is clear; no sandbox receipt was pulled).

If a later agent runs the suite, prioritize a trial `ZeroAmountCheckoutHandler` case, a claw-retry wallet case, and a payment→refund→LHDN-cancel matrix. All three must **fail** today.

---

## 14. Ranked open bugs

**P0 — money or tax payable is wrong, or the inbox cannot book a real event**

1. **B05-L01** — Full B2B refund ≤72h + LHDN cancel double-reverses cash and tax. 008 P0-2. Still the worst book in the module.  
2. **B05-L05** — Renewal / dunning / off-session SST is booked as `REVENUE_GROSS`. Tax payable missing for the life of the sub after hop-1.  
3. **B05-L03** — Non-vault trial `ZeroAmountCheckoutCompleted` is unbalanced; Billing DLQs; Commerce already activated.  
4. **B05-L04** — Utility dispute retry claws credits twice. Test named idempotent lies.

**P1 — next-wrong-number, missing journal, or a race that burns a legal number**

5. **B05-L15** — Inbound refunds dropped; Stripe `pending` = Completed. 008 P0-3.  
6. **B05-L16** — Lost GMV chargeback never journals unless ops refunds.  
7. **B05-L07 / B05-L08** — Refund headers in B2C consolidation; `alreadyConsolidated` blind under worker tenant.  
8. **B05-L06** — B2B MyInvois tax 0 when ledger split SST from metadata.  
9. **B05-L02** — `$0` setup / `type=trial` minted as `GATEWAY_PAYMENT` + `RCPT`.  
10. **B05-L09** — Sequence not in the ledger transaction; gap-free comment is false.  
11. **B05-L12 / B05-L13 / B05-L14** — Refund FX dropped; partial tax remainder; second Completed can over-refund the journal.  
12. **B05-L10 / B05-L11** — Global unique key + unscoped `HasEntryBeenProcessed`.  
13. **B05-L18 / B05-L19** — Claw wrong pack / under-pack top-up silent.  
14. **B05-L21 / B05-L22** — LHDN deduct fail-open; PDF not retried.  
15. **B05-L17** — `HasOpenDispute` latch; `MarkDisputed` overwrites `REFUNDED`.  
16. **B05-L20** — `AmountMyr = 0` → do not sell Hub.  
17. **B05-L23** — Type-02 CN including-tax math.

**P2 — honesty, residue, labels**

18. **B05-L24 / L26 / L27 / L28 / L35** — Balance toy; “Net Cash”; reversals filter; fees 0; Billplz fee 0.  
19. **B05-L25 / L29 / L31–L38** — UTC year; manual 100% cash; holds; child tables; empty zero journal; CN PDF; SaaS dispute no reverse; Guid fallback; `TaxInvoiceId` dual-use.  
20. **B05-L30 / L39 / L40** — Dead handlers/events; stale comments; README lists a missing consumer.

**Closed on this branch (do not re-file as open):**

- GMV dispute publishing `GatewayRefundCompleted` (008 P0-1, `e18edbe`).  
- `DISPUTED` blocked from the real refund path (now allowed; tested).  
- WhatsApp console inventing a cost of 1 (not this slice’s meter; still 0).  
- Platform-collected types dual-posting as GMV (`LedgerBalanceMatrixTests` locks skip).  
- Document numbers being LHDN UUIDs (W2-LP-101 still holds on writers).

---

## 15. Verdict for this slice

The **operator-initiated refund loop is still real**. Amounts are the remaining machine. Already-refunded is rejected. `DISPUTED` can take the **real** refund path and Billing will post one `GATEWAY_REFUND` + `CN-`. `e18edbe` did what it said: chargebacks are OPEN rows, `DISPUTED` stamps, `HasOpenDispute` bits. They are not refunds.

The ledger is still **not** audit-grade.

It balances **per entry** (except the trial zero-checkout that throws). It does not stay true **across** refund + LHDN cancel. It does not split SST on the charges that matter after month one. It will book a SetupIntent as a sale. It will claw a utility wallet twice on a retry while a test named “idempotent” stays green. It names P&amp;L net “cash in bank.” It cannot hear a refund that did not start in our modal. Recognition is parked. `AmountMyr = 0` means we do not even have a live Hub invoice in default config.

Sell: “we record your gateway payments and you can refund them from the console; receipts have numbers; credits are for MyInvois if you turn it on; a Stripe dispute is a flag, not a journal.”

Do not sell: “absolute financial truth,” “Net Cash in Bank,” “chargebacks handled,” “SST payable from the ledger,” “CFO OS,” a priced Hub subscription, or “impossible to lose track of a single cent.”

**Next money work (not this file):** stop `LHDN_CANCELLATION` from mirroring a payment that already has a `GATEWAY_REFUND` (B05-L01); stamp `sst_tax_*` on off-session / renewal / dunning and make Billing split it (B05-L05); make `ZeroAmountCheckoutHandler` treat trial as discount = original or skip the journal (B05-L03); claw credits idempotently **after** (or keyed with) the reverse (B05-L04). Until those four move, Billing is a careful journal with four known ways to lie, plus a test suite that calls one of them “idempotent.”
