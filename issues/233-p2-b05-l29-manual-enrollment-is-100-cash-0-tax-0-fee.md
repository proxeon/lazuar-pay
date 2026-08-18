---
number: "233"
id: B05-L29
severity: P2
status: open
source: plans/009-bugs/05-billing-ledger-refunds-disputes.md
head: "297ba98"
---

# 233 — B05-L29 — Manual enrollment is 100% cash, 0 tax, 0 fee

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/05-billing-ledger-refunds-disputes.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B05-L29 — P2 — Manual enrollment is 100% cash, 0 tax, 0 fee

`ManualSubscriberEnrolledIntegrationEventHandler:51-52`. Offline money is booked as if it hit the bank at 100% with no SST split. B2B still requests a type-01 with `TaxAmount: 0m`. Tests lock save-before-PDF and per-log-id idempotency, not tax.

---

## Evaluation (current tree, 2026-08-18)

### What the bug is
Offline / clerk money (`CreateManualSubscriber`, `RecordSubscriberPayment`, `MarkCheckoutAsPaidOffline`) is booked as `ASSET_CASH = AmountPaid` and `REVENUE_GROSS = −AmountPaid`. There is no `LIABILITY_TAX_PAYABLE` split and no `EXPENSE_GATEWAY_FEE` (offline fee is already 0 on the Commerce log). The event type has `AmountPaid` and `IsB2bRequired` but **no tax field**. When `IsB2bRequired` is true the handler still publishes `B2bTaxInvoiceRequestedIntegrationEvent` with `TaxAmount: 0m` hardcoded, so MyInvois type-01 gets `Total_tax = 0` even if the clerk typed an SST-inclusive cash amount. Hop-1 card checkouts can stamp `sst_tax_*` and split the ledger; the offline path never does. Tests lock save-before-PDF and per-transaction-log-id idempotency, not the tax split.

### Still present?
**STILL BROKEN**

Journal is still 100% cash / 100% gross:

```57:58:apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/ManualSubscriberEnrolledIntegrationEventHandler.cs
        entry.AddLine(AccountTypes.AssetCash, @event.AmountPaid, @event.Currency, @event.AmountPaid, @event.Currency);
        entry.AddLine(AccountTypes.RevenueGross, -@event.AmountPaid, @event.Currency, -@event.AmountPaid, @event.Currency);
```

B2B type-01 still requested at tax 0:

```96:104:apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/ManualSubscriberEnrolledIntegrationEventHandler.cs
                await _eventBus.PublishAsync(new B2bTaxInvoiceRequestedIntegrationEvent(
                    @event.OrganizationId,
                    booked.Id,
                    booked.CustomerDocumentNumber ?? "",
                    booked.ReferenceId,
                    @event.AmountPaid,
                    0m,
                    @event.Currency,
                    correlation));
```

Event contract has no tax (`ManualSubscriberEnrolledIntegrationEvent.cs:6-17`). `CreateManualSubscriberCommandHandler` and `RecordSubscriberPaymentCommandHandler` publish `AmountPaid` as typed, default `IsB2bRequired = false`, `feeAmount: 0m`. Offline mark-paid *can* set `IsB2bRequired` from the session (`MarkCheckoutAsPaidOfflineCommandHandler.cs:182, 228`) and custom quotes can include SST in `totalAmount` via `SubscriptionBillingAmount.CustomQuoteBreakdown` — Billing still books the whole gross as revenue and still sends `TaxAmount: 0m` to LHDN.

