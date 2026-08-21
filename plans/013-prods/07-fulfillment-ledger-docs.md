# 07 — Fulfillment, ledger, and receipts in one Pay handler (steal judgment, not Billing/Lhdn modules)

**Family:** 013-prods  
**Paper:** 07 — fulfillment / journal / Official Receipt on first successful pay  
**Date:** 21 August 2026  
**Type:** Uncondensed analysis. **Not** an implementation. **Not** a flip of [011/11](../011-new-lazuar-pay/11-checklist.md) cells to `done`. **Not** a copy of `Modules/Billing`, `Modules/Lhdn`, `Modules/Commerce`, or `Modules/Communications` into `apps/lazuar-pay`.  
**Slice:** first successful pay creates the subscription (or completes a one-off) **and** writes the ledger in the **same handler**. Official Receipt `RCPT-…`. SST exclusive on the unit then × seats; fail closed if SST registration is unknown. Do not title Tax Invoice. Do not print MyInvois VALID.

This is the money kernel of the new host. Sibling 013 papers own production-ready bar, cutover, host seams, merchant Vite, checkout Vite, gateways, One-in-prod, data migration, and CI/kill. This paper owns what happens **after** a verified PSP webhook says paid, **inside** `apps/lazuar-pay`, **in one function**, **in one Postgres transaction**.

Parent law: [011 01-product.md](../011-new-lazuar-pay/01-product.md) (fulfillment / money / documents / mail / audit), [011 03-first-slice.md](../011-new-lazuar-pay/03-first-slice.md) steps 11–12, [011 11-checklist.md](../011-new-lazuar-pay/11-checklist.md) `NP-FUL` / `NP-MON` / `NP-DOC` / `NP-AUD` / `NP-MAIL`, [011 00-why-leave.md](../011-new-lazuar-pay/00-why-leave.md) (`InvoiceIssued`, `TaxInvoiceId`, Guid as number), [011 07-separate-vs-one-binary.md](../011-new-lazuar-pay/07-separate-vs-one-binary.md) and [011 13-monolith-vs-services.md](../011-new-lazuar-pay/13-monolith-vs-services.md) (Notify/Audit in the Pay process), refuse rows `NP-XX-001` … `003`, `010`, `012`.

---

## Binding (read this before the inventory)

| # | Decision | Lock |
|---|----------|------|
| 1 | **Same process as the webhook.** The PSP POST that Pay just verified is the process that books access + journal + `RCPT-`. Do not wait on One to “hear an event.” Do not wait on a Billing inbox worker. | NP-FUL-001, 011/03 step 11 |
| 2 | **Buyer access is a Pay subscription / session row**, not a One membership, not a Zitadel human, not `AppEntitlementGranted` for `AppId == "BILLING"`. | NP-FUL-002, NP-XX-013 |
| 3 | **One function, one `BEGIN`/`COMMIT`:** mark checkout paid, insert or complete subscription / one-off, insert journal header + lines, allocate `RCPT-`, insert receipt header, upsert payer, insert audit row, insert mail-outbox row. | NP-FUL-001, NP-MON-001, NP-DOC-001, NP-AUD-001, NP-MAIL-001, NP-XX-019 |
| 4 | **Official Receipt.** Title is Official Receipt (or “payment receipt”). Number is `RCPT-{MYT year}-{#####}`. Never a UUID. Missing number is the string `PENDING`. Do not title Tax Invoice. Do not print MyInvois VALID. | NP-DOC-001…004, NP-XX-003 |
| 5 | **SST judgment from old `SstTaxMath` / `SubscriptionBillingAmount`, not the Billing/Lhdn folders.** Exclusive on the **unit**, then × seats. Fail closed if registration is **unknown**. Do not undercharge. | NP-MON-003, NP-MON-004 |
| 6 | **Fee is a line only when the PSP actually sent a fee.** `unknown` ≠ 0. | NP-MON-002 |
| 7 | **Refunds reverse the journal once (v1 later). Disputes do not double-reverse.** Do not reuse a refund event as a chargeback. Do not file homemade type `02` / 72h cancel. | NP-MON-005, NP-MON-006, NP-XX-001, NP-XX-010 |
| 8 | **Receipt email is in-process**, not a Notify service, not `DocumentPublishedIntegrationEvent` → Communications inbox → `DispatchMessageIntegrationEvent`. | NP-MAIL-001, NP-XX-019 |
| 9 | **Merchant opens the receipt in ops** (`GET` on Pay `/v1`, One JWT or `lzr_sk_`). VIEWER cannot refund (authz is a 012/013-08 problem; this paper only requires the receipt to exist and be fetchable). | NP-DOC-005, 011/03 step 12 |
| 10 | **Do not copy per-module schemas or an in-process event catalog talking to yourself.** Steal helper *rules*. Leave `InvoiceIssued`, `ManualPaymentRecorded`, `B2bTaxInvoiceRequested`, `TaxInvoiceId`, consolidation, UBL. | NP-XX-001, NP-XX-009, 00-why-leave |

If a later editor “helpfully” publishes `payment.completed` on an in-process bus so Billing-shaped and Commerce-shaped folders can subscribe, this paper has failed. Call the function.

---

## 0. How to read this paper

Three different “paid” stories live in this family. Mixing them is how the old tree got an in-process catalog talking to itself **and** a public HMAC door **and** Stripe/CHIP/Billplz callbacks all called “webhooks”:

| Plane | Direction | Job | This paper |
|-------|-----------|-----|------------|
| **A. PSP → Pay** | Stripe / CHIP / Billplz POST to Pay | Verify, idempotent `(org, provider, event_id)`, then **this handler** | **Yes.** The body of the handler. Ingress/signature/BYOK is [06-money-rails.md](./06-money-rails.md). |
| **B. One → Pay** | One HMAC POST | `member.*`, `tenant.suspended` | **No.** [012/09](../012-one-to-pay/09-webhooks-events.md). Money stays true in Pay if One is late. |
| **C. Pay → merchant / second app** | Pay POST to a stranger | Bezos door `payment.completed` | **Not v1.** Outbox later. Not how Pay talks to **itself**. |

The old Hub golden rule is the disease, not the prescription:

> The `Payments` module is a dumb pipe. `Commerce` manages subscriptions/checkout access state. The `Billing` module manages Truth.

(`apps/lazuar-api/Modules/Billing/README.md` §8.) That sentence is why a verified webhook still has to *publish* `GatewayPaymentCompletedIntegrationEvent` and hope three inboxes agree. New Pay does not have a Payments pipe, a Commerce access owner, and a Billing truth owner. It has **one handler**.

**What exists on this HEAD in the new host.** `POST /v1/checkouts` mints an **in-memory** `CheckoutSession` with `status: "open"` only. There is no webhook route, no Postgres, no journal, no `RCPT-`, no payer, no mail. `CheckoutStore`’s class comment is the honest product state: “In-memory fixture store. Not a ledger. Replace when money is real.”

**What “steal judgment” means here.** Open the old helper. Copy the **rule** (exclusive SST on the unit; MYT year; never print a Guid as “No:”; skip `AmountPaid <= 0`; `ValidateBalanced` before insert). Do **not** copy the type, the schema, the MediatR command, the event, or the folder. Class names below are cited so an implementer can *read* them, not so they can `using Modules.Billing`.

**What this paper will not do.** It will not design UBL, consolidation, types `01`–`14`, XAdES, TIN-at-checkout as a legal feature, debit notes, self-billed 11–14, Stripe Billing `subscription.updated` as SoT, a Hub SaaS fee plane, or a second org table. Tax later = a **provider**. Pay sends amount + buyer; they return VALID + QR. Until that extract exists, the commercial document is an Official Receipt and the word VALID does not appear on it.

---

## 1. Method / SHAs

Nothing was implemented. The following were read in full or in the cited ranges on 21 August 2026.

### Repos / HEAD

| Repo | Path | Short SHA | Full SHA | Tip |
|------|------|-----------|----------|-----|
| Focused Pay (this tree) | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay` | `6f866ff0` | `6f866ff0489a4de77d2fc1b1bbcfa87fbe72b80f` | `feat(pay): scaffold merchant and checkout Vite apps` (2026-08-21 15:15:51 +0800) |
| Lazuar One (sibling) | `/Users/akmalfirdaus/Code/lazuar/lazuar-one` | `0f79fe4` | `0f79fe4f6503847881286ead2e7e57b7c7dc1808` | `WIP:` (2026-08-20 21:24:22 +0800) |

`git rev-parse HEAD` and `git log -1` were run in both working copies on 21 Aug 2026. Pay’s tip is the merchant/checkout Vite scaffold on 8081. One’s tip is unchanged from the 012 family. If either tree moves, re-pin before treating path lists as frozen.

011 papers cited below are dated 20 August 2026 on this Pay tree. 008-evals papers are dated 16 August 2026 against `feat/007-waves-1-4-implement`. 009-bugs papers are dated 17 August 2026 against assigned SHA `297ba98` (working tree then `30d07d2`). **The old helpers have moved since those evals.** Sequence allocation, `ValidateBalanced`, `DocumentSeries` year, and the B2B PDF title on *this* HEAD are not the 16 August text. This paper quotes **live files at `6f866ff0`**. 008/009 remain evidence of the *seams* (events, dual-use columns, Tax Invoice on pay, Guid-as-number, inbox), not a substitute for opening the helper.

### Pay plans (consumer intent)

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/011-new-lazuar-pay/00-why-leave.md` — entire file. `TaxInvoiceId` dumping ground; `InvoiceIssued` subscribed in two modules and constructed in none; `ManualPaymentRecorded` looking like cash; Hub SaaS PDF sliced a Guid; sequence of one money story vs Commerce/Billing/Payments/Lhdn.
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/011-new-lazuar-pay/01-product.md` — entire file. Fulfillment, money truth, documents, buyer plane, mail, audit. Dogfood sentence: one `RCPT-` and a balanced journal.
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/011-new-lazuar-pay/02-one-integration.md` — buyer entitlement is Pay; staff is One; do not `POST` One to grant access in the webhook.
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/011-new-lazuar-pay/03-first-slice.md` — Pay-side steps 11–12; fail locks (Tax Invoice, UUID number, double-journal, Zitadel buyer).
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/011-new-lazuar-pay/07-separate-vs-one-binary.md` — Pay ↔ Notify / Audit cost; `audit.Append` in the same transaction.
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/011-new-lazuar-pay/09-old-pay.md` — steal `SstTaxMath`, series years in MYT, “don’t call it Tax Invoice until VALID.”
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/011-new-lazuar-pay/11-checklist.md` — `NP-FUL-001`…`005`, `NP-MON-001`…`006`, `NP-DOC-001`…`005`, `NP-MAIL-001`…`004`, `NP-AUD-001`…`003`, `NP-BUY-001`…`002`, `NP-XX-001`…`003`, `010`, `012`, `013`, `019`.
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/011-new-lazuar-pay/12-first-slice-tracker.md` — ordered S1 steps 11–12.
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/011-new-lazuar-pay/13-monolith-vs-services.md` — one transaction for charge, ledger, receipt number, audit, enqueue receipt email.

### 008 / 009 / 010 (seams and SST fail-closed)

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/008-evals/03-ledger-refunds-disputes-credits.md` — entire file as the money-seam museum: `GatewayPaymentCompleted` writers, `ValidateBalanced` as then implemented, `DocumentSeries`, refund + LHDN cancel double reverse, dispute-as-`GatewayRefundCompleted` (later patched; the *reuse* is still the lesson), Hub SaaS plane, `InvoiceIssued` dead.
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/008-evals/04-lhdn-invoicing-documents.md` — entire file as the document-honesty museum: Tax Invoice PDF on pay, `RCPT-` vs `INV-`, `CustomerFacingNumber`, `InvoiceIssued` unpublished, SST in Commerce not Lhdn, sandbox VALID not captured.
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/009-bugs/05-billing-ledger-refunds-disputes.md` — files table; intended journal rules; quoted `GatewayPaymentCompletedHandler` walk; `SstTaxMath`; sequence-on-own-connection (stale vs this HEAD); dispute latch after `e18edbe`.
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/009-bugs/06-lhdn-invoices-documents.md` — B06-D02 Tax Invoice on pay; `DocumentSeries`; Official Receipt disclaimer; quote B2B CRM arity (leave it in the museum).
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/010-failed-tests/01-checkout-b2b-sst.md`, `02-initiate-checkout-qty-sst.md` — issue 167: `MerchantHasSstAsync` throws when `IBillingQueryService` is null. That throw is the fail-closed judgment. The test breakage is not a reason to go back to undercharge.

### Old helpers (judgment sources — do not import)

- `apps/lazuar-api/Modules/Commerce/Application/SstTaxMath.cs` — `SstTaxMath.Compute`, `NotApplicable = "06"`, `ServiceTax = "02"`.
- `apps/lazuar-api/Modules/Commerce/Application/SubscriptionBillingAmount.cs` — `GrossBreakdown`, `LineTax`, `StampSstMetadata`, `MerchantHasSstAsync`, `DefaultServiceTaxRatePercent = 8m`, `TaxFromInclusiveGross`.
- `apps/lazuar-api/Modules/Commerce/Application/SubscriptionActivation.cs` — `Start` (period = next bill, not activation instant).
- `apps/lazuar-api/Modules/Commerce/Domain/Aggregates/CheckoutSession.cs` — `OPEN` / `COMPLETED` / `EXPIRED`, `TryCompleteFromPayment`, `CanFulfillFromPayment`, `Quantity`, `ClientProfileId`.
- `apps/lazuar-api/Modules/Commerce/Domain/Aggregates/Subscription.cs` — `PENDING` / `ACTIVE` / `PAST_DUE` / `SUSPENDED` / `CANCELED`, `Quantity`, `UnitAmount`, `HasUnitSnapshot`, `VaultedCustomerId` / `VaultedTokenId`, `IsReminderOnly`.
- `apps/lazuar-api/Modules/Commerce/Domain/Aggregates/Order.cs` — one-off `Complete()`. Do not recreate as a second module.
- `apps/lazuar-api/Modules/Commerce/Domain/Aggregates/Product.cs` — `SetSst`, `SstTaxType` `06`/`02`.
- `apps/lazuar-api/Modules/Commerce/Infrastructure/EventHandlers/GatewayPaymentCompletedIntegrationEventHandler.cs` + `.OpenCheckout.cs` + `.Subscription.cs` — the **access** half of the split.
- `apps/lazuar-api/Modules/CRM/Domain/ClientProfileEntity.cs` — old payer. Steal the small field list, not the module, not TIN/ID as a legal feature.
- `apps/lazuar-api/Modules/Billing/Domain/Aggregates/LedgerEntry.cs` — `ValidateBalanced`, `CustomerDocumentNumber`, `TaxInvoiceId` (refuse the dual-use), `AssignB2cReceipt`.
- `apps/lazuar-api/Modules/Billing/Domain/Entities/LedgerLine.cs` — signed lines, `TaxTypeCode`, `MsicCode` (do not copy MSIC into v1).
- `apps/lazuar-api/Modules/Billing/Domain/Entities/DocumentSequence.cs` — `(OrganizationId, Prefix)`, `Increment`.
- `apps/lazuar-api/Modules/Billing/Domain/AccountTypes.cs` — `AccountTypes`, `LedgerReferenceTypes`, `LhdnValidationStatuses` (refuse the LHDN statuses on a commercial receipt).
- `apps/lazuar-api/Modules/Billing/Domain/Aggregates/TenantBillingProfile.cs` — `SstRegistrationNumber`. Steal the **flag**, not the LHDN stationery card.
- `apps/lazuar-api/Modules/Billing/Contracts/DocumentSeries.cs` — `Receipt = "RCPT"`, `Prefix` via `MalaysiaTime.ToMyt`, `CustomerFacingNumber`.
- `apps/lazuar-api/Modules/Billing/Contracts/MalaysiaTime.cs` — `Asia/Kuala_Lumpur` / `Singapore Standard Time`.
- `apps/lazuar-api/Modules/Billing/Contracts/Commands/GenerateNextSequenceNumberCommand.cs` + `…/Infrastructure/Commands/GenerateNextSequenceNumberCommandHandler.cs` — same `BillingDbContext` as the ledger (this HEAD).
- `apps/lazuar-api/Modules/Billing/Application/IBillingTransactional.cs` + `ILedgerRepository.cs` + `…/Repositories/LedgerRepository.cs` — per-org idempotency grain; `ExecuteInTransactionAsync`.
- `apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/GatewayPaymentCompletedHandler.cs` — the **journal** half of the split; skip `$0`; skip platform-collected; `ResolveTaxAmount` from event then `sst_tax_amount` metadata.
- `apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/GatewayRefundCompletedHandler.cs` — contra pattern to steal **later**; `CN-` series is not v1 S1.
- `apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/InvoiceIssuedHandler.cs` — dead AR writer. Do not port.
- `apps/lazuar-api/Modules/Billing/Contracts/Events/InvoiceIssuedIntegrationEvent.cs` — unpublished in production.
- `apps/lazuar-api/Modules/Billing/Contracts/Events/ManualPaymentRecordedIntegrationEvent.cs` — parked, comment says do not add a second cash journal.
- `apps/lazuar-api/Modules/Billing/Contracts/Events/DocumentPublishedIntegrationEvent.cs` — denormalized mail trigger. Do not port the event; port the *fields* onto the mail-outbox row.
- `apps/lazuar-api/Modules/Billing/Infrastructure/Commands/GenerateAndStoreDocumentCommandHandler.cs` + `…/Documents/InvoiceDocumentFactory.cs` — QuestPDF + R2 + `DocumentPublished`. Steal the Official Receipt disclaimer string, not the R2 key layout, not `ICommerceDocumentLookup`, not `IOneQueryService`.
- `apps/lazuar-api/Modules/Billing/Infrastructure/Endpoints/AdminLedgerEndpoints.cs` — `GET /admin/billing/ledger/{id}/document` is the old NP-DOC-005. New door is Pay `/v1`.
- `apps/lazuar-api/Modules/Lhdn/Infrastructure/EventHandlers/InvoiceIssuedIntegrationEventHandler.cs` — log-only no-op. Proof the event was a lie.
- `apps/lazuar-api/Modules/Communications/Infrastructure/EventHandlers/DocumentPublishedIntegrationEventHandler.cs` — template lookup then `DispatchMessageIntegrationEvent`. Silent return if template missing.
- `apps/lazuar-api/Modules/Messaging/Contracts/DispatchMessageIntegrationEvent.cs` — “any module can publish.” That sentence is NP-XX-009.
- `apps/lazuar-api/Modules/One/Domain/AuditEvent.cs` + `…/Infrastructure/Services/AuditRecorder.cs` + `…/Contracts/IAuditRecorder.cs` — fire-and-forget, **must never throw**, own `SaveChanges`, swallow. Anti-pattern for NP-AUD-001.
- `apps/lazuar-api/Modules/Commerce/Application/Commands/RecordRefundCommandHandler.cs` — `RecordAuditAsync` **after** `SaveChanges`, optional recorder. Not the same transaction.
- `apps/lazuar-api/Modules/Payments/Application/Commands/ProcessGatewayWebhookCommandHandler.cs` — verify then publish. The publish is the seam.
- `apps/lazuar-api/Modules/Payments/Contracts/Events/GatewayPaymentCompletedIntegrationEvent.cs` — the payload the three inboxes deserialize.
- Tests as locks of judgment: `SstTaxMathTests.cs`, `SubscriptionBillingAmountTests.cs` (`GrossBreakdown_PerUnitThenSeats_PinsSenSplit`, `MerchantHasSst_Null_Billing_Throws`), `DocumentSeriesTests.cs` (`Prefix_UsesMalaysiaYear_OnUtcNewYearsEve`, `CustomerFacingNumber_NeverUsesRawUuid`), `LedgerEntryBalanceTests.cs`.

### Focused Pay host (what “paid” must add)

- `apps/lazuar-pay/src/Lazuar.Pay/Program.cs` — health, whoami, org ready, `MapCheckouts`. No webhook. No DB.
- `apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutSession.cs` — `Id`, `OrgId`, `Amount`, `Currency`, `Status`, `SuccessUrl`, `CancelUrl`, `CreatedAt`. Status created as `"open"` only.
- `apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CreateCheckoutRequest.cs` — no payer, no product, no seats, no SST.
- `apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutEndpoints.cs` — member gate; amount > 0; currency default `MYR`; `Idempotency-Key`.
- `apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutStore.cs` — `ConcurrentDictionary`. Comment: not a ledger.
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/CheckoutTests.cs` — `Create_and_get_open_session` asserts `status == "open"`. There is **no** paid test because there is no paid.
- `packages/pay-spec/main.tsp` — `model CheckoutSession` matches the fixture. Comment on the service: “Checkout is a fixture (open session), not a charge.”

### 012 family (org_id, planes, host)

- `plans/012-one-to-pay/06-tenant-org.md` — One tenant id **is** Pay `org_id`. Money rows store a copy of that id, not a Pay `organizations` table.
- `plans/012-one-to-pay/09-webhooks-events.md` — Plane A (One HMAC) ≠ Plane B (PSP). “webhook retry no-ops” in the dogfood sentence is the **PSP** webhook.
- `plans/012-one-to-pay/03-pay-host-seams.md` — no MediatR, no `Modules/One` copy. Same refusal applies to `Modules/Billing`.

### Isolation / tests used as evidence of the fixture

- `CheckoutTests.Create_and_get_open_session` — JSON snake_case `status: open`.
- `CheckoutTests.Create_idempotent_on_key` — create-time idempotency is **not** webhook idempotency. Paid must not reuse the create key as the journal key.

---

## 2. Old money story seams that must not be recreated (events across Commerce / Billing / Payments / Lhdn)

### 2.1 The story that should have been one function

011/00:

> Checkout, ledger, tax document, and gateway webhook are **one money story**. Splitting them into Commerce / Billing / Payments / Lhdn **before** the story is stable does not isolate risk. It hides the story.

The right sequence, already written:

1. One app, one database, `recordPayment()` updates ledger and receipt in the same transaction.
2. Extract a service later if something actually needs its own process.

A tax **provider** already is that extract. Four named platforms as four deploys on day one is not. An **in-process** four-named-platform with four DbContexts, four outboxes, and four inboxes is the shape we left.

### 2.2 What the old tree actually does on a card payment (this HEAD)

Ingress is `ProcessGatewayWebhookCommandHandler.HandleCoreAsync` (`apps/lazuar-api/Modules/Payments/Application/Commands/ProcessGatewayWebhookCommandHandler.cs`). It verifies the adapter signature, allow-lists `PAYMENT_COMPLETED` / `DISPUTE_CREATED` / `DISPUTE_CLOSED` / `PAYMENT_FAILED` / `REFUND_COMPLETED`, dedupes a payments webhook log on `(event_id, gateway, tenant)` (and a business key), then **publishes** `GatewayPaymentCompletedIntegrationEvent`. It does not insert a Commerce subscription. It does not insert a Billing `LedgerEntry`. It does not allocate `RCPT-`. It does not send mail. It does not append a Pay audit row for the charge.

The event (`Modules.Payments.Contracts.Events.GatewayPaymentCompletedIntegrationEvent`):

```csharp
public record GatewayPaymentCompletedIntegrationEvent(
    Guid OrganizationId,
    string GatewayTransactionId,
    decimal AmountPaid,
    string Currency,
    decimal GatewayFee,
    decimal TaxAmount,
    decimal NetAmount,
    decimal FxRate,
    string BaseCurrency,
    List<LineItemDto> LineItems,
    Dictionary<string, string> Metadata,
    string? GatewayCustomerId = null,
    string? GatewayTokenId = null) : IIntegrationEvent
```

That record is the contract between folders that share a process. It is the tax 00-why-leave named: “Lift Commerce into a service” would mean rewriting this, not moving a folder.

**Who subscribes to the same event:**

| Module | Registration | Handler | Job |
|--------|--------------|---------|-----|
| Payments | `UsePaymentsSubscriptions` | `IntegrationCheckoutGatewayEventsHandler` | Integration-checkout twin (not GMV product) |
| Commerce | `UseCommerceSubscriptions` | `GatewayPaymentCompletedIntegrationEventHandler` | Access: complete session, insert `Subscription` or `Order`, transaction log, `SubscriptionActivatedIntegrationEvent` / `OrderCompletedIntegrationEvent` / outbound `payment_link.paid` |
| Billing | `UseBillingSubscriptions` | `GatewayPaymentCompletedHandler` | GMV journal + `RCPT-`/`INV-` + PDF + `B2bTaxInvoiceRequestedIntegrationEvent` |
| Billing | same | `PlatformTopUpEventHandler` | Utility credits (`metadata.type == utility_credit_topup`) |
| Billing | same | `PlatformSaasFeeHandler` | Hub software fee (`platform_saas_fee`) |

Commerce’s handler **returns** unless `CommerceCheckoutMetadata.IsCommerceSubscriptionType(type)` or `type == "custom_payment_link"`. Billing’s GMV handler **returns** if `PlatformCheckoutTypes.IsPlatformCollected`. The partition is a string in `Metadata["type"]`. A misspelled type books access without a journal, or a journal without access, or Hub fee as creator GMV. That is not a type system. That is a seam.

Then Commerce, on a product session that is not `one_time`, publishes **another** event:

```csharp
await _eventBus.PublishAsync(new SubscriptionActivatedIntegrationEvent(
    subscription.OrganizationId,
    subscription.Id,
    subscription.ClientProfileId,
    subscription.ProductId,
    product.FulfillmentTargets.ToList(),
    true));
```

(`GatewayPaymentCompletedIntegrationEventHandler.OpenCheckout.cs`.) `FulfillmentTargets` is the old “grant in another app” list. 011 later says entitlement for a **second** Lazuar app is HTTP or a function if in-process — **not** an in-process event catalog talking to yourself (`NP-LAT-003`, `NP-XX-009`).

Billing, on B2B, publishes **yet another** event after the PDF:

```csharp
await _eventBus.PublishAsync(new B2bTaxInvoiceRequestedIntegrationEvent(
    @event.OrganizationId,
    booked.Id,
    booked.CustomerDocumentNumber ?? "",
    @event.GatewayTransactionId,
    grossRevenue,
    taxAmount,
    @event.Currency,
    correlation,
    ResolveLineDescription(@event)));
```

(`GatewayPaymentCompletedHandler.GenerateDocumentAsync`.) Lhdn consumes that. The customer already has a PDF. MyInvois may never see a `TaxDocument` (mapper skip). 008/009 named this as the legal lie. New Pay does not publish it.