### Related files
- [`apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/ManualSubscriberEnrolledIntegrationEventHandler.cs`](apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/ManualSubscriberEnrolledIntegrationEventHandler.cs) — 100% cash / 0 tax / B2B `0m`.
- [`apps/lazuar-api/Modules/Commerce/Contracts/Events/ManualSubscriberEnrolledIntegrationEvent.cs`](apps/lazuar-api/Modules/Commerce/Contracts/Events/ManualSubscriberEnrolledIntegrationEvent.cs) — no `TaxAmount`.
- [`apps/lazuar-api/Modules/Commerce/Application/Commands/CreateManualSubscriberCommandHandler.cs`](apps/lazuar-api/Modules/Commerce/Application/Commands/CreateManualSubscriberCommandHandler.cs) — clerk amount, no SST, no B2B flag.
- [`apps/lazuar-api/Modules/Commerce/Application/Commands/RecordSubscriberPaymentCommandHandler.cs`](apps/lazuar-api/Modules/Commerce/Application/Commands/RecordSubscriberPaymentCommandHandler.cs) — same event shape.
- [`apps/lazuar-api/Modules/Commerce/Application/Commands/MarkCheckoutAsPaidOfflineCommandHandler.cs`](apps/lazuar-api/Modules/Commerce/Application/Commands/MarkCheckoutAsPaidOfflineCommandHandler.cs) — can mark B2B and can include SST in the cash total; Billing still ignores tax.
- [`apps/lazuar-api/Modules/Lhdn/Infrastructure/EventHandlers/B2bTaxInvoiceRequestedIntegrationEventHandler.cs`](apps/lazuar-api/Modules/Lhdn/Infrastructure/EventHandlers/B2bTaxInvoiceRequestedIntegrationEventHandler.cs) — `Total_tax = event.TaxAmount` (0).
- [`apps/lazuar-api/tests/Lazuar.ModuleTests/Billing/EventHandlers/ManualSubscriberEnrolledHandlerTests.cs`](apps/lazuar-api/tests/Lazuar.ModuleTests/Billing/EventHandlers/ManualSubscriberEnrolledHandlerTests.cs) — order + idempotency only.

### Tests
- Existing: `ManualSubscriberEnrolledHandlerTests.HandleAsync_SavesChangesBeforeGeneratingDocument`; `HandleAsync_TwoEventsSameSubscription_DifferentTransactionLogIds_BothBook`; `HandleAsync_ReplaySameTransactionLogId_DoesNotAddTwice`. Commerce: `CreateManualSubscriberCommandHandlerTests`, `RecordSubscriberPaymentCommandHandlerTests`, `CommerceProductCompletenessTests` (publishes the event / `IsB2bRequired` on mark-paid).
- None fail while the journal is 100% cash. No test asserts `LIABILITY_TAX_PAYABLE` or a non-zero `B2bTaxInvoiceRequested.TaxAmount` on this path.
- First regression: enroll an SST-registered merchant B2B offline payment of 108 (100 + 8) and assert ledger tax −8 / gross −100, and `B2bTaxInvoiceRequested.TaxAmount == 8` (or whatever Commerce resolved). A B2C offline case should still skip type-01.

### Reproduction today
Arrange an org with an SST id on `TenantBillingProfile` and a product `SstTaxType = 02`. Act: `CreateManualSubscriber` with `AmountPaid = 108` (or mark a B2B checkout paid offline). Assert: `billing.LedgerLines` has cash +108 / revenue −108 and no tax line; if `IsB2bRequired`, outbox `B2bTaxInvoiceRequested` has `TaxAmount = 0`; LHDN type-01 `Total_including_tax` equals the cash with `Total_tax = 0`.

### Blast radius
SST-registered merchants who take bank transfer / cash / mark-paid. Tax payable is understated (offline SST looks like revenue). B2B MyInvois understates tax the same way as **076 / B05-L06** but from a hard-coded 0 rather than a raw Stripe field. Fee is correctly 0 for desk money. Frequency: every offline enrollment / record-payment / mark-paid with `amount > 0`. Still P2 unless we sell “offline e-invoice includes SST”.

### Suggested fix
Add `TaxAmount` (and maybe `TaxType`) to `ManualSubscriberEnrolledIntegrationEvent`. Have Commerce fill it from `SstTaxMath` / `SubscriptionBillingAmount` the same way hop-1 does. Billing: `gross = AmountPaid − tax`, add `LIABILITY_TAX_PAYABLE` when tax > 0, pass the resolved tax into `B2bTaxInvoiceRequested`. Do not invent a fee on `OFFLINE`. Do not TypeSpec-regen. Do not use Stripe Billing. Related honesty: **034** (offline first charge SST) and **095** (`IsB2bRequired` drop on mark-paid) — resolve tax here even if those stay open.

### Evaluation notes
Audit line numbers drifted (`:51-52` is now `:57-58`). Duplicate class of **076 / 096** (B2B tax is not the resolved SST). Residual after 161–200: handler is org-scoped + per-log-id (081/LP-065); tax was never in that work. Still P2. Not blocked on 244 (`ManualPaymentRecorded` is a different unused event).