PDF storage then publishes `DocumentPublishedIntegrationEvent`. Communications consumes it, looks up a `communications.MessageTemplates` row by **name** (`"Official Receipt"` / `"Tax Invoice"` / `"Credit Note"` / `"Quotation Ready"`), and if the row is missing **returns**. If present, it publishes `DispatchMessageIntegrationEvent` onto the Messaging bus. WhatsApp body is filled even for a receipt. 011 refuses WhatsApp dunning (`NP-XX-004`) and refuses Notify as a process (`NP-XX-019`).

**Count of hops for “card paid → buyer has access, books, and an email” in the old tree:** webhook HTTP → Payments outbox → bus → Commerce inbox + Billing inbox (parallel, different DbContexts) → (optional) Commerce outbox `SubscriptionActivated` → (optional) Billing outbox `B2bTaxInvoiceRequested` + `DocumentPublished` → Lhdn inbox + Communications inbox → Messaging inbox. None of those hops share a `BEGIN`. Workers run with empty ambient `TenantId`, so every consumer `IgnoreQueryFilters()`. 00-why-leave: “Workers needed `IgnoreQueryFilters` because the ‘module’ ran with an empty tenant.”

### 2.3 Events that were the product and then were not

| Type | Production `new` | Subscribers | What 00-why-leave said | New Pay |
|------|------------------|-------------|------------------------|---------|
| `InvoiceIssuedIntegrationEvent` | **Zero** in production `*.cs`. Tests only (`MyInvoisLoopTests`). | Billing `InvoiceIssuedHandler` (AR vs deferred revenue). Lhdn `InvoiceIssuedIntegrationEventHandler` (log-only: “MyInvois submit uses B2bTaxInvoiceRequested only.”). | “subscribed in two modules and constructed in none.” | **Do not create this type.** |
| `ManualPaymentRecordedIntegrationEvent` | None. Contract comment: “Parked. Nothing in production publishes or handles this type. Offline / clerk cash is `ManualSubscriberEnrolledIntegrationEvent`. Do not add a second cash journal here.” | None | “a contract that looked like cash settlement.” | **Do not create this type.** One cash writer. |
| `B2bTaxInvoiceRequestedIntegrationEvent` | Billing GMV handler on B2B | Lhdn submit path | Homemade LHDN | **Refuse** (`NP-XX-001`). |
| `DocumentPublishedIntegrationEvent` | `GenerateAndStoreDocumentCommandHandler` after R2 upload | Communications mail | Mail as a module hop | **Refuse the event.** Mail-outbox row in the same transaction. |
| `DispatchMessageIntegrationEvent` | Communications (and anyone) | Messaging | “any module can publish” | **Refuse.** |
| `SubscriptionActivatedIntegrationEvent` | Commerce payment handler | Commerce lifecycle handlers | In-process catalog talking to yourself | **Refuse as the grant mechanism.** If a second Lazuar app needs a grant later, HTTP (`NP-LAT-003`). |
| `GatewayRefundCompletedIntegrationEvent` published **from** `CommerceGatewayDisputeCreatedHandler` | Wave 3 did this; `e18edbe` stopped it | Billing booked a refund for a *held* chargeback | 008 P0-1 | **Never reuse a refund event as a dispute.** See §7. |

`InvoiceIssuedHandler` still books:

```csharp
entry.AddLine(AccountTypes.AssetAccountsReceivable, @event.Amount, ...);
entry.AddLine(AccountTypes.LiabilityDeferredRevenue, -@event.Amount, ...);
entry.ValidateBalanced();
```

It is subscribed (`UseBillingSubscriptions`). It has never run in production. Leaving a live consumer on a dead type is how the next editor “restores publishing” and creates a second cash story (AR that is never settled — 008: no payment-application entry, and `ManualPaymentRecorded` is dead). New Pay does not have AR in v1. A receipt is issued **because money moved**, not because an invoice was opened.

### 2.4 Dual-use columns that made deletion look like a product

`LedgerEntry.TaxInvoiceId` (this HEAD, comment at the field):

```csharp
/// <summary>
/// Legacy dual-use field (receipt #, LHDN UUID, consolidation ref). Prefer
/// <see cref="CustomerDocumentNumber"/> and <see cref="LhdnDocumentUuid"/> for new code.
/// Kept for back-compat with existing rows and LHDN correlation.
/// </summary>
public string? TaxInvoiceId { get; private set; }
```

00-why-leave: “`TaxInvoiceId` was a dumping ground because Billing, LHDN, and consolidation could not share one document model.” 008: `UpdateLhdnStatus` wrote the UUID into `TaxInvoiceId`; `MarkConsolidatedPending` overwrote it with `B2C-CONS-…`; after VALID the consolidation banner search went blank. This HEAD’s `MarkConsolidatedPending` no longer overwrites `TaxInvoiceId` (the comment on the method says so). The **field is still there**, still filled by `AssignB2cReceipt` (`TaxInvoiceId ??= receiptNumber`) and `AssignB2bInvoice`. New Pay does not have this column. The customer-facing number lives on the receipt header. A tax-provider UUID, **if it ever exists**, is a different column on a different row owned by the provider integration — not a dual-use on the journal.

Hub SaaS PDF sliced a Guid because that handler did not use the merchant numbering helper sitting one folder over (00-why-leave). `DocumentSeries.CustomerFacingNumber` exists specifically to refuse that:

```csharp
public static string CustomerFacingNumber(string? customerDocumentNumber, string? taxInvoiceId)
{
    if (!string.IsNullOrWhiteSpace(customerDocumentNumber))
        return customerDocumentNumber;

    if (!string.IsNullOrWhiteSpace(taxInvoiceId) && !LooksLikeGuid(taxInvoiceId))
        return taxInvoiceId;

    return "PENDING";
}
```

Steal the **function**. Do not steal the second argument. There is no `taxInvoiceId` on a v1 receipt.

### 2.5 “Financial truth” as a module README vs a function

Billing README still sells the module as the Core Domain for Financial Truth and forbids calculating cash from Commerce logs. The live dashboard (008 §10) labelled ledger **net revenue** “Net Cash in Bank” and computed MRR from Commerce snapshots. That is what a module wall does: the golden rule is a document; the page is a join of two APIs with three incompatible numbers.

New Pay’s truth is the **row the handler inserted**. Ops `GET /v1/journal/{id}` (name TBD) reads that row. There is no second “Commerce log is also kind of cash.” If we keep a payments log for PSP recon, it is **not** the source of GMV, tax payable, or “paid.”

### 2.6 Dual-write Hub (do not bring the second plane into this handler)

On the same `GatewayPaymentCompletedIntegrationEvent`, Billing also runs `PlatformSaasFeeHandler` and `PlatformTopUpEventHandler`. Those are **Lazuar collecting from the tenant** (Hub SKU, credit packs) using **system** keys, not the tenant’s BYOK GMV. `PlatformCheckoutTypes.IsPlatformCollected` is the partition. `Saas:Plan:AmountMyr = 0` in repo config; checkout 400s until an operator overlays a price. 008/009: do not sell a Hub subscription against default config.

New Pay is not Hub. This handler books **the merchant’s buyer payment**. It does not:

- mint Hub credits;
- activate `WorkspaceSaasSubscription`;
- allocate `SAAS-yyyy-#####` on a system org sequence;
- dual-write a row into old `apps/lazuar-api` tables “so ops still works during cutover.”

Cutover dual-run is [02-replace-old-cutover.md](./02-replace-old-cutover.md). This handler’s anti-goal is: **one writer per fact, in the new database.** If a migration backfills old ledger rows, that is 013-09, not a live dual-write from the webhook.

### 2.7 Setup counted as paid (the other lie on this event)

009: `ProcessGatewayWebhookCommandHandler` published `$0` sessions as `PAYMENT_COMPLETED`. Billing’s GMV handler **now** returns if `AmountPaid <= 0` (“$0 Stripe setup / 100% coupon vault is not GMV. Do not burn a RCPT number.”). Commerce’s payment handler still keys off `metadata.type` (`trial` is not `IsCommerceSubscriptionType`). Split-brain was: phantom RM 0 receipt, no subscription activation from that webhook — or the reverse, depending on the week.

New Pay: **setup / setup-intent is not paid** (`NP-GW-008`). The fulfillment handler is not invoked for `amount <= 0`. It does not mint `RCPT-`. It does not insert `ACTIVE`. Vaulting a PM is a different write (token columns on the subscription), not this story.

### 2.8 What “same handler” forbids, concretely

Do not:

- `IEventBus.PublishAsync(new GatewayPaymentCompletedIntegrationEvent(...))` as the way Pay talks to Pay.
- `AddBillingModule` / `UseBillingSubscriptions` / `BillingInboxConsumerJob`.
- A `Modules/` folder under `apps/lazuar-pay`.
- MediatR `IRequestHandler<GenerateAndStoreDocumentCommand>`.
- `IgnoreQueryFilters` because the worker has an empty tenant — there is no worker for this write.
- A second hop to One (`POST /tenants/{id}/members`, `authz/write`, SCIM) to “activate the buyer.”
- A Hub outbox “so lazuar-ops can list the payment.”

Do:

```text
verified webhook POST
  → FulfillPaidCheckout(...)   // one function, one transaction
  → 200 to the PSP
```

Retry of the same `(org_id, provider, event_id)` enters the function, sees the journal unique key, and **returns**. That is NP-GW-006 and NP-FUL-001 together. 012/09 already warned: the dogfood “webhook retry no-ops” is this path, not One’s `member.accepted`.

---

## 3. Minimum tables/rows in ONE Pay database

One Postgres for Pay. One schema is enough (call it `pay`, or `public` — do not invent `commerce` + `billing` + `crm` + `communications` + `lhdn`). `org_id` on every money row is a **copy of the One tenant UUID** ([012/06](../012-one-to-pay/06-tenant-org.md)). It is not a foreign key to a Pay `organizations` table (`NP-XX-014`).

This is a **minimum** for the handler in §4. Catalog (products/prices), BYOK gateway keys, and webhook ingress logs are owned by other 013 papers; they are named here only where the handler must read or write them. Do not copy `billing.LedgerEntries` column-for-column. Do not copy `CRM.ClientProfiles` TIN/ID/address as a legal e-invoice feature (`NP-XX-002`).

### 3.1 What the fixture is missing (so “paid” has somewhere to live)

Live `Lazuar.Pay.Checkouts.CheckoutSession` (`apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutSession.cs`):

```csharp
public sealed class CheckoutSession
{
    public required string Id { get; init; }
    public required string OrgId { get; init; }
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public required string Status { get; init; }
    public string? SuccessUrl { get; init; }
    public string? CancelUrl { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
```

`CheckoutEndpoints.Create` sets `Status = "open"` and never writes another status. TypeSpec `packages/pay-spec/main.tsp` `model CheckoutSession` is the same seven fields. Tests lock `status == "open"`.

**`status: "open"` is not a charge.** Paid must add columns (or a replacement row type — still one table). If an implementer only flips `status` to `"paid"` and leaves payer/product/seats/provider ids off the row, ops cannot show a receipt and a webhook retry cannot prove it is the same payment.

### 3.2 `checkouts` — the cash-register row

One row per hosted attempt. Create path (already sketched by the fixture) inserts `open`. The fulfillment handler is the **only** writer of `paid`.

| Column | Why |
|--------|-----|
| `id` | Public checkout id. Not a ledger number. Not a UUID printed as `RCPT`. |
| `org_id` | One tenant id. |
| `status` | `open` \| `paid` \| `expired` \| `canceled`. **Not** `COMPLETED` vs `CONFIRMED` vs `PAID` in three tables. |
| `amount` | Gross the PSP was asked to collect (inclusive of exclusive-SST-on-unit × seats). |
| `currency` | Start `MYR`. |
| `success_url` / `cancel_url` | Hosted-page redirects. Unchanged from the fixture. |
| `created_at` | |
| `expires_at` | Open sessions expire; a **late** paid webhook may still fulfill (old `CanFulfillFromPayment` allowed `EXPIRED` — steal that judgment, do not steal the name). |
| `idempotency_key` | Create-time, per org. Unique `(org_id, idempotency_key)` where not null. **Not** the webhook event id. |
| **Must add for paid** | |
| `product_id` / `price_id` | What was sold. Catalog tables are adjacent. |
| `interval` | `mo` \| `yr` \| `one_off`. Drives whether the handler inserts `subscriptions` or only marks the one-off complete. |
| `quantity` | Seats. `>= 1`. SST multiplies after unit tax. |
| `unit_net` / `unit_tax` / `tax_type` / `tax_rate_percent` | Frozen at create (or at pay if create did not yet know SST — but create **should** know; fail closed then, not at webhook). |
| `payer_id` | FK to `payers`. Old tree used `ClientProfileId` on the session **before** pay (CRM resolve at initiate). New tree: payer email/name on the hosted page; upsert payer in the **same** paid transaction (`NP-BUY-001`, `NP-BUY-002`). |
| `payer_email` / `payer_name` | Snapshot on the session even if `payers` is later anonymized. Receipt mail uses this snapshot. |
| `provider` | `stripe` \| `chip` \| … |
| `provider_checkout_ref` | Hop-2 session / bill id, if any. |
| `provider_payment_id` | PaymentIntent / CHIP purchase / Billplz bill that **paid**. Journal `reference_id`. |
| `provider_event_id` | The event that fulfilled. Webhook idempotency is `(org_id, provider, event_id)` — store it here or on `webhook_events`; the journal unique key must be stable. |
| `paid_at` | Realization time. |
| `subscription_id` | Null for one-off. Set in the same transaction as insert. |
| `journal_id` | The header this pay booked. |
| `receipt_id` | The Official Receipt header. |
| `setup_only` | If true, this session must **never** go `paid` (`NP-GW-008`). |

Do not add `is_b2b_required`, `lhdn_validation_status`, `tax_invoice_id`, `document_number` as `INV-`. Quote/proforma (`QT-`) is `NP-SOON-001`, not this handler.

Old `CheckoutSession.TryCompleteFromPayment` is the status machine to steal: only `OPEN` (and late `EXPIRED`) become paid; a second complete is a no-op. New code should use `paid`, not `COMPLETED` — “complete” was overloaded with custom quotes that never created a subscription (008: quote-only buyers could not open portal documents).

### 3.3 `subscriptions` — buyer access (seats)

This **is** access (`NP-FUL-002`). One membership is staff. A paid one-off does **not** require this row; the paid checkout + receipt is the completion.

| Column | Why |
|--------|-----|
| `id` | Buyer access id. Magic link later binds to this (`NP-BUY-003`), not to a Zitadel user. |
| `org_id` | |
| `payer_id` | |
| `checkout_id` | The first successful pay that created it. |
| `product_id` / `price_id` | |
| `status` | v1 S1: `active`. Do not invent `PAST_DUE` on this write (`NP-FUL-005` is a **later** renew rule: decline does not invent PAST_DUE on a healthy seat without a real failed charge). |
| `quantity` | Seats. `Math.Max(1, quantity)` as in `SubscriptionBillingAmount.Seats`. |
| `unit_amount` | **Net** unit snapshot (`HasUnitSnapshot` judgment). Gross is computed, not stored as the snapshot, or you double-count SST on renew. Old `UnitAmount` is net; `GrossBreakdown` adds tax. Steal that. |
| `billing_interval` | `mo` \| `yr`. |
| `current_period_end` / `next_billing_at` | Old `SubscriptionActivation.Start`: paid-through is the **next** bill, not the activation instant (`AdvanceFrom`). Steal that. |
| `vaulted_customer_id` / `vaulted_token_id` | Nullable. Stripe/CHIP if a real PM exists. Empty ⇒ reminder-only on renew (`NP-FUL-004`, wrap-rails). Do not treat presence of a setup session as a token. |
| `is_reminder_only` | Honest matrix: Billplz-class never silent debit. |
| `created_at` | |

Do **not** port in v1: `PendingQuantity`, dunning campaign snapshot, `HasOpenDispute` one-way latch, `CurrentRenewalCheckoutUrl`, `FulfillmentTargets`, `TRIALING` (trial is not the dogfood path). Renew is `NP-FUL-004` (V1, not S1). This paper only requires: **first successful pay inserts `active` with seats and a period.**

One-off: no `subscriptions` row. The paid `checkouts` row + `receipts` row **is** completion (`NP-FUL-001` “or completes one-off”). Do not recreate `Order` + `OrderCompletedIntegrationEvent` as a second aggregate unless a later paper proves a list UI needs it. A `kind` on checkout (`subscription` \| `one_off`) is enough.

### 3.4 Journal header + `journal_lines` — money truth

Steal the **shape** of `LedgerEntry` + `LedgerLine`, not the LHDN columns.

**Header (`journal_entries`):**

| Column | Why |
|--------|-----|
| `id` | Internal. Never printed as “No:”. |
| `org_id` | **In the unique key.** Old unique index was global `(ReferenceType, ReferenceId)` (008/009); this HEAD’s `HasEntryBeenProcessedAsync` already takes `organizationId`. New unique: `(org_id, reference_type, reference_id)`. |
| `timestamp` | Realization (`DateTime.UtcNow` is fine; display in MYT). |
| `reference_type` | `gateway_payment` for this handler. Later: `gateway_refund`. **Not** `invoice_issued`, `lhdn_cancellation`, `system_saas_fee`, `system_credit_topup`. |
| `reference_id` | Provider payment id (PaymentIntent / CHIP purchase id / Billplz bill id). Webhook retries use the same string. |
| `currency` | |
| `description` | Optional. Product name, not `"B2B sale"`. |
| `checkout_id` / `subscription_id` | Correlation without metadata dictionaries. |

**Lines (`journal_lines`):**

| Column | Why |
|--------|-----|
| `id` | |
| `journal_id` | Child of header. **No** `org_id` on the line is acceptable if **every** query joins the header (old `LedgerLine` had no org — 009 flagged raw `DbSet<LedgerLine>` as a footgun). Prefer copying `org_id` onto the line anyway so a mistaken query cannot leak. |
| `account` | `asset_cash` \| `expense_gateway_fee` \| `revenue_gross` \| `liability_tax_payable`. That is the v1 chart. Do not port AR, deferred, affiliate payable, recognized, software subscription, discount, commission. |
| `amount` | Signed. Same convention as the old writers: cash/fee **positive** on a sale; revenue/tax **negative** on a sale. Document the convention in one comment; `validate_balanced` does not know it. |
| `currency` | |
| `tax_type` | `02` or `06`. Default `06`. No MSIC `004`/`022` in v1 (those were MyInvois classification leftovers). |

**Balance guard to steal (this HEAD, not the 16 August one-liner):** `LedgerEntry.ValidateBalanced` now:

- throws if `_lines.Count == 0`;
- nets `Amount` **per `Currency`**;
- nets `BaseCurrencyAmount` **per `BaseCurrency`**.

```171:197:apps/lazuar-api/Modules/Billing/Domain/Aggregates/LedgerEntry.cs
    public void ValidateBalanced()
    {
        if (_lines.Count == 0)
        {
            throw new InvalidOperationException($"Ledger entry {Id} has no lines.");
        }

        foreach (var group in _lines.GroupBy(l => l.Currency, StringComparer.OrdinalIgnoreCase))
        {
            var net = group.Sum(l => l.Amount);
            if (net != 0)
            {
                throw new InvalidOperationException(
                    $"Ledger entry {Id} is unbalanced in {group.Key}. Net amount: {net}");
            }
        }
        // ... same for BaseCurrencyAmount / BaseCurrency
    }
```

Locked by `LedgerEntryBalanceTests` (`ValidateBalanced_EmptyLines_Throws`, `ValidateBalanced_NativeAmountMismatch_Throws`, `ValidateBalanced_BalancedMyrSale_Passes`). Steal **empty-lines-are-not-a-journal** and **per-currency net**. v1 is MYR-only: still run the guard. Wrong-but-cancelling accounts still pass — the handler owns that class of bug (comment on the old method). The happy-path composition:

```
ASSET_CASH          + net          // amount_paid - fee  (fee 0 if unknown)
EXPENSE_GATEWAY_FEE + fee          // only if fee > 0
REVENUE_GROSS       − (amount_paid − tax)
LIABILITY_TAX_PAYABLE − tax        // only if tax > 0
```

Holds iff `net + fee == amount_paid` and `tax` is a **slice** of `amount_paid`, not an add-on. If the adapter ever sends `net + fee != amount_paid`, **throw** (fail closed). Do not coerce. Billplz often has fee 0 by construction in the old webhook (`estimatedFeePercentage: 0`, `fixedFee: 0`) — then cash = amount paid. That is honest about **our** numbers (`NP-MON-002`: unknown ≠ 0). Do not invent a 2.5% Billplz fee to look like a payout file.

Idempotency: `HasEntryBeenProcessedAsync(org, gateway_payment, provider_payment_id)` **before** allocate. A retry after commit returns the existing header and does not increment `RCPT-`. A crash **after** increment and **before** commit must **roll the increment back** (see `document_sequences`).

Do not add `lhdn_validation_status`, `consolidation_status`, `lhdn_document_uuid`, `customer_type` B2B/B2C, `tax_invoice_id` to this header.

### 3.5 `receipts` — Official Receipt header (commercial, not tax)

The journal is the books. The receipt is the **customer-facing paper**. They are written together. They are not the same row: a journal can exist without a PDF; a PDF without a journal is a lie. Old `GenerateAndStoreDocumentCommandHandler` built the PDF **after** `SaveChanges` and published mail from that. 009 B05-L22: if PDF throws, inbox retry sees `HasEntryBeenProcessed` and **never generates the PDF**. Steal the later-patch idea (retry may regenerate from the existing header) and make it local: receipt **number** is in the transaction; PDF bytes can be filled after commit **without allocating a new number**.

| Column | Why |
|--------|-----|
| `id` | Internal. |
| `org_id` | |
| `journal_id` | 1:1 with the sale journal for v1. |
| `checkout_id` | |
| `number` | `RCPT-2026-00001` or the literal `PENDING` only if allocation failed — and allocation failure **fails the transaction**. Do not persist a UUID here. Do not persist `null` and hope the UI prints `PENDING` from a Guid. |
| `title` | `Official Receipt`. Check constraint or enum. **Not** `Tax Invoice`. **Not** `Invoice` (the old tree’s later honesty patch still used `"Invoice"` for B2B — see §6). |
| `issued_at` | Store UTC; the **series year** is MYT. |
| `payer_name` / `payer_email` | Snapshot. |
| `gross` / `tax` / `currency` | Display. Must match the journal split. |
| `disclaimer` | Frozen string: payment receipt, not a validated MyInvois tax invoice. |
| `pdf` | Nullable bytes, or object-store key. **Not required to commit.** Ops GET may render on the fly from this header + lines for S1. |
| `valid_badge` | **Do not add this column.** |

`document_sequences` (supporting, required for the number):

| Column | Why |
|--------|-----|
| `org_id` | |
| `prefix` | `RCPT-2026` (MYT year baked in). Unique `(org_id, prefix)`. |
| `current_value` | Increment in the **same** transaction as the journal insert. |

Steal `DocumentSequence.Increment` and this HEAD’s `GenerateNextSequenceNumberCommandHandler` (same DbContext, comment: “Callers wrap both in `IBillingTransactional` so a failed persist rolls the increment back”). **Do not** steal the 16 August Dapper `CreateConnection()` path 008/009 quoted — that comment claimed gap-free rollbacks and did the opposite. Gaps after a **committed** increment are acceptable for commercial numbers; claiming gap-free is not. Rolling back with the journal is the judgment.

v1 series: **`RCPT` only.** Do not allocate `INV-`, `CN-`, `QT-`, `SAAS-` in this handler. `INV-` is how the old tree titled a tax invoice before VALID. `CN-` is refunds (§7). `QT-` is soon.

### 3.6 `payers` — small buyer profile inside Pay

011: “Keep a small payer profile inside Pay (old CRM/client-profile job, stripped).” `NP-BUY-002`. Not a Zitadel human (`NP-XX-013`). Not `ClientProfileEntity.Tin` / `IdType` / `IdValue` as a legal e-invoice feature (`NP-XX-002`).

Old `ClientProfileEntity` (`Modules/CRM/Domain/ClientProfileEntity.cs`):

```csharp
public Guid OrganizationId { get; set; }
public Guid? GlobalUserId { get; set; }   // do not port — that was Hub identity
public string FullName { get; set; }
public string Email { get; set; }
public string Phone { get; set; }
public string? CompanyName { get; set; }
public string? Tin { get; set; }          // later / provider; not v1 legal
public string? IdType { get; set; }       // refuse as checkout legal feature
public string? IdValue { get; set; }      // the quote B2B arity bomb lived here
public BillingAddress? Address { get; set; }
public bool ConsentedToMarketing { get; set; }
```

**v1 columns:**

| Column | Why |
|--------|-----|
| `id` | |
| `org_id` | |
| `email` | Unique per org (normalized lower). Magic link later (`NP-BUY-003`). Receipt mail. |
| `name` | |
| `phone` | Optional. |
| `created_at` / `updated_at` | |

Optional later, **not** this handler’s job: company name as a display string (not BRN), marketing consent. Do not add `global_user_id`. Do not add TIN/ID pair until a tax **provider** integration exists, and even then it is not “title this receipt Tax Invoice.”

Upsert key: `(org_id, email)`. First pay creates. Second product pay by the same mailbox updates name if empty (old CRM “enrich only empty fields” — steal the *caution*, not the permanence of a poisoned `IdValue`). Do not invent a Hub user.

### 3.7 Rows this handler also writes (same database, still not extra modules)

These are not the five nouns in the section title. They are required so NP-AUD / NP-MAIL / NP-GW-006 are not “later events.”

| Table | Row on first successful pay |
|-------|-----------------------------|
| `webhook_events` | `(org_id, provider, event_id)` processed. Unique. Ingress paper owns the verify; this handler owns the “already fulfilled” read. |
| `audit_events` | `action = charge.paid` (name TBD), `entity_type = checkout`, `entity_id = checkout.id`, actor = `system:webhook` or empty. **Inserted in the same transaction.** No swallow. |
| `mail_outbox` | `to = payer_email`, `kind = receipt`, `receipt_id`, `status = pending`. Send **after** commit from this process. |
| `org_settings` (or `merchant_sst`) | **Read**, not written. SST registration number / `sst_registered` tri-state. Fail closed if missing. Do not hide this on `TenantBillingProfile` next to TIN/MSIC/MyInvois cert. |

Do **not** add: `outbox_messages` for in-process domain events, `inbox_messages`, `tax_documents`, `lhdn_tenant_config`, `credit_wallets`, `workspace_saas_subscriptions`.

### 3.8 What a first successful pay looks like as rows (one org, one buyer, 1 seat, SST 8%, fee unknown)

Assume unit net RM 100, qty 1, merchant SST-registered, Stripe fee unknown ⇒ 0.

| Table | Row |
|-------|-----|
| `checkouts` | `status=paid`, `amount=108.00`, `quantity=1`, `unit_net=100`, `unit_tax=8`, `tax_type=02`, `provider_payment_id=pi_…`, `paid_at=…`, FKs filled |
| `subscriptions` | `status=active`, `quantity=1`, `unit_amount=100`, `next_billing_at=+1mo` |
| `journal_entries` | `reference_type=gateway_payment`, `reference_id=pi_…` |
| `journal_lines` | cash +108; revenue −100; tax −8 |
| `receipts` | `number=RCPT-2026-00001`, `title=Official Receipt`, `gross=108`, `tax=8` |
| `document_sequences` | `prefix=RCPT-2026`, `current_value=1` |
| `payers` | email/name from the hosted page |
| `audit_events` | charge.paid |
| `mail_outbox` | receipt pending |

Qty 3, unit 100, SST 8%: gross **324** (`100+8)×3`, not tax(300×8%). Lines: cash +324; revenue −300; tax −24. See §5.

### 3.9 Tables this paper refuses even if they already exist in the museum

`billing.LedgerEntries.TaxInvoiceId`, `lhdn.TaxDocuments`, `communications.MessageTemplates` as the gate on whether a receipt email exists, `one.AuditEvents` written with `IAuditRecorder` from Commerce (cross-module audit that swallows), `commerce.Orders` as a parallel cash story, `billing.WorkspaceSaasSubscriptions`, `billing.TenantCreditBalances`. Cutover may **read** them ([09-data-migration.md](./09-data-migration.md)). This handler does not **write** them.

---

## 4. Handler sequence on webhook success (pseudocode level, one function)

Ingress (signature, empty body → 400, tenant from URL vs metadata) is [06-money-rails.md](./06-money-rails.md). This section starts **after** `verified == true` and `event_type` is a real payment success (not setup, not dispute, not refund). `NP-GW-008`: amount ≤ 0 returns without this function.

Name is illustrative. The point is **one function**, not three MediatR handlers.

```text
FulfillPaidCheckout(org_id, provider, event_id, payment_id, amount_paid, currency, fee_or_unknown, checkout_id):

  // 0. Idempotency of the PSP event (NP-GW-006)
  if webhook_events contains (org_id, provider, event_id):
      return 200  // already fulfilled or already decided; no new journal

  // 1. Load the open (or late-expired) checkout in this org
  session = checkouts where id=checkout_id and org_id=org_id
  if session is null or session.org_id != org_id: reject (do not book)
  if session.setup_only or amount_paid <= 0: return 200 without booking
  if session.status == paid:
      // same payment_id → 200; different payment_id → alarm, do not second-journal
      return 200
  if session.status not in (open, expired): reject

  // 2. SST (NP-MON-003 / 004) — fail closed BEFORE begin if unknown
  sst = ResolveSstOrThrow(org_id, session.product)   // see §5
  expected_gross = sst.unit_gross * seats
  if amount_paid != expected_gross:
      throw  // do not silently book the wrong split

  fee = fee_or_unknown   // null → 0 line omitted (NP-MON-002)
  net_cash = amount_paid - fee
  tax = sst.unit_tax * seats
  revenue = amount_paid - tax
  if net_cash + fee != amount_paid: throw

  BEGIN
      // 3. Access (NP-FUL-001 / 002)
      session.status = paid
      session.paid_at = now
      session.provider_payment_id = payment_id
      session.provider_event_id = event_id
      payer = upsert payers (org_id, email) with name
      session.payer_id = payer.id

      if session.interval in (mo, yr):
          sub = insert subscriptions (active, quantity, unit_amount=unit_net,
                 period_end = AdvanceFrom(now, interval), vault ids if real PM)
          session.subscription_id = sub.id
      else:
          // one-off complete = paid session; no subscriptions row
          pass

      // 4. Journal (NP-MON-001)
      if journal_entries contains (org_id, gateway_payment, payment_id):
          ROLLBACK; return 200
      je = insert journal_entries (...)
      insert line asset_cash +net_cash
      if fee > 0: insert line expense_gateway_fee +fee
      insert line revenue_gross −revenue
      if tax > 0: insert line liability_tax_payable −tax
      ValidateBalanced(je)  // empty lines throw; per-currency net 0

      // 5. Receipt number in the SAME transaction (NP-DOC-001 / 002)
      prefix = "RCPT-" + MalaysiaYear(now)     // MYT, not UTC
      seq = lock document_sequences (org_id, prefix); increment
      number = prefix + "-" + seq.current_value padded 5
      // never write je.id (a UUID) into receipts.number
      rcpt = insert receipts (title="Official Receipt", number, disclaimer, ...)
      session.journal_id = je.id
      session.receipt_id = rcpt.id

      // 6. Audit + mail outbox (NP-AUD-001, NP-MAIL-001) — same BEGIN
      insert audit_events (org_id, action="charge.paid", entity=checkout, entity_id=session.id)
      insert mail_outbox (kind=receipt, to=session.payer_email, receipt_id=rcpt.id, pending)

      insert webhook_events (org_id, provider, event_id, payment_id)

  COMMIT

  // 7. After commit: send mail in this process (not a Notify service)
  TrySendReceiptEmail(mail_outbox row)   // failure leaves pending; a loop retries
  return 200
```

### 4.1 Ordering constraints (why this order)

1. **SST before `BEGIN`.** If registration is unknown, throwing before open means the PSP will retry. That is the correct fail closed. Booking RM 100 when the unit should have been RM 108 is the undercharge 167 forbade.
2. **Access and journal in one `BEGIN`.** Old Commerce could `SaveChanges` a subscription while Billing’s inbox was still empty (or dead-lettered on `ValidateBalanced`). Paid-but-no-receipt and receipt-but-no-seat are the races 011/13 named.
3. **Sequence increment inside `BEGIN`.** This HEAD already moved `GenerateNextSequenceNumberCommandHandler` onto `BillingDbContext` for that reason. Copy the *property*, not the MediatR command.
4. **Audit insert without try/catch swallow.** Old `IAuditRecorder`: “Implementations must never throw to callers.” Old `AuditRecorder.RecordAsync` catches, logs, and returns. Old `RecordRefundCommandHandler` calls it **after** `SaveChanges`. That is NP-XX-019 in miniature: the trail is optional. New rule: if audit insert fails, the **charge** rolls back. A down audit table is a Pay outage, not a silent gap. That is what “same transaction” means (011/07).
5. **Mail outbox in `BEGIN`, send after `COMMIT`.** If send is in the transaction, a down SMTP blocks capture. If send is a `DocumentPublished` event, you have Communications again. Outbox row in the transaction + in-process sender after commit is the 011/13 sentence: “Charge, ledger, receipt number, audit row, ‘enqueue receipt email’ can share `BEGIN`/`COMMIT`.” Enqueue ≠ SES round-trip.
6. **Return 200 only after `COMMIT`.** Returning 200 then booking is how you double-journal on retry. Returning 500 after commit is how you replay a fulfilled event; the unique keys make that a no-op — still return 200 on the replay so the PSP stops.

### 4.2 What the function does not call

- One HTTP (`/me`, `authz/check`, members, apps). Staff chrome may lag. Money is true (`NP-FUL-002`, 011/07 rule 2).
- Stripe Billing `subscription.updated` (`NP-XX-012`).
- `SubmitTaxDocumentCommand`, `JsonUblDocumentSigner`, `B2cConsolidationJob`.
- `ICommerceDocumentLookup.GetCustomerForDocumentAsync` (transaction-log short-circuit that stripped TIN — and we are not putting TIN on the PDF anyway).
- `IOneQueryService.GetWorkspaceByIdAsync` as a requirement to mint `RCPT-`. Seller display name can be cached org settings; it must not be a cross-process call inside `BEGIN`.

### 4.3 Correlation without metadata dictionaries

Old handlers parsed `metadata["subscription_id"]`, `["type"]`, `["sst_tax_amount"]`, `["is_b2b_required"]`, `["tenant_id"]`. The fulfillment function loads the **checkout row** it created at hop-1. SST amounts live on that row. `org_id` is a column. If the PSP metadata tenant disagrees with the URL tenant, ingress rejects **before** this function (already a Payments handler rule). Do not re-parse SST from metadata as the SoT; metadata was a workaround because Billing could not join Commerce. You can join.

`StampSstMetadata` is judgment for **adapters that cannot take a tax line** (Billplz `TaxAmount=0`). If hop-2 still needs a string bag for the PSP, copy `sst_tax_amount` / `sst_tax_type` onto the **PSP** metadata as a debug aid. The journal reads the checkout row.

### 4.4 Replay / concurrency

| Race | Outcome |
|------|---------|
| PSP retries the same `event_id` after commit | `webhook_events` hit → 200, zero new lines |
| PSP retries after `BEGIN` but before `COMMIT` | unique `(org_id, gateway_payment, payment_id)` or row lock on checkout; one winner |
| Two event ids for one PaymentIntent | Second insert hits journal unique key → 200 no-op (treat as already booked). Do not allocate a second `RCPT-`. |
| Expiry job marks `expired` while PSP is in flight | Allow fulfill from expired (old `CanFulfillFromPayment`). Do not allow fulfill from `canceled`. |

Do not use the checkout **create** `Idempotency-Key` as the journal key. Two creates with two keys can still be one PaymentIntent if the buyer double-clicked hop-2; the journal key is the **provider payment id**.

---

## 5. SST: copy rules from `SstTaxMath` with file path; fail closed

### 5.1 Where the judgment lives (old tree)

| Helper | Path | Role |
|--------|------|------|
| `SstTaxMath` | `apps/lazuar-api/Modules/Commerce/Application/SstTaxMath.cs` | Exclusive tax on **one net amount** (a unit, or a quote line). |
| `SubscriptionBillingAmount.GrossBreakdown` | `apps/lazuar-api/Modules/Commerce/Application/SubscriptionBillingAmount.cs` | Unit tax then × seats. |
| `SubscriptionBillingAmount.MerchantHasSstAsync` | same file | Fail closed if the billing port is missing. |
| `Product.SetSst` | `apps/lazuar-api/Modules/Commerce/Domain/Aggregates/Product.cs` | Catalog allows only `06` / `02`. |
| `TenantBillingProfile.SstRegistrationNumber` | `apps/lazuar-api/Modules/Billing/Domain/Aggregates/TenantBillingProfile.cs` | Merchant SST id. Empty ⇒ not registered. |
| `GatewayPaymentCompletedHandler.ResolveTaxAmount` | `apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/GatewayPaymentCompletedHandler.cs` | Split tax out of `AmountPaid` using event then `sst_tax_amount` metadata. |

Lhdn does **not** own SST math. 008 §8: SST is Commerce checkout math stamped onto metadata so Billing can split `LIABILITY_TAX_PAYABLE`. Do not put SST inside a tax-document module in the new host.

### 5.2 `SstTaxMath.Compute` — copy these rules

```1:29:apps/lazuar-api/Modules/Commerce/Application/SstTaxMath.cs
public static class SstTaxMath
{
    public const string NotApplicable = "06";
    public const string ServiceTax = "02";

    /// <summary>
    /// Round exclusive SST on <paramref name="netAmount"/> (one unit, or a custom-quote line).
    /// Callers that have seats must pass the unit net, not the line net — see GrossBreakdown.
    /// </summary>
    public static (string TaxType, decimal TaxAmount) Compute(
        string? requestedType,
        decimal ratePercent,
        decimal netAmount,
        bool merchantHasSstRegistration)
    {
        if (!merchantHasSstRegistration
            || !string.Equals(requestedType, ServiceTax, StringComparison.OrdinalIgnoreCase)
            || ratePercent <= 0
            || netAmount <= 0)
        {
            return (NotApplicable, 0m);
        }

        var tax = Math.Round(netAmount * ratePercent / 100m, 2, MidpointRounding.AwayFromZero);
        return (ServiceTax, tax);
    }
}
```

Rules to re-implement in `apps/lazuar-pay` (new type name is fine; the **numbers** are not negotiable):

1. Tax type `02` = service tax. Type `06` = not applicable. No type `01` sales tax, no tourism tax, no export `E` in v1 (008: LP-119 reserved).
2. Exclusive: `tax = round(unit_net * rate / 100, 2, AwayFromZero)`. Not inclusive at this step. Not Stripe Tax. Not “add 8% to the PSP amount and also book 8% as liability” (that would double).
3. If the merchant is **not** SST-registered, requested `02` **coerces to `06` / 0**. Locked by `SstTaxMathTests.Product02_WithoutSstId_Coerces06`.
4. If requested type is `06`, rate is ignored. Locked by `Product06_NoTax`.
5. `ratePercent <= 0` or `netAmount <= 0` ⇒ no tax.
6. Callers with seats **must not** pass `unit_net * seats` into `Compute`. The comment points at `GrossBreakdown`. That comment **is** the product.

### 5.3 Seats: `GrossBreakdown` — copy these rules

```44:61:apps/lazuar-api/Modules/Commerce/Application/SubscriptionBillingAmount.cs
    /// Exclusive SST is rounded on the unit, then × seats (B01-C12 / B02-C20).
    /// Hop-2 adapters multiply Amount × Quantity, so the charged line is
    /// unitGross * seats. Do not switch this helper to tax(unitNet * seats)
    /// without also changing the adapter contract — that mix is a sen off on odd prices.
    public static Breakdown GrossBreakdown(
        decimal unitNet,
        int seats,
        string? sstTaxType,
        decimal sstRatePercent,
        bool merchantHasSst)
    {
        seats = Math.Max(1, seats);
        var (taxType, unitTax) = SstTaxMath.Compute(sstTaxType, sstRatePercent, unitNet, merchantHasSst);
        var unitGross = unitNet + unitTax;
        return new Breakdown(unitNet, unitTax, unitGross, seats, unitGross * seats, taxType);
    }
```

`LineTax = UnitTax * Seats`. Default rate constant: `DefaultServiceTaxRatePercent = 8m`.

Locked by `SubscriptionBillingAmountTests.GrossBreakdown_PerUnitThenSeats_PinsSenSplit`:

| Case | Unit net | Seats | Unit tax (8%) | Line tax | Gross |
|------|----------|-------|---------------|----------|-------|
| Odd sen | 10.03 | 3 | 0.80 (0.8024 → 0.80) | 2.40 | 32.49 |
| Line-level would have been | tax(30.09 × 8%) = 2.41 | | | | 32.50 |
| 33.33 × 3 | 33.33 | 3 | 2.67 | 8.01 | 108.00 |
| Line-level would have been | tax(99.99 × 8%) = 8.00 | | | | |

**The hop-2 SSoT is unit then × seats.** A later “simplification” to tax(line net) is a **different product** and will disagree with Stripe/CHIP `Amount × Quantity` by a sen. Do not “fix” it without changing the adapter contract in the same commit.

Also locked: `Gross_SstRegistered_Unit100_Rate8_Is108` (108), `Gross_SstRegistered_Qty3_Is324` (324), `Gross_NoSst_Is100` (empty SST number ⇒ 100).

`TaxFromInclusiveGross` exists for **clerk/offline** cash that was collected inclusive. S1 dogfood is PSP checkout of exclusive-gross. Do not use inclusive extraction on the webhook path (you would round twice). Offline mark-paid is not S1 (`NP-SOON` adjacent). When it arrives, steal this helper so the journal still splits `LIABILITY_TAX_PAYABLE` without changing cash collected.

### 5.4 Fail closed if SST registration is **unknown**

`MerchantHasSstAsync`:

```121:131:apps/lazuar-api/Modules/Commerce/Application/SubscriptionBillingAmount.cs
    public static async Task<bool> MerchantHasSstAsync(IBillingQueryService? billing, Guid organizationId)
    {
        if (billing == null)
        {
            throw new InvalidOperationException(
                "IBillingQueryService is required to decide SST; refusing to undercharge.");
        }

        var profile = await billing.GetBillingProfileAsync(organizationId);
        return !string.IsNullOrWhiteSpace(profile?.Sst_registration_number);
    }
```

Locked by `MerchantHasSst_Null_Billing_Throws` (`*refusing to undercharge*`). Issue 167 (`49606466`) is the commit that replaced “null billing ⇒ false ⇒ undercharge.” 010-failed-tests/01 and /02 are the test fixtures that still constructed `InitiateCheckoutCommandHandler` without the billing port and then failed for the **right** reason. **Do not revert 167.** Steal the throw.

**Tri-state for new Pay** (tighten the old boolean):

| State | Meaning | Charge |
|-------|---------|--------|
| **Registered** | Org settings has a non-blank SST registration number (or an explicit `sst_registered = true` that you only set when the number is present). | Exclusive `02` if the product is `02` and rate > 0. |
| **Not registered** | Explicit `sst_registered = false` (merchant told us). | `06` / 0. `SstTaxMath` coerce. |
| **Unknown** | Settings row missing, column null with no default, query failed, code path forgot to load settings, “we’ll look it up later.” | **Throw. Do not book. Do not undercharge.** PSP retry is acceptable. |

The old boolean treated **missing profile** as not registered (`profile?.Sst_registration_number` whitespace ⇒ `false`). That is **undercharge** if the merchant is SST-registered and never opened Legal & Billing. 011 NP-MON-004 is stricter than 167. New org settings should not be creatable in a state that is silent-null. Onboarding: pick registered (and enter the number) or not registered **before** the first live charge. If the row does not exist, unknown → throw.

Do not fail closed by charging SST on everyone. That is overcharge. Fail closed means **stop**, not **guess 8%**.

Do not skip SST because “Lhdn is not configured.” SST is a **commercial** split. MyInvois is a later provider. 008: quotes and offline mark-paid skipped `SstTaxMath` and understated tax — do not port those writers as the S1 path.

### 5.5 Product flag vs merchant flag

`Product.SetSst` only allows `06` or `02`. A registered merchant can still sell a non-taxable product (`06`). Then `Compute` returns 0 even though `merchantHasSstRegistration` is true (`Product06_NoTax`). Steal that: **both** flags must be true for a tax line.

Ops UI (013-04) should not show a rate picker if the merchant is not registered (old `ProductForm` hid SST until Legal profile had a number). If the picker is hidden, persist `06`. Do not persist `02` with rate 8 and then “forget” at pay.

### 5.6 Where SST is decided in the new loop

**At checkout create / hosted-page quote**, not only in the webhook. The amount sent to Stripe/CHIP **is** `unitGross * seats`. If create undercharges, the webhook cannot invent tax without disagreeing with the PSP capture.

Webhook handler **recomputes** from stored `unit_net`, `seats`, org SST state, product type, and **throws if `amount_paid` ≠ expected gross**. That is how a settings change between create and pay does not silently book the wrong liability. If the merchant **registered SST** after the session was created at 100, fail closed (do not capture 100 as if it were 108). Operator retries a new checkout.

Renew (V1, `NP-FUL-004`): recompute from snapshot `unit_amount` (net) × current seats × current SST state. 009 named the old bug: hop-1 stamped `sst_tax_*` metadata; **every later charge did not** (`BillingEngineJob`, `RenewalCheckoutIssuer`, off-session Stripe metadata). Billing then booked SST-inclusive cash as `REVENUE_GROSS` with no `LIABILITY_TAX_PAYABLE`. Steal the warning: **every** charge path calls the same `GrossBreakdown`. There is no “first charge metadata, later charge amount-only.”

### 5.7 Journal split (so tax is not profit)

`ResolveTaxAmount` on the old GMV handler: prefer `event.TaxAmount` if > 0, else parse `metadata["sst_tax_amount"]`, else 0. We do not run Stripe Tax; Stripe `TaxAmount` is usually 0. The metadata workaround exists because of the module wall.

New handler: `tax = session.unit_tax * session.quantity` (or `LineTax(breakdown)`). `sst_tax_type == "02"` and `tax > 0` ⇒ line code `02`. Else `06` and **no** tax line (do not insert a 0 tax line).

Happy path 108 / 8 tax / 0 fee (unknown):

| Account | Amount |
|---------|--------|
| `asset_cash` | +108 |
| `revenue_gross` | −100 |
| `liability_tax_payable` | −8 |
| Net | 0 |

Happy path 108 / 8 tax / 3 fee (PSP actually sent 3):

| Account | Amount |
|---------|--------|
| `asset_cash` | +105 |
| `expense_gateway_fee` | +3 |
| `revenue_gross` | −100 |
| `liability_tax_payable` | −8 |
| Net | 0 |

That composition is what `LedgerBalanceMatrixTests.Payment_PostsBalancedSale_AndIsIdempotent` locked in the museum. Re-lock it in `apps/lazuar-pay` tests. Do not label the sum of revenue “cash in bank.”

---

## 6. Receipt: `RCPT-` series, MYT year, title honesty; merchant can open in ops (`NP-DOC-005`)

### 6.1 Title

011/01:

> Official Receipt / payment receipt (`RCPT-…`). Number is never a UUID; missing number is `PENDING`.  
> Do not title it Tax Invoice. Do not print MyInvois VALID.

`NP-DOC-003` / `NP-XX-003` / 03-first-slice fail lock: “Receipt titled Tax Invoice or numbered with a UUID.”

The old GMV handler on this HEAD still branches:

- B2C → `DocumentSeries.ReceiptPrefix()` → `AssignB2cReceipt` → PDF `"Official Receipt"`.
- B2B → `DocumentSeries.InvoicePrefix()` → `AssignB2bInvoice` → PDF `"Invoice"` (was `"Tax Invoice"` on the 16 August eval; factory now adds “This invoice is pending MyInvois validation…”).

That branch **is the product we are not shipping.** v1 has no B2B e-invoice. Every cleared buyer payment gets **Official Receipt**. There is no `INV-` series in this handler. There is no “pending MyInvois” subtitle, because there is no MyInvois.

Do not “meet in the middle” with H1 `Invoice`. 009 B06-D02 treated `DocumentType == "Tax Invoice"` as the spec. A later patch to `"Invoice"` is still a tax-shaped word on a commercial PDF. Malaysian buyers and ops staff will file it as a tax invoice. **Official Receipt.**

Steal the disclaimer strings, rewritten without promising a future VALID:

Old `InvoiceDocumentFactory.OfficialReceiptDisclaimer`:

```text
This Official Receipt confirms payment. It is not a validated MyInvois tax invoice.
```

Old `BaseInvoiceDocument` footer:

```text
Payment receipt. Not an LHDN e-invoice.
```

Keep both. Do not add a QR. Do not add a UUID field. Do not print the word `VALID`. Do not print `NOT REQUIRED` / `B2C_RECEIPT` / `NEEDS_BUYER_TIN` — those are LHDN lifecycle badges on a commercial list (008 §4.5). Ops shows **Official Receipt** + `RCPT-` + amount + date.

### 6.2 Number series — `RCPT-`, year in **MYT**

`DocumentSeries` on this HEAD:

```16:20:apps/lazuar-api/Modules/Billing/Contracts/DocumentSeries.cs
    public static string Prefix(string series, DateTime? utcNow = null)
    {
        var myt = MalaysiaTime.ToMyt(utcNow ?? DateTime.UtcNow);
        return $"{series}-{myt:yyyy}";
    }
```

`MalaysiaTime` (`apps/lazuar-api/Modules/Billing/Contracts/MalaysiaTime.cs`):

- Linux: `TimeZoneInfo.FindSystemTimeZoneById("Asia/Kuala_Lumpur")`.
- Windows fallback: `"Singapore Standard Time"`.
- `ToMyt`: unspecified treated as UTC, then convert.

Locked by `DocumentSeriesTests.Prefix_UsesMalaysiaYear_OnUtcNewYearsEve`: `2025-12-31 18:00 UTC` = `2026-01-01 02:00 MYT` ⇒ prefix `RCPT-2026`, **not** `RCPT-2025`. 008/009 recorded the UTC-year bug (`Prefix` used `DateTime.UtcNow`); 011/09 lists “document series years in MYT” as steal-as-notes. The live helper is the note. Copy `MalaysiaTime` (or an equivalent) into the Pay host. Do not depend on the machine’s local zone. Do not use `+08:00` as a naked offset without the IANA id (DST is not a Malaysia issue; the id is still the honest one).

Format: `{prefix}-{value:D5}` ⇒ `RCPT-2026-00001`. Steal `GenerateNextSequenceNumberCommandHandler`’s format string. Allocation is per `(org_id, prefix)`. Org A’s `00001` is not org B’s.

`CustomerFacingNumber`: if the stored number is empty and the only other id is a Guid, print `PENDING`. Locked by `CustomerFacingNumber_NeverUsesRawUuid`. In new Pay the insert should **not** commit a receipt row without `RCPT-…`. `PENDING` is a **display** fallback for a bug, not a happy path. Never `ToString("N")` the journal id onto the PDF “No:”. 00-why-leave: “Hub SaaS PDF sliced a Guid because that handler did not use the merchant numbering helper sitting one folder over.”

v1 does not need gapless numbers. Do not comment that the upsert “prevents sequence gaps during rollbacks” unless the increment is in the same transaction (this HEAD’s comment on the handler is the honest one: same DbContext, failed persist rolls back).

### 6.3 What the merchant opens in ops (`NP-DOC-005`)

Old door: `GET /admin/billing/ledger/{id}/document` (`AdminLedgerEndpoints.cs`) — OrgAdmin, JSON `{ url }` R2 presign for `vault/{tenant}/documents/{ledgerEntryId}.pdf`. Public HMAC: `GET /public/billing/{tenantSlug}/documents/{ledgerEntryId}?sig&exp`.

New door: Pay `/v1`, first client = `lazuar-pay-merchant` (`:5178`), not `lazuar-ops` (`:3003`), not `lazuar-admin` (`:5173`). 011/03 step 12: “Merchant sees the payment and receipt in ops.” `NP-API-004`: merchant ops is a client of `/v1`. No back-door table reads.

Minimum HTTP (names illustrative):

| Method | Path | Who | Returns |
|--------|------|-----|---------|
| `GET` | `/v1/orgs/{orgId}/payments` | One JWT / `lzr_sk_`, `authz/check member` | Paid checkouts + `receipt_number` + amount + payer email |
| `GET` | `/v1/orgs/{orgId}/receipts/{receiptId}` | same | Header: title Official Receipt, `RCPT-…`, amounts, disclaimer. Optional PDF bytes or a same-origin download URL **on Pay**. |

VIEWER cannot charge / refund is `NP-ONE-021` (012/07 honesty gap: One has no VIEWER role; member vs admin). This paper only requires: **the row exists and a member can GET it.** Do not block S1 on a PDF renderer. JSON of the receipt header is enough to “open the receipt in ops.” PDF can be the same GET with `Accept: application/pdf` rendered from the header + journal lines (QuestPDF is judgment; `InvoiceDocumentFactory` + `ICommerceDocumentLookup` + `IOneQueryService` is not).

Do not require R2/`lazuar-vault-test` to commit a payment. Object storage is a later seam (011/13: Media is not this binary). Bytes on the row or render-on-GET are both legal for S1.

Buyer download is `NP-BUY-005` (V1). S1 dogfood is **merchant** sees it. Buyer magic link is not this paper’s implement target; the receipt row must still exist so that later GET has a thing to sign.

### 6.4 Honesty locks on the PDF / JSON (when you do render)

| Print | v1 |
|-------|----|
| H1 | Official Receipt |
| No: | `RCPT-2026-#####` |
| If number missing | `PENDING` — and treat as a bug |
| UUID / LongId / QR | **Absent** |
| Word VALID / INVALID / SUBMITTED | **Absent** |
| “Tax Invoice” | **Absent** |
| SST line | Only if `tax_type=02` and tax > 0; label `SST:` |
| Buyer | Name + email from the payer snapshot. No fake TIN. No stub `C1234567890`. |
| Seller | Org display name from settings. TIN optional stationery — not a MyInvois supplier card. |
| Footer | Payment receipt. Not an LHDN e-invoice. |

008 §4.4: transaction-log-first lookup printed the person name and omitted company/TIN **on a document titled Tax Invoice**. We are not titling Tax Invoice, and we are not collecting TIN as a legal feature. Print what we actually have.

### 6.5 `AssignB2cReceipt` side effects we do **not** steal

Old `AssignB2cReceipt` also set `LhdnValidationStatus = B2C_RECEIPT` and `ConsolidationStatus = PENDING`, and copied the number into `TaxInvoiceId`. Amounts over RM 10,000 became `NEEDS_BUYER_TIN`. That is homemade LHDN (`NP-XX-001`). New `receipts` insert sets **number + title + disclaimer**. The journal insert is unrelated to a 28th-of-the-month job. `B2cConsolidationJob` stays in the museum.

---

## 7. Refunds / disputes later vs v1

### 7.1 What v1 S1 (this dogfood) includes

**Nothing in the refund/dispute writers.** First successful pay. Official Receipt. Balanced journal. Webhook retry no-ops. Merchant GET receipt. That is steps 11–12.

`NP-MON-005` (full refund: call gateway, then reverse the journal **once**) and `NP-MON-006` (disputes: do not double-reverse) are **V1**, not S1. They are specified here so S1 does not paint itself into the 008 P0s.

### 7.2 Full refund later — steal once-reverse, refuse the rest

Old happy path after Waves 0–4 (008 §3–4): `RecordRefundCommandHandler` remaining machine; API rails publish `GatewayRefundRequested` then adapter; mark-refunded rails publish `GatewayRefundCompleted` immediately; Billing `GatewayRefundCompletedHandler` posts `GATEWAY_REFUND` with `CONTRA_REVENUE_REFUNDS` + tax reverse + `CN-`. That **operator-initiated** loop is real. Steal:

- Remaining cap: do not refund more than paid.
- Journal contra **once** per fulfilled refund attempt, unique `(org_id, gateway_refund, provider_refund_id)`.
- Tax reverse proportional to the original sale tax (AwayFromZero), last slice should close remaining tax — 009 noted independent per-attempt scaling can leave 7.9999 ≠ 8; fix that when you build it.
- `ValidateBalanced`.
- Call the **gateway first** (or mark-refunded only on rails that have no refund API). Then the journal. Not journal-then-maybe-adapter.
- Fees: if the PSP does not return a reclaimed fee, do not invent one (`RefundedFee = 0` was specified). Label it.

Do **not** steal:

- `CN-` as a MyInvois type `02`. A commercial credit note PDF is a later document type; v1 can show “refunded” on the original `RCPT-` without a legal CN.
- Lhdn `GatewayRefundCompletedIntegrationEventHandler`: full only; ≤72h `CancelTaxDocumentCommand`; ≥72h type `02`; `Total_including_tax = RefundedAmount + TaxAmount` while `RefundedAmount` is already gross (008 P1-6). `NP-XX-001`, `NP-XX-010`.
- `LhdnDocumentCancelledIntegrationEventHandler` mirroring **every original line** after Billing already posted `GATEWAY_REFUND` (008 P0-2: cash and tax through the looking-glass). New Pay has no LHDN cancel consumer. When a tax **provider** exists, the provider’s cancel must **not** insert a second cash reverse if Pay already reversed.
- Publishing `GatewayRefundCompleted` from the **refund** path **and** from a dispute handler.
- `IAuditRecorder` after `SaveChanges` (`RecordRefundCommandHandler.RecordAuditAsync`). Refund audit is `NP-AUD-002`: **same transaction** as the reverse.

Partial refunds are `NP-SOON-006`. Do not build a partial machine in S1 “while we are here.”

### 7.3 Disputes later — do not double-reverse

008 P0-1: `CommerceGatewayDisputeCreatedHandler` published `GatewayRefundCompleted` with `Id = dispute.Id` so Billing would contra. A dispute is not a refund. Funds are held; outcome unknown. Wave 3 documented the reuse; it was still wrong. `e18edbe` **stopped the publish**. 009 §5: GMV dispute persists OPEN, `MarkDisputed`, `HasOpenDispute` latch; **no** journal. `ChargebackClawbackHandler` still no-ops GMV.

Steal the **corrected** judgment, not the Wave 3 ticket:

| Event | v1 later behaviour |
|-------|-------------------|
| Dispute opened | Flag the payment / subscription. **Do not** insert `gateway_refund`. **Do not** cancel access automatically (old ops copy: “Access stays active until you cancel.”). |
| Dispute lost / charge reversed by PSP | **One** journal reverse (same as refund-once), keyed by the PSP’s reversal id. |
| Dispute won | If you never reversed, do nothing to cash. If you wrongly reversed, you have a bug — do not “fix” it by reversing again from a second event. |
| Refund then dispute | Must not book two contras. Unique key on the **payment** reversal, not on “every event id we ever saw.” |
| Dispute then refund | Remaining machine: if still unreversed, one reverse. |

`HasOpenDispute` as a **one-way latch** (never cleared on win) is not a money bug; it is a chrome bug. Do not port it as eternal truth.

Inbound `charge.refunded` dropped on the allow-list (008 P0-3) is a **later** ingress bug. S1 allow-list is payment success / fail. When refunds ship, either consume the PSP refund event **or** do not treat Stripe `pending` as terminal (`IssueRefundAsync` treated `pending` as success).

`LedgerReferenceTypes.GatewayDispute` exists on this HEAD. A dedicated dispute journal is acceptable later. Reusing `gateway_refund` is not.

### 7.4 What S1 must not “leave a hook for”

Do not add `lhdn_cancellation` reference types, 72-hour windows, or a Credit Notes page titled “Credit & Debit Notes” (`NP-XX-010`). Do not subscribe `InvoiceIssued` “for AR refunds.” Do not add `CONTRA_REVENUE_REFUNDS` to the S1 chart **until** the refund writer exists — unused accounts are how READMEs outrun publishers.

S1 schema may still have a `journal_entries.reference_type` check that only allows `gateway_payment`. Opening `gateway_refund` later is a migration. That is cheaper than an unused contra account and a parked handler.

---

## 8. Mail: receipt email in process

### 8.1 Product lock

`NP-MAIL-001`: Receipt email after paid. Same process; not a Notify service. Wave S1 (dogfood not Y — still required for the loop to be honest; tracker notes `—` on Dogfood).

011/01: “Transactional email: receipt, dunning, failed pay, buyer magic link.” Dunning / failed pay / magic link are V1 (`NP-MAIL-002`…`004`). This handler enqueues **receipt**.

011/07: Pay ↔ Notify makes receipt eventual and buyer magic-link a distributed critical path. Until a **second product** shares a sending domain, Notify is a package / table inside Pay (`NP-LAT-004`).

### 8.2 What the old tree did (do not copy)

`GenerateAndStoreDocumentCommandHandler` uploads PDF to R2, then:

```csharp
await _eventBus.PublishAsync(new DocumentPublishedIntegrationEvent(
    request.OrganizationId,
    request.LedgerEntryId,
    request.DocumentType,
    storageKey,
    tenantSlug,
    businessName,
    customerName,
    customerEmail));
```

`DocumentPublishedIntegrationEventHandler` (`Modules/Communications/...`):

- returns if `CustomerEmail` or `TenantSlug` empty;
- maps document type to a **template name**;
- **returns if `MessageTemplates` row is missing** (silent no email);
- 008 §4.6: Tax Invoice / Credit Note **fall back to the Official Receipt template** if the merchant never created those templates — email talks like a receipt, PDF titled Tax Invoice;
- publishes `DispatchMessageIntegrationEvent` (email + WhatsApp body);
- `SaveChanges` on CommunicationsDbContext (not the Billing transaction).

`IAuditRecorder` XML: fire-and-forget, must never throw. Mail was the same shape: optional, another schema, WhatsApp-shaped.

### 8.3 New shape

Inside `FulfillPaidCheckout`’s `BEGIN`: insert `mail_outbox (kind=receipt, to=payer_email, receipt_id, payload json, status=pending)`.

After `COMMIT`, same process:

```text
TrySendReceiptEmail(row):
    render a boring HTML body from the payload (Official Receipt, RCPT-…, amount, org name)
    send via the tenant’s SMTP/Resend key if present, else Pay’s transactional key if you have one
    on success: status=sent
    on failure: leave pending, increment attempts, next_attempt_at
```

A tiny loop in the Pay process (not `MessagingInboxConsumerJob`, not WhatsApp, not credits) retries `pending`. If SMTP is down, **money is still true**. That is the accepted lag 011/13 named for Notify. It is **not** an accepted lag for the journal.

Do not gate send on a merchant-editable template named `"Official Receipt"`. A missing template is how the old tree silently sent nothing. v1 body is code. Merchant branding (logo, from-name) can be org settings later.

Do not fill a WhatsApp body (`NP-XX-004`).

Do not `POST` One to look up the buyer. The payer mailbox is on the checkout row.

Do not wait for PDF/R2. The email can say “your payment of RM 108 was received. Receipt number RCPT-2026-00001.” A link, if any, points at **Pay** (`/v1` public receipt GET with a signed token later — `NP-BUY-005`), not Hub `/public/billing/{slug}/documents/{guid}`.

From-address: Pay-owned transactional domain. Not One’s invite mail. Staff invite copy-link stays One (`NP-MAIL` header in 011/11).

### 8.4 Audit vs mail

| Write | In the money `BEGIN`? | Failure |
|-------|----------------------|---------|
| `audit_events` | **Yes** | Rolls back the charge |
| `mail_outbox` row | **Yes** | Rolls back the charge (we promised enqueue) |
| SMTP send | **No** (after commit) | Outbox stays pending |

That pairing is 011/07: “If audit is down, you either block the business write (audit is not separate) or lose the trail (audit is a lie).” We block. Mail send is the one that may lag.

Old `AuditEvent` fields worth stealing: `OrganizationId`, `ActorUserId`, `ActorEmail`, `Action`, `EntityType`, `EntityId`, `MetadataJson`, `CreatedAt`. Webhook actor is not a One user — `actor` = `system:psp-webhook` is enough. Do not copy `one.AuditEvents` as a table in the One schema. Pay’s table, Pay’s transaction.

---

## 9. Anti-goals (Tax Invoice, VALID, UUID numbers, waiting on One, dual-write Hub)

Keep these rows. Deleting them is how the museum comes back. Mapped to 011 refuse IDs where they exist.

| Anti-goal | Why | Lock |
|-----------|-----|------|
| Title the document **Tax Invoice** | Commercial paper is not a tax invoice. Old B2B pay issued H1 Tax Invoice before MyInvois knew the document existed (008 §4.2, 009 B06-D02). Later H1 `"Invoice"` is still wrong. | NP-DOC-003, NP-XX-003 |
| Print **VALID** / MyInvois UUID / QR | VALID means a tax system said VALID. Sandbox VALID was **never captured** in the old tree (`docs/honesty/lhdn-sandbox-valid.md`, 008 §12). | NP-DOC-004, NP-XX-001 |
| Homemade LHDN / XML / UBL / consolidation / JSON 1.1 signer / types `01`–`14` / 72h cancel / XAdES | Tax later = provider. Pay never owns this in v1. | NP-XX-001, NP-LAT-001 |
| TIN-at-checkout as a **legal** e-invoice feature | Quote path stuffed company name into `IdValue` (008 §11). Product path can file type `01` and still not have VALID. | NP-XX-002 |
| Number the receipt with a **UUID** / slice of a Guid | `CustomerFacingNumber` exists because someone did. Missing = `PENDING`. | NP-DOC-002 |
| Allocate `INV-` on first pay | That series meant “tax invoice” in the old product. v1 is `RCPT-` only. | NP-DOC-001 |
| Wait on One to hear an event before the seat exists | `SubscriptionActivated` / `AppEntitlementGranted` / `POST` members. One down = webhook retries = mess. Buyer access is the Pay row. | NP-FUL-001, NP-FUL-002, 011/07 rule 2 |
| Create a Zitadel human per cardholder | Buyer plane is Pay. | NP-XX-013 |
| Dual-write old Hub (`apps/lazuar-api` tables, `lazuar-ops` ledger, Hub SaaS fee plane, credits wallet) from this webhook | One writer per fact. Cutover reads are 013-02 / 013-09. Live dual-write is a second money story. | this paper §2.6 |
| Dual-write **inside** Pay via an in-process bus (`GatewayPaymentCompleted` → Commerce + Billing + Lhdn + Communications) | Already paid that tax. | NP-XX-009 |
| `InvoiceIssuedIntegrationEvent` / `ManualPaymentRecordedIntegrationEvent` | Subscribed-without-publisher / publisher-without-subscriber. | 00-why-leave |
| `TaxInvoiceId` dual-use column | Receipt # + UUID + consolidation ref. | 00-why-leave |
| Notify or Audit as a **process** in v1 | Same Pay DB transaction. | NP-XX-019 |
| Fire-and-forget audit that cannot throw | `IAuditRecorder` XML. Trail becomes optional. | NP-AUD-001 |
| Silent no-email when a template row is missing | `DocumentPublishedIntegrationEventHandler` `if (template == null) return`. | NP-MAIL-001 |
| WhatsApp body on the receipt path | Vitamin. | NP-XX-004 |
| Debit notes, self-billed 11–14, page titled “Credit & Debit Notes” | Strategy-only lies. | NP-XX-010 |
| Stripe Billing `subscription.updated` as source of truth | Wrap-rails. First successful pay is **our** webhook handler. | NP-XX-012 |
| Setup-intent / `$0` as paid / burning a `RCPT-` | `NP-GW-008`; this HEAD’s Billing skip of `AmountPaid <= 0` is the judgment. | NP-GW-008 |
| Per-module schemas (`pay_commerce`, `pay_billing`, `pay_lhdn`) | One database, one migration timeline. | NP-XX-009, 011/13 |
| MediatR cathedral, `AddBillingModule`, project reference into `apps/lazuar-api` | Host README already forbids it. | 012/03, Pay README |
| Undercharge when SST state is unknown | 167 throw. NP-MON-004. | NP-MON-004 |
| Tax(line net) instead of unit-then-seats | Sen split tests. | NP-MON-003 |
| Document series year in UTC | 31 Dec 18:00 UTC is 1 Jan MYT. | 011/09, `MalaysiaTime` |
| Dashboard “Net Cash in Bank” = P&L net | 008 §10. | honesty |
| Portal label “Download tax invoice” on a `RCPT-` | `PortalDocumentQueryService.Classify`. | NP-DOC-003 |
| Waiting to implement this handler until LHDN sandbox VALID exists | VALID is later/provider. The Official Receipt is the v1 product. | 011/01 dogfood |

---

## 10. Open questions

These are **not** invitations to un-refuse `NP-XX`. They are decisions a later implement/checklist program must pin. Until pinned, the binding in this paper’s table stands.

1. **Receipt PDF in S1 vs JSON-only ops.** NP-DOC-005 says the merchant can **open** the receipt. Is JSON on `:5178` enough for dogfood, or do we render PDF in-process on GET in the same slice? Recommendation: JSON first, PDF same GET as a follow-up **without** R2. Do not block step 12 on object storage.

2. **Org SST onboarding UX.** Tri-state requires the merchant to declare registered / not registered before live charge. Where does that live in `:5178`? A single boolean + number field in org settings is enough. Do not revive `BillingProfilePage` Card 2 (MyInvois cert, MSIC, SANDBOX/PROD).

3. **Payer upsert at hosted-page submit vs only at paid.** Old CRM resolved at `InitiateCheckout`. New hosted page (`:5179`) collects name/email with no One account. Recommendation: store name/email on the **open** checkout (so abandon still has a mailbox for later), upsert `payers` **only** in the paid transaction so a never-paid email is not a “customer.” `NP-BUY-001` is the session; `NP-BUY-002` is the profile.

4. **Late expiry vs paid.** Steal `CanFulfillFromPayment` (open **or** expired)? Recommendation: **yes**. A paid webhook after expiry is money. Do not steal custom-quote `COMPLETED` naming.

5. **One-off storage.** Dedicated `orders` table vs paid checkout only. Recommendation: **no** `orders` table in S1. Lists can query `checkouts where status=paid and subscription_id is null`.

6. **Fee line when CHIP/Stripe send `balance_transaction.fee` later than `payment_intent.succeeded`.** NP-MON-002: unknown ≠ 0. If the first event has no fee, book fee 0. A later fee event is a **new** journal (or an adjustment line) — not a rewrite of the sale. Pin this when 06-money-rails specifies adapter payloads.

7. **Idempotency grain when CHIP reuses ids across tenants.** Unique `(org_id, reference_type, reference_id)` is the grain. If a provider id is not globally unique, org in the key is mandatory (old global unique index was the wrong grain — 008 P1-8). Confirm per adapter in 06.

8. **Mail from-address and BYOK SMTP.** Tenant Resend key vs Lazuar transactional domain. Out of this paper’s money kernel; the outbox row does not care. Do not invent a Communications module to hold the question.

9. **Public buyer receipt URL in S1.** Dogfood sentence does not require the buyer to download. Merchant ops does. Signed public GET can wait for `NP-BUY-005`.

10. **Render language / MYR sen.** QuestPDF vs HTML-to-PDF vs no PDF. Judgment only: do not import `Modules.Billing.Infrastructure.Documents` or `ICommerceDocumentLookup`.

11. **When a tax provider exists, do we keep the Official Receipt and attach VALID as a second document?** 011 later: provider returns VALID + QR. Recommendation: **yes, two documents.** The `RCPT-` never mutates into a tax invoice. A new row (different series, different title) appears **only** when the provider says VALID. Until then the word VALID does not appear. This is the “don’t call it Tax Invoice until VALID” note without lying during the wait.

12. **Refunds in the same function vs a second function.** Later V1: `FulfillRefund` is a sibling function, same database, same “one transaction” rule — not a Billing inbox. Do not preview it as an event on the S1 bus.

13. **`PENDING` on purpose.** Should ops display `PENDING` if PDF is not yet generated but `RCPT-` exists? No. `PENDING` is for a **missing number**, not a missing blob. Number is in the transaction.

14. **Clock for `MalaysiaTime` in tests.** Pin a clock seam (`utcNow`) like `DocumentSeries.Prefix(DateTime? utcNow)`. Do not call `DateTime.UtcNow` untestably in the allocator.

15. **VIEWER vs member on GET receipt.** 012/07: One has no VIEWER. `authz/check member` is the S1 gate. A future “cannot refund” rule must not hide the receipt GET. Pin in 013-04 / 013-08.

16. **Do we copy `SubscriptionActivation` trial?** `IsTrialOffer` + `ActivateTrial`. Not dogfood. Recommendation: **no trial in S1.** Paid → `active` with period = next bill.

17. **Metadata `type=commerce_subscription` vs `saas_subscription` vs `custom_payment_link`.** New host does not need a type bag to partition Hub fee vs GMV vs quotes. If a string is required for PSP metadata, one value: `lazuar_pay_checkout`. Do not resurrect `platform_saas_fee` on this handler.

18. **Cutover of old `RCPT-2026-#####` into the new sequence table.** 013-09. This handler on a greenfield org starts at `00001`. Do not read `billing.DocumentSequences` at runtime from the new process.

---

## Appendix A — Old type names (read-only index)

So a later implementer can open the museum without importing it.

| Name | Path |
|------|------|
| `SstTaxMath` | `apps/lazuar-api/Modules/Commerce/Application/SstTaxMath.cs` |
| `SubscriptionBillingAmount` / `Breakdown` / `GrossBreakdown` / `MerchantHasSstAsync` / `StampSstMetadata` / `TaxFromInclusiveGross` | `apps/lazuar-api/Modules/Commerce/Application/SubscriptionBillingAmount.cs` |
| `SubscriptionActivation` | `apps/lazuar-api/Modules/Commerce/Application/SubscriptionActivation.cs` |
| `CheckoutSession` (old) | `apps/lazuar-api/Modules/Commerce/Domain/Aggregates/CheckoutSession.cs` |
| `CheckoutSession` (new fixture) | `apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutSession.cs` |
| `Subscription` | `apps/lazuar-api/Modules/Commerce/Domain/Aggregates/Subscription.cs` |
| `Order` | `apps/lazuar-api/Modules/Commerce/Domain/Aggregates/Order.cs` |
| `Product.SetSst` | `apps/lazuar-api/Modules/Commerce/Domain/Aggregates/Product.cs` |
| `ClientProfileEntity` | `apps/lazuar-api/Modules/CRM/Domain/ClientProfileEntity.cs` |
| `ResolveClientProfileCommand` | `apps/lazuar-api/Modules/CRM/Contracts/ResolveClientProfileCommand.cs` |
| `LedgerEntry` / `ValidateBalanced` / `AssignB2cReceipt` / `TaxInvoiceId` | `apps/lazuar-api/Modules/Billing/Domain/Aggregates/LedgerEntry.cs` |
| `LedgerLine` | `apps/lazuar-api/Modules/Billing/Domain/Entities/LedgerLine.cs` |
| `DocumentSequence` | `apps/lazuar-api/Modules/Billing/Domain/Entities/DocumentSequence.cs` |
| `AccountTypes` / `LedgerReferenceTypes` / `LhdnValidationStatuses` | `apps/lazuar-api/Modules/Billing/Domain/AccountTypes.cs` |
| `TenantBillingProfile` | `apps/lazuar-api/Modules/Billing/Domain/Aggregates/TenantBillingProfile.cs` |
| `DocumentSeries` / `CustomerFacingNumber` | `apps/lazuar-api/Modules/Billing/Contracts/DocumentSeries.cs` |
| `MalaysiaTime` | `apps/lazuar-api/Modules/Billing/Contracts/MalaysiaTime.cs` |
| `GenerateNextSequenceNumberCommand` / `GenerateNextSequenceNumberCommandHandler` | `…/Contracts/Commands/…` and `…/Infrastructure/Commands/…` |
| `IBillingTransactional` / `ILedgerRepository` / `LedgerRepository` | `Modules/Billing/Application` and `Infrastructure/Repositories` |
| `GatewayPaymentCompletedHandler` | `apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/GatewayPaymentCompletedHandler.cs` |
| `GatewayPaymentCompletedIntegrationEventHandler` | `apps/lazuar-api/Modules/Commerce/Infrastructure/EventHandlers/GatewayPaymentCompletedIntegrationEventHandler*.cs` |
| `GatewayPaymentCompletedIntegrationEvent` | `apps/lazuar-api/Modules/Payments/Contracts/Events/GatewayPaymentCompletedIntegrationEvent.cs` |
| `ProcessGatewayWebhookCommandHandler` | `apps/lazuar-api/Modules/Payments/Application/Commands/ProcessGatewayWebhookCommandHandler.cs` |
| `InvoiceIssuedIntegrationEvent` / `InvoiceIssuedHandler` / `InvoiceIssuedIntegrationEventHandler` | Billing contracts + Billing handler + Lhdn no-op |
| `ManualPaymentRecordedIntegrationEvent` | `apps/lazuar-api/Modules/Billing/Contracts/Events/ManualPaymentRecordedIntegrationEvent.cs` |
| `DocumentPublishedIntegrationEvent` / `GenerateAndStoreDocumentCommandHandler` | Billing |
| `InvoiceDocumentFactory` | `apps/lazuar-api/Modules/Billing/Infrastructure/Documents/InvoiceDocumentFactory.cs` |
| `DocumentPublishedIntegrationEventHandler` | `apps/lazuar-api/Modules/Communications/Infrastructure/EventHandlers/DocumentPublishedIntegrationEventHandler.cs` |
| `DispatchMessageIntegrationEvent` | `apps/lazuar-api/Modules/Messaging/Contracts/DispatchMessageIntegrationEvent.cs` |
| `IAuditRecorder` / `AuditRecorder` / `AuditEvent` | `Modules/One/Contracts`, `Infrastructure/Services`, `Domain` |
| `GatewayRefundCompletedHandler` | `apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/GatewayRefundCompletedHandler.cs` |
| `RecordRefundCommandHandler` | `apps/lazuar-api/Modules/Commerce/Application/Commands/RecordRefundCommandHandler.cs` |
| `B2cConsolidationJob` | `apps/lazuar-api/Modules/Billing/Infrastructure/Workers/B2cConsolidationJob.cs` |
| `PlatformSaasFeeHandler` | `apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/PlatformSaasFeeHandler.cs` |

---

## Appendix B — Tracker rows this paper specifies (do not flip)

Status stays `todo`. This file is evidence for a later checklist program, not a done stamp.

| ID | What “done” will mean when someone implements |
|----|-----------------------------------------------|
| NP-FUL-001 | One handler, one transaction, no One wait, no Billing inbox |
| NP-FUL-002 | Seat/session row in Pay; no One member for the cardholder |
| NP-FUL-003 | Ops list of payments + subscribers via `/v1` |
| NP-MON-001 | Balanced journal on first pay |
| NP-MON-002 | Fee line only if PSP sent a fee |
| NP-MON-003 | Unit exclusive SST × seats |
| NP-MON-004 | Unknown SST throws |
| NP-DOC-001 | `RCPT-` Official Receipt |
| NP-DOC-002 | Never UUID; missing display `PENDING` |
| NP-DOC-003 | Not titled Tax Invoice |
| NP-DOC-004 | No VALID print |
| NP-DOC-005 | Merchant GET in ops |
| NP-MAIL-001 | Outbox row in the same transaction; send in-process |
| NP-AUD-001 | Audit row in the same transaction |
| NP-BUY-001 / 002 | Payer on session; small payer table |
| NP-XX-001, 002, 003, 010, 012, 013, 019 | Still refuse |

`NP-FUL-004` / `005`, `NP-MON-005` / `006`, `NP-MAIL-002`…`004`, `NP-AUD-002` stay **later / V1** as in 011/11. Do not implement them from this analysis.

---

End of paper 07. Implementation is a later program. Do not condense this file into the 013 README.
