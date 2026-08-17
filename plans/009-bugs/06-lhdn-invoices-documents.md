# 06 — LHDN invoices + commercial documents (bug audit)

**Date:** 17 August 2026  
**Branch:** `feat/007-waves-1-4-implement` (`297ba98`)  
**Workspace:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay`  
**Slice:** Commercial paper + MyInvois pipeline only. Quotes (`QT-`), Official Receipts (`RCPT-`), tax invoices (`INV-`), credit / debit / refund notes, LHDN submit / poll, QR, document identity, B2B TIN, stationery.  
**Does not implement. Does not commit.**

This file re-reads the tree at `297ba98`. It is not a rewrite of `plans/008-evals/04-lhdn-invoicing-documents.md`. An 008 finding is closed only if this commit no longer contains it. New defects 008 missed are still written up.

**Standing honesty (not a code bug unless the UI or a comment claims otherwise):**

- `docs/honesty/lhdn-sandbox-valid.md` (`e1d5407`) is still “not captured.” No operator run in this repo has produced a MyInvois sandbox document that polls to `VALID` with a scannable QR. That is a **missing proof**, not an implementation defect, unless a screen or PDF says `VALID` when it is not.
- Official Receipt ≠ MyInvois. `RCPT-` with the two disclaimers is the honest B2C product. Calling that PDF a tax invoice would be a code/copy bug. The current Official Receipt factory and footer do **not** do that.

Code wins. Quotes below are from this commit.

---

## 0. How to read this file

Wave 2 remounted invoicing pages. Wave 4 added receipt honesty copy. Neither closed the MyInvois loop, and neither fixed the quote B2B CRM arity that 008 already named.

Four commercial jobs still exist:

| Job | What the tree actually does |
|-----|-----------------------------|
| 1. Quote / proforma | `QT-yyyy-#####` on a Commerce `CheckoutSession`. QuestPDF title **Proforma Invoice**. Not LHDN. |
| 2. Commercial AR invoice | **Still not built.** Terms persist. Buyer page is “Total Due” + pay now. No open balance, no partial, no reminder that is this slice. |
| 3. Official Receipt | Silent B2C pay. `RCPT-`. Two disclaimers. Honest. |
| 4. LHDN e-invoice | Backend path exists. Product B2B can *request* type `01` if CRM identity is complete. Quote B2B cannot. Clearance is unproven. `INV-` PDF titled **Tax Invoice** is issued **on pay**, before MyInvois knows the document exists. |

Hunt items this slice was asked to confirm or refute:

Quote B2B identity wrong · `INV-` issued as if VALID · poll never advances · TIN validate false-accept · credit note against wrong doc · number series collision · QR payload wrong · submit without credits · self-bill vs normal mixup · document shown to wrong tenant · receipt disclaimer missing vs present.

---

## 1. Absolute paths (live anchors)

| Concern | Path |
|---------|------|
| Sequences | `apps/lazuar-api/Modules/Billing/Contracts/DocumentSeries.cs` |
| Sequence SQL | `apps/lazuar-api/Modules/Billing/Infrastructure/Commands/GenerateNextSequenceNumberCommandHandler.cs` |
| Ledger identity | `apps/lazuar-api/Modules/Billing/Domain/Aggregates/LedgerEntry.cs` |
| Pay → ledger + PDF | `apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/GatewayPaymentCompletedHandler.cs` |
| Offline / mark-paid ledger | `apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/ManualSubscriberEnrolledIntegrationEventHandler.cs` |
| Refund ledger + `CN-` | `apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/GatewayRefundCompletedHandler.cs` |
| VALID stamp + PDF regen | `apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/LhdnDocumentValidatedIntegrationEventHandler.cs` |
| LHDN cancel contra | `apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/LhdnDocumentCancelledIntegrationEventHandler.cs` |
| QuestPDF factory | `apps/lazuar-api/Modules/Billing/Infrastructure/Documents/InvoiceDocumentFactory.cs` |
| QuestPDF layout | `apps/lazuar-api/Modules/Billing/Infrastructure/Documents/BaseInvoiceDocument.cs` |
| Final PDF write | `apps/lazuar-api/Modules/Billing/Infrastructure/Commands/GenerateAndStoreDocumentCommandHandler.cs` |
| Draft proforma | `apps/lazuar-api/Modules/Billing/Infrastructure/Queries/GenerateDraftDocumentQueryHandler.cs` |
| Public HMAC download | `apps/lazuar-api/Modules/Billing/Infrastructure/Endpoints/PublicBillingEndpoints.cs` |
| Ops PDF presign | `apps/lazuar-api/Modules/Billing/Infrastructure/Endpoints/AdminLedgerEndpoints.cs` |
| B2C consolidation job | `apps/lazuar-api/Modules/Billing/Infrastructure/Workers/B2cConsolidationJob.cs` |
| Ledger search | `apps/lazuar-api/Modules/Billing/Infrastructure/Services/BillingQueryService.cs` |
| Quote create | `apps/lazuar-api/Modules/Commerce/Application/Commands/CreateCustomCheckoutCommandHandler.cs` |
| Quote / product pay | `apps/lazuar-api/Modules/Commerce/Application/Commands/InitiateCheckoutCommandHandler.cs` |
| Offline mark-paid | `apps/lazuar-api/Modules/Commerce/Application/Commands/MarkCheckoutAsPaidOfflineCommandHandler.cs` |
| PDF customer lookup | `apps/lazuar-api/Modules/Commerce/Infrastructure/Services/CommerceDocumentLookup.cs` |
| Portal document table | `apps/lazuar-api/Modules/Commerce/Infrastructure/Services/PortalDocumentQueryService.cs` |
| CRM resolve | `apps/lazuar-api/Modules/CRM/Contracts/ResolveClientProfileCommand.cs`, `…/CRM/Infrastructure/ResolveClientProfileCommandHandler.cs` |
| B2B MyInvois request | `apps/lazuar-api/Modules/Billing/Contracts/Events/B2bTaxInvoiceRequestedIntegrationEvent.cs` |
| B2B submit consumer | `apps/lazuar-api/Modules/Lhdn/Infrastructure/EventHandlers/B2bTaxInvoiceRequestedIntegrationEventHandler.cs` |
| Dead InvoiceIssued | `apps/lazuar-api/Modules/Lhdn/Infrastructure/EventHandlers/InvoiceIssuedIntegrationEventHandler.cs` |
| Cons submit consumer | `apps/lazuar-api/Modules/Lhdn/Infrastructure/EventHandlers/ConsolidatedInvoiceIssuedIntegrationEventHandler.cs` |
| Refund → cancel / type 02 | `apps/lazuar-api/Modules/Lhdn/Infrastructure/EventHandlers/GatewayRefundCompletedIntegrationEventHandler.cs` |
| Submit + sign + credits | `apps/lazuar-api/Modules/Lhdn/Application/Commands/SubmitTaxDocumentCommand.cs` |
| Cancel | `apps/lazuar-api/Modules/Lhdn/Application/Commands/CancelTaxDocumentCommand.cs` |
| 72h rule | `apps/lazuar-api/Modules/Lhdn/Domain/Rules/CancelWindowMustBeValidRule.cs` |
| TaxDocument | `apps/lazuar-api/Modules/Lhdn/Domain/Aggregates/TaxDocument.cs` |
| Buyer rules | `apps/lazuar-api/Modules/Lhdn/Domain/MyInvoisBuyerRules.cs` |
| Buyer mapper | `apps/lazuar-api/Modules/Lhdn/Infrastructure/Services/LhdnBuyerMapper.cs` |
| Strategy factory | `apps/lazuar-api/Modules/Lhdn/Infrastructure/Services/DocumentStrategyFactory.cs` |
| View model / entity swap | `apps/lazuar-api/Modules/Lhdn/Infrastructure/Services/Strategies/ViewModelMapper.cs` |
| JSON 1.1 signer | `apps/lazuar-api/Modules/Lhdn/Infrastructure/Services/JsonUblDocumentSigner.cs` |
| TIN service | `apps/lazuar-api/Modules/Lhdn/Infrastructure/Services/TaxpayerValidationService.cs` |
| QR host | `apps/lazuar-api/Modules/Lhdn/Infrastructure/Services/LhdnLinkService.cs` |
| Status GET | `apps/lazuar-api/Modules/Lhdn/Application/Queries/LhdnQueries.cs` |
| Public TIN | `apps/lazuar-api/Modules/Lhdn/Infrastructure/Endpoints/PublicTinValidationEndpoints.cs` |
| Integrator documents | `apps/lazuar-api/Modules/Lhdn/Infrastructure/Endpoints/DocumentEndpoints.cs` |
| Submit worker | `apps/lazuar-api/Modules/Lhdn/Infrastructure/Workers/LhdnSubmissionJob.cs` |
| Poll worker | `apps/lazuar-api/Modules/Lhdn/Infrastructure/Workers/LhdnStatusPollingJob.cs` |
| Gateway | `apps/lazuar-api/Modules/Lhdn/Infrastructure/Gateways/LhdnGatewayAdapter*.cs` |
| Lhdn README | `apps/lazuar-api/Modules/Lhdn/README.md` |
| Honesty | `docs/honesty/lhdn-sandbox-valid.md` |
| Sandbox scripts | `scripts/lhdn_sandbox/` |
| Ignored E2E | `apps/lazuar-api/tests/Lazuar.ModuleTests/Lhdn/LhdnSandboxE2ETests.cs` |
| Quote UI | `apps/lazuar-ops/src/modules/invoicing/pages/QuotesPage.tsx`, `CreateQuoteModal.tsx`, `QuoteDetailPanel.tsx` |
| Sales documents | `apps/lazuar-ops/src/modules/invoicing/pages/TaxInvoicesPage.tsx`, `TaxInvoiceDetailPanel.tsx` |
| Credit notes UI | `apps/lazuar-ops/src/modules/invoicing/pages/CreditNotesPage.tsx` |
| Product B2B toggle | `apps/lazuar-ops/src/modules/commerce/components/ProductForm.tsx` |
| `/pay/{id}` | `apps/lazuar-portal/src/app/[tenantSlug]/pay/[sessionId]/page.tsx` |
| QuoteView | `apps/lazuar-portal/src/modules/checkout/components/QuoteView.tsx` |
| Product checkout TIN | `apps/lazuar-portal/src/modules/checkout/components/CheckoutForm.tsx` |
| Buyer documents | `apps/lazuar-portal/src/app/[tenantSlug]/portal/page.tsx` |
| Email of PDF | `apps/lazuar-api/Modules/Communications/Infrastructure/EventHandlers/DocumentPublishedIntegrationEventHandler.cs` |

---

## 2. Pipeline mechanics (what the code actually does)

### 2.1 Series

`DocumentSeries` is four prefixes. Year is baked into the sequence key. LHDN UUID is never a series value.

```9:21:apps/lazuar-api/Modules/Billing/Contracts/DocumentSeries.cs
public static class DocumentSeries
{
    public const string Receipt = "RCPT";
    public const string Quote = "QT";
    public const string Invoice = "INV";
    public const string CreditNote = "CN";

    public static string Prefix(string series, DateTime? utcNow = null) =>
        $"{series}-{(utcNow ?? DateTime.UtcNow):yyyy}";

    public static string ReceiptPrefix(DateTime? utcNow = null) => Prefix(Receipt, utcNow);
    public static string QuotePrefix(DateTime? utcNow = null) => Prefix(Quote, utcNow);
    public static string InvoicePrefix(DateTime? utcNow = null) => Prefix(Invoice, utcNow);
    public static string CreditNotePrefix(DateTime? utcNow = null) => Prefix(CreditNote, utcNow);
```

Allocation is one SQL statement on `billing.DocumentSequences`:

```26:42:apps/lazuar-api/Modules/Billing/Infrastructure/Commands/GenerateNextSequenceNumberCommandHandler.cs
        // Atomically upserts and returns the incremented sequence value. 
        // This is safe under concurrency and prevents sequence gaps during rollbacks.
        const string sql = @"
            INSERT INTO billing.""DocumentSequences"" (""Id"", ""OrganizationId"", ""Prefix"", ""CurrentValue"")
            VALUES (@Id, @OrganizationId, @Prefix, 1)
            ON CONFLICT (""OrganizationId"", ""Prefix"") 
            DO UPDATE SET ""CurrentValue"" = billing.""DocumentSequences"".""CurrentValue"" + 1
            RETURNING ""CurrentValue"";";
        ...
        return $"{request.Prefix}-{nextValue:D5}";
```

Concurrent safety is real. Gaplessness is not: the handler opens **its own** connection, outside the ledger transaction. A crash after increment and before `LedgerEntry` save **gaps** the series. Malaysian practice does not require gapless commercial numbers. The **comment** is the lie.

Who writes what:

| Series | Writer |
|--------|--------|
| `QT-yyyy-#####` | `CreateCustomCheckoutCommandHandler` |
| `RCPT-yyyy-#####` | `GatewayPaymentCompletedHandler` (B2C), `ManualSubscriberEnrolledIntegrationEventHandler` (non-B2B) |
| `INV-yyyy-#####` | same two handlers on B2B |
| `CN-yyyy-#####` | `GatewayRefundCompletedHandler` on every refund row; Lhdn refund handler can allocate a **second** `CN-` if it cannot see the refund ledger yet |

Zero-amount 100% coupon checkouts write a ledger row with **no** `RCPT-` / `INV-` (`ZeroAmountCheckoutHandler.cs:27–42`). Portal history will not show a receipt.

`CustomerDocumentNumber` is immutable (`LedgerEntry.cs:72–73`, `102`). `UpdateLhdnStatus` **does** overwrite `TaxInvoiceId` with the MyInvois UUID (`140–147`). That dual-use field is still the consolidation correlation key. See B06-D26 / D29.

### 2.2 Pay → paper → (maybe) MyInvois

`GatewayPaymentCompletedHandler` is the spine.

B2C (`metadata["is_b2b_required"] != "true"`):

```89:117:apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/GatewayPaymentCompletedHandler.cs
        if (!isB2b)
        {
            var receiptNumber = await _mediator.Send(
                new GenerateNextSequenceNumberCommand(@event.OrganizationId, DocumentSeries.ReceiptPrefix()));
            entry.AssignB2cReceipt(receiptNumber);
            if (@event.AmountPaid > _b2cIndividualThresholdMyr)
            {
                entry.MarkConsolidationNotRequired();
                entry.UpdateLhdnStatus(null, LhdnValidationStatuses.NeedsBuyerTin);
            }
        }
        ...
            await _mediator.Send(new GenerateAndStoreDocumentCommand(
                @event.OrganizationId,
                entry.Id,
                "Official Receipt",
                CorrelationId: correlation
            ));
```

B2B:

```100:136:apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/GatewayPaymentCompletedHandler.cs
        else
        {
            var invoiceNumber = await _mediator.Send(
                new GenerateNextSequenceNumberCommand(@event.OrganizationId, DocumentSeries.InvoicePrefix()));
            entry.AssignB2bInvoice(invoiceNumber);
        }
        ...
            await _mediator.Send(new GenerateAndStoreDocumentCommand(
                @event.OrganizationId,
                entry.Id,
                "Tax Invoice",
                CorrelationId: correlation
            ));

            await _eventBus.PublishAsync(new B2bTaxInvoiceRequestedIntegrationEvent(
```

On gateway paid, **before** MyInvois has said anything, the customer already has a PDF whose H1 is **Tax Invoice**. That is still the code. W2-LP-103 called it intentional. It is commercially convenient and legally unsafe if anyone emails that PDF as the Malaysian tax invoice.

QR and UUID are attached only when `LhdnValidationStatus == Valid` at generation time (`GenerateAndStoreDocumentCommandHandler.cs:79–82`). First PDF has neither. A later regen on VALID can add them (`LhdnDocumentValidatedIntegrationEventHandler.cs:48–63`).

### 2.3 How a type `01` is supposed to be born

There is still **no** `new InvoiceIssuedIntegrationEvent(` in production code. The live hook is `B2bTaxInvoiceRequestedIntegrationEvent`.

1. Pay (or offline B2B **custom**) → Billing books `INV-` and publishes `B2bTaxInvoiceRequested`.
2. Lhdn handler maps CRM + Commerce display → `SubmitDocumentRequestDto` type `_01`.
3. If `LhdnBuyerMapper.TryCreatePayloadBuyer` fails (no TIN, stub TIN, empty/`NA` id value), the handler **logs and returns**. No `TaxDocument`. Ops stays blank / `NOT REQUIRED`. The customer already has the Tax Invoice PDF.
4. `SubmitTaxDocumentCommand` persists `TaxDocument` `PENDING`. Meters 3 credits when not test mode (`appsettings.json` `Credits:Costs:LhdnSubmit = 3`). Deduction is **after** persist; deduction failure is logged, not fatal (`SubmitTaxDocumentCommand.cs:152–169`).
5. `LhdnSubmissionJob` Base64-encodes `RawXmlContent` (XML **or** JSON), sets `format` via `DetectSubmissionFormat`, POSTs `/api/v1.0/documentsubmissions`.
6. On HTTP success / 202, reads `acceptedDocuments[0].uuid` if present and `MarkAsSubmitted`.
7. `LhdnStatusPollingJob` GETs `/api/v1.0/documentsubmissions/{submissionUid}`. `VALID` / `INVALID` terminal. Else reschedule. Sandbox 404 is “not processed yet”, retry 5s.
8. VALID publishes `LhdnDocumentValidated` + outbound `invoice.valid`. Billing stamps UUID/status and **re-generates** the PDF with QR (except `B2C-CONS-` keys).

Integrator `POST /lhdn/documents` returns `{ status: "accepted_for_processing" }`. That “accepted” is **Lazuar’s** 200, not MyInvois `acceptedDocuments`.

Default signing is **Off**. Default bytes are unsigned XML 1.0. JSON 1.1 is only when `Lhdn:Signing=Auto` and a decryptable `.p12` is on file. `GetBaseUrl()` is always `Lhdn:BaseUrl` or `https://preprod-api.myinvois.hasil.gov.my`. Tenant `Environment` SANDBOX/PROD is stored and shown and **never read** by the gateway.

### 2.4 Official Receipt disclaimer (present)

```90:93:apps/lazuar-api/Modules/Billing/Infrastructure/Documents/InvoiceDocumentFactory.cs
    internal static string? OfficialReceiptDisclaimer(string documentType) =>
        string.Equals(documentType, "Official Receipt", StringComparison.OrdinalIgnoreCase)
            ? "This Official Receipt confirms payment. It is not a validated MyInvois tax invoice."
            : null;
```

```185:191:apps/lazuar-api/Modules/Billing/Infrastructure/Documents/BaseInvoiceDocument.cs
                if (string.Equals(_model.DocumentType, "Official Receipt", StringComparison.OrdinalIgnoreCase))
                {
                    column.Item().PaddingBottom(6).Text(
                            "Payment receipt. Not an LHDN e-invoice.")
```

Tax Invoice PDFs get **no** “pending MyInvois validation” note. That is also tested (see §7). Honesty of Official Receipt is real. Honesty of Tax Invoice is not.

---

## 3. Quoted walk — three paths a merchant can click today

### 3.1 B2C product pay (the honest path)

1. Product does **not** require tax ID.
2. Buyer pays. Metadata has no `is_b2b_required=true`.
3. Ledger: `RCPT-2026-#####`, `CustomerType=B2C`, `LhdnValidationStatus=B2C_RECEIPT` (or `NEEDS_BUYER_TIN` if amount > RM 10,000).
4. QuestPDF title **Official Receipt**. Notes + footer both say it is not an LHDN e-invoice.
5. Ops Sales documents lists the row. Badge `B2C RECEIPT` or `NEEDS BUYER TIN`.
6. If the buyer has a subscription magic-link, portal Documents classifies **Official Receipt**.
7. On the 28th MYT the B2C job may file a General Public type `01` as `B2C-CONS-{yyyyMM}-{org:N}`. Individual receipts are not themselves e-invoices.

This path is demoable and, until consolidation VALID paints those receipts `VALID` (B06-D28), honest.

### 3.2 B2B product pay (the only path that can honestly reach PENDING)

1. ProductForm “Require Company Name & Tax ID (LHDN B2B)” is on. Subtitle still says *“We do not validate the TIN at checkout.”* That subtitle is false (B06-D07).
2. CheckoutForm collects company, TIN, ID type/value, and **does** `POST /public/commerce/{slug}/validate-tin`. If the merchant has not connected MyInvois, that endpoint 400s “Merchant has not connected MyInvois.” and checkout **cannot proceed** (B06-D18).
3. Country default on the form is `"MY"` (`CheckoutForm.tsx:53`). CRM / UBL want `"MYS"` (B06-D16).
4. `InitiateCheckoutCommandHandler` uses **named** CRM args. Company goes to `CompanyName`. TIN / IdType / IdValue are correct (`198–208`).
5. Pay stamps `is_b2b_required=true`. Billing allocates `INV-` and generates a PDF titled **Tax Invoice** immediately (B06-D02).
6. `GetCustomerForDocumentAsync` returns the **transaction log** first if it has an email. The log constructor only has name + email. The PDF’s “Billed To” is the person name, **no buyer TIN, no company, no address** (B06-D03).
7. Lhdn handler does a **second** CRM-by-email lookup. If TIN + ID pair exist, it files type `01` with one synthetic line `"B2B sale"` classification `022` (B06-D11). Tax rate is `TaxAmount / AmountExcludingTax` (a **fraction**, e.g. `0.08`), written into UBL `cbc:Percent` (B06-D09).
8. If TIN validate at submit fails, `SubmitTaxDocumentCommand` throws. Inbox retries. Ops stays without a `TaxDocument`. The INV- PDF is already in the buyer’s inbox.

This is the only path that can honestly reach `PENDING`. It has not been proven to `VALID` in this repo.

### 3.3 B2B quote `/pay/{id}` (the path that looks like Compliance CaaS and is not)

1. Ops Create Quote. Placeholder “e.g. Acme Corp” (`CreateQuoteModal.tsx:123`). Merchant types a **company** into Client Name. Checkbox: “Require buyer tax ID (B2B tax invoice after payment)” (`CreateQuoteModal.tsx:184`).
2. `CreateCustomCheckoutCommandHandler` resolves CRM with **only** `OrganizationId`, `ClientName`, `ClientEmail`, `""` phone (`30–35`). Company is `FullName`. `CompanyName` stays null. No TIN at quote time.
3. Buyer opens `/{slug}/pay/{sessionId}`. Page is live (`pay/[sessionId]/page.tsx:16–42`). Heading **Proforma Invoice**. Draft PDF is also **Proforma Invoice**. That pairing is honest.
4. QuoteView B2B fields: company name (optional) + TIN (required). **No** `id_type` / `id_value`. **No** `validate-tin` (`QuoteView.tsx:36–55`, `163–179`).
5. `handleProceedToPayment` sends `name: checkout.client_name`, `company_name`, `tax_id`. Session branch of `InitiateCheckoutCommandHandler`:

```134:142:apps/lazuar-api/Modules/Commerce/Application/Commands/InitiateCheckoutCommandHandler.cs
                await _mediator.Send(new ResolveClientProfileCommand(
                    tenantId.Value,
                    request.Name,
                    request.Email,
                    request.Phone ?? "",
                    request.TaxId,
                    null,
                    request.CompanyName,
                    customBillingAddress), ct);
```

Mapped against the record (`ResolveClientProfileCommand.cs:7–18`):

| Positional arg | Parameter | Value |
|----------------|-----------|-------|
| 5 | `Tin` | tax id (ok) |
| 6 | `IdType` | `null` |
| 7 | `IdValue` | **`request.CompanyName`** |
| 8 | `BillingAddress` | address or null |
| 10 | `CompanyName` | **never set** |

6. `ResolveClientProfileCommandHandler` **enriches only empty fields** (`45–58`). First quote pay writes `IdValue = "Acme Sdn Bhd"` permanently. A later product checkout with a real BRN **cannot overwrite** it (B06-D05).
7. Pay books `INV-` and a Tax Invoice PDF. QuoteDetailPanel promised “B2B tax invoice” (`QuoteDetailPanel.tsx:172`).
8. `LhdnBuyerMapper.TryCreatePayloadBuyer`: `idType` defaults to **BRN**; `idValue` is the company name string. If the buyer left company blank, `idValue` empty → skip submit. If they filled it, submit proceeds to TIN validate with BRN = “Acme Sdn Bhd” → MyInvois 404 → command throws → no `VALID`.
9. “Open buyer portal” on a completed quote (`QuoteView.tsx:96–98`) goes to `/{slug}/portal` **without a token**. Portal documents require a magic-link bound to a **subscription**. Quote-only buyers have no subscription. They cannot open history (B06-D32).

This is the highest-leverage Wave 2 bug that is not “go get a sandbox UUID.” It is still in the tree at `297ba98`.

---

## 4. Bug catalog

Priorities: **P0** ships a wrong legal object or poisons identity. **P1** breaks the MyInvois loop or teaches the merchant the wrong lesson. **P2** is copy, dead code, or a narrow window.

### B06-D01 — Quote B2B CRM arity: company name written into `IdValue` (P0)

**Status at `297ba98`:** open. Same defect 008 §11 named. Not fixed.

Evidence: `InitiateCheckoutCommandHandler.cs:134–142` (quoted in §3.3). Command shape: `ResolveClientProfileCommand.cs:7–18`. Product path next to it uses named args and is correct (`198–208`).

`CheckoutB2bIdentityTests.InitiateCheckout_CustomSession_CopiesIsB2bRequiredIntoMetadata` only asserts gateway metadata `is_b2b_required=true`. It does **not** assert `ResolveClientProfileCommand` arguments. The session branch is unguarded.

Effect: type `01` either never created, or created with BRN = company name and then INVALID / retry-loop.

### B06-D02 — `INV-` PDF titled “Tax Invoice” on pay, before VALID (P0)

**Status:** open. Intentional per `W2-LP-103-done.md`. Still a legal lie if the merchant emails it.

```119:126:apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/GatewayPaymentCompletedHandler.cs
        else
        {
            await _mediator.Send(new GenerateAndStoreDocumentCommand(
                @event.OrganizationId,
                entry.Id,
                "Tax Invoice",
                CorrelationId: correlation
            ));
```

Factory adds **no** pending note for that title (`InvoiceDocumentFactory.cs:90–93`). `InvoiceDocumentFactoryTests.CreateHeader_TaxInvoice_DoesNotAddReceiptDisclaimer` **locks the missing note in**.

`GatewayPaymentCompletedHandlerTests.HandleAsync_WhenB2B_BooksB2b_SkipsReceiptAndOfficialPdf` asserts `DocumentType == "Tax Invoice"` and that Official Receipt is **not** sent. The test treats the lie as the spec.

Portal classifies the same row as Tax Invoice whenever `CustomerType == "B2B"` or the number starts with `INV-` (`PortalDocumentQueryService.cs:197–198`), VALID or not. Subscription card label becomes “Download tax invoice” (`184–185`).

### B06-D03 — Transaction-log short-circuit strips buyer TIN / company / address from the PDF (P0)

**Status:** open. Same as 008 §4.4.

```92:102:apps/lazuar-api/Modules/Commerce/Infrastructure/Services/CommerceDocumentLookup.cs
    public async Task<CommerceCustomerDisplay?> GetCustomerForDocumentAsync(...)
    {
        var fromLog = await FindCustomerOnTransactionLogAsync(organizationId, referenceId, ct);
        if (fromLog != null && !string.IsNullOrWhiteSpace(fromLog.Email))
        {
            return fromLog;
        }
```

```157:161:apps/lazuar-api/Modules/Commerce/Infrastructure/Services/CommerceDocumentLookup.cs
        return new CommerceCustomerDisplay(
            string.IsNullOrWhiteSpace(log.CustomerName) ? "Customer" : log.CustomerName,
            log.CustomerEmail ?? "",
            null,
            null);
```

The interface comment even documents the preference (`ICommerceDocumentLookup.cs:31–33`: “Prefers an existing transaction log email”). After a real pay, the log almost always exists. Therefore the Tax Invoice PDF’s “Billed To” is typically the **person name from checkout**, with **no buyer TIN**.

Lhdn submit does a second CRM-by-email lookup (`B2bTaxInvoiceRequestedIntegrationEventHandler.cs:42–46`). MyInvois can have a real buyer while the customer PDF does not. That is worse than “we have no e-invoice.”

`FromCrmAsync` (`201–213`) **would** deliver TIN, company, address, id type/value. The short-circuit never reaches it on the common path.

### B06-D04 — QuoteView collects TIN only; no ID pair; no `validate-tin` (P0)

**Status:** open.

```36:55:apps/lazuar-portal/src/modules/checkout/components/QuoteView.tsx
    if (checkout.is_b2b_required && !taxId.trim()) {
      setGlobalError("Company tax ID (TIN) is required for this payment request.");
      return;
    }
    ...
        company_name: checkout.is_b2b_required ? companyName.trim() || undefined : undefined,
        tax_id: checkout.is_b2b_required ? taxId.trim() : undefined,
```

Company name is optional. ID type/value are absent. `CheckoutForm.tsx:96–110` and `227–252` do the opposite on the product path.

Backend session branch does not require `IdType`/`IdValue` the way `EnforceCheckoutConfiguration` does for products (`425–428`).

### B06-D05 — CRM enrich-only: poisoned `IdValue` can never be corrected (P0)

**Status:** open. 008 named the write. It did not name the permanence.

```50:58:apps/lazuar-api/Modules/CRM/Infrastructure/ResolveClientProfileCommandHandler.cs
            if (string.IsNullOrWhiteSpace(existingProfile.IdType) && !string.IsNullOrWhiteSpace(request.IdType))
            {
                existingProfile.IdType = request.IdType;
                isModified = true;
            }
            if (string.IsNullOrWhiteSpace(existingProfile.IdValue) && !string.IsNullOrWhiteSpace(request.IdValue))
            {
                existingProfile.IdValue = request.IdValue;
                isModified = true;
            }
```

First quote pay writes `IdValue = "Acme Sdn Bhd"`. A later product checkout with a real BRN finds the profile by email and **leaves IdValue alone**. Every subsequent type `01` for that email uses the company name as BRN until someone anonymizes the profile.

`ClientProfileCompanyNameTests` only covers the **named** product-shaped command. It does not cover the session branch.

### B06-D06 — Ops / portal teach “Tax Invoice” / `VALID` on objects that are not cleared (P1)

Portal `Classify`:

```189:200:apps/lazuar-api/Modules/Commerce/Infrastructure/Services/PortalDocumentQueryService.cs
        if (ledger.ReferenceType is "GATEWAY_REFUND" or "LHDN_CANCELLATION"
            || DocumentSeries.IsCreditNoteNumber(ledger.CustomerDocumentNumber))
        {
            return "Credit Note";
        }

        if (ledger.CustomerType == "B2B" || DocumentSeries.IsInvoiceNumber(ledger.CustomerDocumentNumber))
            return "Tax Invoice";

        return "Official Receipt";
```

Ops Sales documents empty state: “No tax invoices found.” (`TaxInvoicesPage.tsx:169`) while the page title is the honest “Sales documents.” Badge for a pre-submit B2B row is **“NOT REQUIRED”** (`207–210`) — which is the opposite of what the PDF title said.

After consolidation VALID, `LhdnDocumentValidatedIntegrationEventHandler` updates **every** matching ledger row to `VALID` (`41–44`) and the test **asserts** that (`LhdnDocumentValidatedIntegrationEventHandlerTests.cs:62–65`). Those `RCPT-` rows are not individually validated e-invoices. Handler skips QR regen for the cons key (`51–53`) — correct — but the badge lies.

### B06-D07 — ProductForm subtitle: “We do not validate the TIN at checkout” (P2)

**Status:** open leftover lie.

```221:222:apps/lazuar-ops/src/modules/commerce/components/ProductForm.tsx
                <span className="text-[12px] font-medium text-[#09090b] block">Require Company Name &amp; Tax ID (LHDN B2B)</span>
                <span className="text-[11px] text-[#71717a] block mt-0.5">Collects buyer company + TIN. We do not validate the TIN at checkout.</span>
```

`CheckoutForm.tsx:96–110` calls `validateTin`. The subtitle is false. Quotes, which **don’t** validate, have no such warning.

### B06-D08 — Offline product mark-paid drops `IsB2bRequired` (P1)

**Status:** open.

Custom session:

```210:220:apps/lazuar-api/Modules/Commerce/Application/Commands/MarkCheckoutAsPaidOfflineCommandHandler.cs
            await _eventBus.PublishAsync(new ManualSubscriberEnrolledIntegrationEvent(
                ...
                session.IsB2bRequired));
```

Product session (`166–175`) omits the bool. Event default is `false` (`ManualSubscriberEnrolledIntegrationEvent.cs:16`). A product flagged “Require Company Name & Tax ID” that is marked paid offline is booked **B2C**, gets `RCPT-`, and never publishes `B2bTaxInvoiceRequested`.

`ManualSubscriberEnrolledIntegrationEventHandler` trusts `@event.IsB2bRequired` (`43`). It also books **no SST** (`51–52`: cash = revenue = `AmountPaid`). Offline product B2B is a double miss: wrong document type and understated tax.

### B06-D09 — Type `01` tax `Percent` is a fraction, not a percent (P0)

**Status:** open. 008 did not file this as a numbered defect.

```81:83:apps/lazuar-api/Modules/Lhdn/Infrastructure/EventHandlers/B2bTaxInvoiceRequestedIntegrationEventHandler.cs
                    Tax_rate = @event.AmountExcludingTax == 0 ? 0 : (double)(@event.TaxAmount / @event.AmountExcludingTax),
```

If SST is 16 on 200, `Tax_rate = 0.08`. That value is copied into the view model (`ViewModelMapper.cs:96`) and emitted as:

- XML: `<cbc:Percent>{{ format_amount line.tax_rate }}</cbc:Percent>` (`StandardInvoice.xml:131`)
- JSON: `["Percent"] = line.TaxRate` (`UblJsonDocumentBuilder.cs:171`)

B2C consolidation does the **opposite** (correct percent):

```289:289:apps/lazuar-api/Modules/Billing/Infrastructure/Workers/B2cConsolidationJob.cs
                    TaxRate: taxAmount > 0 ? Math.Round((taxAmount / grossRevenue) * 100, 2) : 0,
```

A product B2B sale with real SST is a realistic MyInvois INVALID even if TIN/ID are perfect. Tests submit `Tax_rate = 0` (`MyInvoisLoopTests.SamplePayload`) and never assert percent scale.

### B06-D10 — B2B event `TaxAmount` is the raw gateway field, not the resolved SST (P1)

**Status:** open.

Ledger books tax via `ResolveTaxAmount` (event field **or** metadata `sst_tax_amount`) (`GatewayPaymentCompletedHandler.cs:67`, `159–174`). Gross for the event is `AmountPaid - taxAmount` (resolved). The published event then sends **`@event.TaxAmount`**, not the resolved value (`134`).

Billplz’s `TaxAmount=0` with SST in metadata: ledger is right, type `01` is `Total_tax=0` and `Total_excluding_tax` already reduced by SST, so `Total_including_tax` does not equal amount paid. Pairs with B06-D09.

### B06-D11 — One synthetic line `"B2B sale"` / classification `022`; quote lines discarded (P1)

**Status:** open.

```74:88:apps/lazuar-api/Modules/Lhdn/Infrastructure/EventHandlers/B2bTaxInvoiceRequestedIntegrationEventHandler.cs
            Items = new List<LhdnItemDto>
            {
                new()
                {
                    Description = "B2B sale",
                    Classification_code = "022",
                    Quantity = 1,
                    Unit_price = (double)@event.AmountExcludingTax,
                    ...
                }
            },
```

Quote line items never reach UBL. Product name never reaches UBL. Ledger MSIC for B2B is also hardcoded `"022"` (`GatewayPaymentCompletedHandler.cs:69`) — that field is then reused as classification on cons lines (`B2cConsolidationJob.cs:286`). Classification `022` (e-commerce) is not an MSIC. Supplier MSIC in UBL is tenant config or `"00000"` (`ViewModelMapper.cs:42`).

### B06-D12 — TIN HTTP 200 with empty / unparseable body is treated as valid (P1)

**Status:** open.

```27:38:apps/lazuar-api/Modules/Lhdn/Infrastructure/Gateways/LhdnGatewayAdapter.Tin.cs
        if (response.IsSuccessStatusCode)
        {
            try
            {
                var json = JsonDocument.Parse(responseBody);
                var taxpayerName = json.RootElement.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
                return new LhdnTinValidationResult(true, true, taxpayerName, null);
            }
            catch
            {
                return new LhdnTinValidationResult(true, true, null, null);
            }
        }
```

Any 2xx, including empty body, is `IsValid=true`. `TaxpayerValidationService` then caches **valid for 30 days** (`TaxpayerValidationService.cs:71`). Product checkout and `SubmitTaxDocumentCommand` both trust this. A gateway/proxy that 200s an HTML error page false-accepts a TIN and then **files type `01`**.

404 is correctly `IsValid=false` (`41–44`). Other statuses throw / fail. The false-accept is specifically the 200+garbage path.

Default cache salt is `"default_local_salt_replace_in_prod"` (`TaxpayerValidationService.cs:31`). Shared salt across tenants is fine (cache key includes org). A production deploy that never sets `Lhdn:TinHashSalt` is a comment-shaped hole, not a false-accept.

### B06-D13 — Stub TIN lists are not the same (P1)

**Status:** open.

`MyInvoisBuyerRules.IsStubTin` only refuses `C1234567890` (`MyInvoisBuyerRules.cs:16–17`). `LhdnBuyerMapper.StubTins` is `C1234567890`, `IG1234567890`, `EI00000000010` (`LhdnBuyerMapper.cs:11–16`).

B2B handler uses the mapper (skips IG / General Public). Integrator `POST /lhdn/documents` uses `EnsureBuyerTinValidAsync`, which only blocks `C1234567890` and then asks MyInvois. `LhdnSingleCreditPathTests` uses buyer TIN `IG1234567890` and stubs validation as **valid** (`LhdnSingleCreditPathTests.cs:52`, `173`). The credit test treats a mapper-stub TIN as a happy-path buyer.

`EI00000000010` + `NA` is correctly skipped at submit (`SubmitTaxDocumentCommand.cs:177–181`). The mapper also treats that TIN as a stub so a B2B handler cannot accidentally file General Public as a named buyer. Good. The IG mismatch is the hole.

### B06-D14 — Poller does not write poll UUID back onto `TaxDocument` (P1)

**Status:** open. Closest thing in the hunt to “poll never advances.”

`MarkAsSubmitted` stores UUID from `acceptedDocuments[0]` (`TaxDocument.cs:50–58`). Submit parser:

```88:94:apps/lazuar-api/Modules/Lhdn/Infrastructure/Gateways/LhdnGatewayAdapter.Submit.cs
        string? uuid = null;
        if (root.TryGetProperty("acceptedDocuments", out var acceptedDocs) && acceptedDocs.GetArrayLength() > 0)
        {
            uuid = acceptedDocs[0].TryGetProperty("uuid", out var uuidProp) ? uuidProp.GetString() : null;
        }

        return new LhdnSubmissionResult(true, submissionUid, uuid, null);
```

If `acceptedDocuments` is empty but `submissionUid` exists, Success=true, Uuid=null. Document is SUBMITTED. Poll later gets uuid + longId from `documentSummary`. Poller:

```89:99:apps/lazuar-api/Modules/Lhdn/Infrastructure/Workers/LhdnStatusPollingJob.cs
                    if (result.Status == "VALID")
                    {
                        doc.MarkAsValid(result.LongId!);
                        ...
                        await eventBus.PublishAsync(new LhdnDocumentValidatedIntegrationEvent(
                            doc.OrganizationId, doc.InternalReferenceId, result.Uuid!, "VALID", qrLink));
```

`MarkAsValid` sets `LongId` only (`TaxDocument.cs:91–98`). **`LhdnUuid` stays null.** GET `/lhdn/documents/{internalId}` builds QR only from `doc.LhdnUuid` + `doc.LongId` (`LhdnQueries.cs:35–39`). Ops panel QR is therefore **blank** after VALID if submit missed the uuid, even though Billing received `result.Uuid` on the event.

Poll **does** advance SUBMITTED → VALID/INVALID on the happy path. 404 is Success=false + retry 5s (`LhdnGatewayAdapter.Status.cs:34–37`). Missing config `continue`s after the lease was claimed (`LhdnStatusPollingJob.cs:78–81`); the row is not stuck forever, only leased. “Poll never advances” as a total hang is **not** what this code does. “Poll advances and the QR/GET still look empty” is.

### B06-D15 — QR host is always preprod; ops renders via `api.qrserver.com` (P1 / P2)

**Status:** open.

```15:18:apps/lazuar-api/Modules/Lhdn/Infrastructure/Services/LhdnLinkService.cs
    public string GetPortalUrl()
    {
        return _configuration["Lhdn:PortalUrl"]?.TrimEnd('/') ?? "https://preprod.myinvois.hasil.gov.my";
    }
```

`appsettings.json:64` default is preprod. Tenant `Environment=PROD` does not change it. Payload shape `{portal}/{uuid}/share/{longId}` is the official MyInvois share URL. The **host** is wrong for production. There is no scanned QR from a real VALID UUID in this repo (honesty file).

Ops panel:

```261:261:apps/lazuar-ops/src/modules/invoicing/components/TaxInvoiceDetailPanel.tsx
                    src={`https://api.qrserver.com/v1/create-qr-code/?size=160x160&data=${encodeURIComponent(qrLink)}`}
```

UUID + LongId are sent to a third-party QR SaaS. QuestPDF uses in-process QRCoder (`BaseInvoiceDocument.cs:202–210`) — that half is fine.

### B06-D16 — Tenant `Environment` is cosmetic; checkout country default is `MY` (P1)

**Status:** open.

```44:47:apps/lazuar-api/Modules/Lhdn/Infrastructure/Gateways/LhdnGatewayAdapter.cs
    private string GetBaseUrl()
    {
        return _configuration["Lhdn:BaseUrl"]?.TrimEnd('/') ?? "https://preprod-api.myinvois.hasil.gov.my";
    }
```

Ops Legal card can flip SANDBOX/PROD. GET config echoes it (`LhdnQueries.cs:84–86`). Traffic does not move.

CheckoutForm `useState("MY")` (`CheckoutForm.tsx:53`). Initiate stores `request.CountryCode ?? "MYS"` (`InitiateCheckoutCommandHandler.cs:194`). If `requires_address` is on and the buyer leaves the default, CRM gets `MY`. UBL `Country.IdentificationCode` becomes `MY`. LHDN wants ISO 3166-1 alpha-3 `MYS`. Realistic INVALID.

### B06-D17 — Submit without credits: deduct-after-persist is fail-open (P1)

**Status:** open as a money/compliance pair.

Pre-check `HasSufficientCreditsAsync` (`SubmitTaxDocumentCommand.cs:77–83`) then persist `TaxDocument` then deduct (`152–169`). If deduct throws, the document is already PENDING and the worker will submit. Comment says this is intentional. A tenant at 0 credits who races two submits, or whose deduct fails, **files MyInvois for free**.

`LhdnDocumentSubmittedIntegrationEventHandler` correctly does **not** deduct again (that double-charge was fixed). `LhdnSingleCreditPathTests` asserts the deduct call happens; it does **not** assert behaviour when deduct fails.

Test mode (`IExecutionContextAccessor.IsTestMode`) skips metering entirely (`74–76`). Sandbox scripts that hit the API with test-mode on do not prove the credit gate.

### B06-D18 — Integrator `accepted_for_processing` is Lazuar, not MyInvois; product B2B checkout is coupled to MyInvois (P1)

**Status:** open.

```37:38:apps/lazuar-api/Modules/Lhdn/Infrastructure/Endpoints/DocumentEndpoints.cs
                await mediator.Send(new SubmitTaxDocumentCommand(ctx.TenantId, idempotencyKey, req));
                return TypedResults.Ok(new StatusResponse { Status = "accepted_for_processing" });
```

The document is PENDING in Lazuar. Worker has not POSTed yet.

Separately: product checkout **requires** `validate-tin` to succeed before pay (`CheckoutForm.tsx:96–110`). Public TIN 400s “Merchant has not connected MyInvois.” if creds are missing (`PublicTinValidationEndpoints.cs:45–47`). A merchant who only wants a commercial INV- PDF cannot take a B2B product payment until MyInvois is connected. Quote path, which is the broken identity path, has no such gate.

### B06-D19 — Type `02` credit note UBL can double-count tax (P0)

**Status:** open.

```119:136:apps/lazuar-api/Modules/Lhdn/Infrastructure/EventHandlers/GatewayRefundCompletedIntegrationEventHandler.cs
            Items = new List<LhdnItemDto>
            {
                new()
                {
                    Description = "Refund",
                    Classification_code = "022",
                    Quantity = 1,
                    Unit_price = (double)@event.RefundedAmount,
                    Tax_rate = 0,
                    Tax_amount = (double)@event.TaxAmount,
                    Subtotal = (double)@event.RefundedAmount,
                    Tax_type_code = LhdnItemDtoTax_type_code._06
                }
            },
            Total_excluding_tax = (double)@event.RefundedAmount,
            Total_tax = (double)@event.TaxAmount,
            Total_including_tax = (double)(@event.RefundedAmount + @event.TaxAmount)
```

`RefundedAmount` on the payments event is the money that left the gateway — typically **gross**. Adding `TaxAmount` again makes `Total_including_tax` larger than the refund. `Tax_type_code` is `_06` (not applicable) while `Tax_amount` may be non-zero. CreditNote.xml still has `<cbc:Percent>0</cbc:Percent>` and BillingReference `<cbc:ID>NA</cbc:ID>` (`CreditNote.xml:27–28`) with only the original UUID filled. That `NA` original document number is a realistic INVALID even when the UUID is right.

`GatewayRefundCompletedIntegrationEventHandlerTests.FullRefund_After72h_SubmitsCreditNoteWithCrmTin` asserts type `_02`, CN number, buyer TIN. It does **not** assert totals.

### B06-D20 — Partial refunds skip LHDN entirely; commercial `CN-` still issued (P1)

**Status:** open.

```49:55:apps/lazuar-api/Modules/Lhdn/Infrastructure/EventHandlers/GatewayRefundCompletedIntegrationEventHandler.cs
        if (!@event.IsFullRefund)
        {
            _logger.LogInformation(
                "Skipping LHDN cancel/CN for partial refund PaymentRecordId {PaymentRecordId}.",
                @event.PaymentRecordId);
            return;
        }
```

Billing still allocates `CN-` on every refund row (`GatewayRefundCompletedHandler.cs:73–76`). Ops Credit Notes shows a CN number with LHDN “NOT REQUIRED.” Portal classifies it Credit Note and offers a download. No PDF was generated (B06-D21). The legal note does not exist.

### B06-D21 — Credit note PDF is never generated on refund; Lhdn handler can mint a second `CN-` (P1)

**Status:** open.

`GatewayRefundCompletedHandler` does **not** call `GenerateAndStoreDocumentCommand`. The only Credit Note PDF path is VALID regen (`LhdnDocumentValidatedIntegrationEventHandler.ResolveDocumentType` returns `"Credit Note"` for refund / CN- numbers). Partial refunds never VALID. Full refunds inside 72h cancel (no type `02`). Full refunds after 72h may VALID later.

Until then, ops “Download PDF Document” (`TaxInvoiceDetailPanel.tsx:77–81`) hits `GET /admin/billing/ledger/{id}/document`, which **always** presigns `vault/{tenant}/documents/{id}.pdf` with **no existence check** (`AdminLedgerEndpoints.cs:36–46`). Buyer portal does the same HMAC URL. The click 404s on R2.

Race: Lhdn refund handler looks up the refund ledger by `PaymentRecordId:EventId` (`173–184`). If it runs **before** Billing’s refund handler, it calls `GenerateNextSequenceNumberCommand` and files type `02` as `CN-00002` while Billing later stamps the ledger `CN-00001`. Two commercial numbers, one refund. `TaxDocuments` index on `(OrganizationId, InternalReferenceId)` is **not unique** (`LhdnDbContext.cs:76`).

### B06-D22 — Original document resolution can walk the wrong key; cancel+refund double row (P1)

**Status:** open.

```150:156:apps/lazuar-api/Modules/Lhdn/Infrastructure/EventHandlers/GatewayRefundCompletedIntegrationEventHandler.cs
        var candidates = new[]
        {
            payment?.CustomerDocumentNumber,
            payment?.LhdnDocumentUuid,
            payment?.TaxInvoiceId,
            @event.PaymentRecordId.ToString()
        }
```

After VALID, `TaxInvoiceId` is the UUID — looking up by UUID is correct. `PaymentRecordId.ToString()` as last candidate is a Guid. `GetTaxDocumentByInternalIdAsync` is FirstOrDefault on a **non-unique** index. Unlikely unless someone submitted with that Guid as `internal_id`. The more common “wrong doc” is the **missing** original (skip) or the **second CN** (B06-D21).

Full refund ≤72h sends `CancelTaxDocumentCommand` (`68–74`). Cancel also posts `LHDN_CANCELLATION` mirroring every original line (`LhdnDocumentCancelledIntegrationEventHandler.cs:41–65`). Billing already posted `GATEWAY_REFUND` with a `CN-`. Credit Notes page lists both (`BillingQueryService.cs:60–62`). Trigger labels: “Refund” vs “Cancellation” (`CreditNotesPage.tsx:157`). A 72h refund+cancel appears **twice**. Cancellation row has **no** `CN-` (`AssignCustomerDocumentNumber` is never called).

### B06-D23 — Types `03` / `04` / `11`–`14` are strategy-only; page title overclaims (P2)

**Status:** open.

```33:40:apps/lazuar-api/Modules/Lhdn/Infrastructure/Services/DocumentStrategyFactory.cs
            "02" or "03" or "04" => 
                _serviceProvider.GetRequiredKeyedService<IUblDocumentStrategy>("CreditNote"),
            "11" => 
                _serviceProvider.GetRequiredKeyedService<IUblDocumentStrategy>("SelfBilledInvoice"),
            "12" or "13" or "14" => 
                _serviceProvider.GetRequiredKeyedService<IUblDocumentStrategy>("SelfBilledCredit"),
```

Refund handler hardcodes `_02`. No ops composer. Credit Notes page title is **“Credit & Debit Notes”** (`CreditNotesPage.tsx:100`). Lhdn README claims debit, refund, and all four self-billed types as supported (`README.md:14–25`). ViewModelMapper entity-swap for self-bill is real code (`ViewModelMapper.cs:33–85`) with **no production publisher**. `scripts/lhdn_sandbox/07_test_self_billed.sh` exists; `run_all.sh` runs it; no committed log.

Empty buyer TIN on an integrator type `01` selects **B2C consolidated** strategy (`DocumentStrategyFactory.cs:19`, `27–28`). A B2B payload with a blank TIN becomes a General Public template. That is a self-bill / cons mixup adjacent.

### B06-D24 — B2C consolidation idempotency is dead in workers; banner dies after VALID (P1)

**Status:** open. 008 §6.5 / §7.5 still true, with a sharper root cause.

`alreadyConsolidated`:

```209:211:apps/lazuar-api/Modules/Billing/Infrastructure/Workers/B2cConsolidationJob.cs
        var alreadyConsolidated = await db.LedgerEntries.AnyAsync(e =>
            e.OrganizationId == orgId
            && e.TaxInvoiceId == consolidationRef, ct);
```

**No `IgnoreQueryFilters`.** Platform filter: `OrganizationId == ExecutionContext.TenantId` (`PlatformDbContext.cs:43–46`). Comment on that filter: *“empty ambient TenantId matches no rows (workers must IgnoreQueryFilters + explicit org).”* The same file’s pending-row queries **do** `IgnoreQueryFilters` (`107`, `152`). The safety-net query does not. In a worker with empty TenantId, `alreadyConsolidated` is **always false**.

The live defense is only `ConsolidationStatus == Consolidated` excluding rows from the select. That holds on the happy path. It does **not** hold if:

- publish succeeds and `SaveChanges` fails (event already out; rows still Pending; next run files again);
- any new Pending row appears for a previously consolidated month.

`ConsolidatedInvoiceIssuedIntegrationEventHandler` uses `idempotencyKey = Guid.CreateVersion7().ToString()` (`57`). Submit-level dedup on `Internal_id` does not exist. Two type `01` for `B2C-CONS-{yyyyMM}-{org}` is possible.

After VALID, `UpdateLhdnStatus` writes the UUID into `TaxInvoiceId` (`LedgerEntry.cs:142–147`). Ops banner searches `B2C-CONS-` (`TaxInvoicesPage.tsx:51–61`) against `ReferenceId` / `TaxInvoiceId` / `CustomerDocumentNumber` (`BillingQueryService.cs:51–53`). After VALID those are a gateway tx, a UUID, and `RCPT-…`. **The banner goes blank at the moment you would want it.**

`B2cConsolidationJobTests.SecondRun_SamePeriod_IsIdempotent` only double-runs **before** VALID. It never overwrites `TaxInvoiceId`. It does not exercise `alreadyConsolidated` under a worker filter.

### B06-D25 — Sequence “prevents gaps” comment; `TaxDocument.InternalReferenceId` not unique (P2 / P1)

Comment lie: `GenerateNextSequenceNumberCommandHandler.cs:26–27` (quoted §2.1).

Index:

```76:76:apps/lazuar-api/Modules/Lhdn/Infrastructure/LhdnDbContext.cs
            builder.HasIndex(x => new { x.OrganizationId, x.InternalReferenceId });
```

Not unique. Two PENDING rows can share `INV-2026-00001` if idempotency keys differ (cons Guid keys; credit-note race). GET by internal id is `FirstOrDefault` (`LhdnRepository.cs:37–41`). Cancel / poll / ops attach to **one** of them arbitrarily.

B2B handler’s key `b2b-inv:{org}:{invoiceNumber}` is the one honest idempotency string in this slice.

### B06-D26 — Country / address / phone placeholders that INVALID a real submit (P1)

Empty buyer address becomes `NA` / `00000` / state `17` (`LhdnBuyerMapper.cs:35–42`). ViewModelMapper default state for missing address is **`14`** (WP KL) (`ViewModelMapper.cs:48`, `64`). Two different “we don’t know” states.

Phone is `+60000000000` when missing (`ViewModelMapper.cs:43`, `60`). InvoicePeriod Description is always `"Monthly"` (`StandardInvoice.xml:21`, `UblJsonDocumentBuilder.cs:27`) even for a one-time quote. Credit note original ID is `"NA"` (B06-D19).

None of these are proven INVALID in-repo. They are the bytes we would send on the first real sandbox run.

### B06-D27 — InvoiceIssued is dead; comments name handlers that do not exist (P2)

```8:25:apps/lazuar-api/Modules/Lhdn/Infrastructure/EventHandlers/InvoiceIssuedIntegrationEventHandler.cs
/// InvoiceIssued has no honest buyer identity. MyInvois submit is
/// <see cref="B2bSaleSubmitHandler"/>. This handler must never file stub TIN C1234567890.
...
            "Ignoring InvoiceIssued {Invoice} — MyInvois submit uses B2bSaleReadyForEinvoice only.",
```

`B2bSaleSubmitHandler` does not exist. `B2bSaleReadyForEinvoice` does not exist. The live type is `B2bTaxInvoiceRequestedIntegrationEventHandler`. Grep of `new InvoiceIssuedIntegrationEvent` in production: zero. `MyInvoisLoopTests.InvoiceIssuedHandler_DoesNotSubmitStubTin` only asserts the no-op does not throw.

### B06-D28 — Lhdn README still says signatures unimplemented / XAdES (P2)

`Modules/Lhdn/README.md:32–36` still says XMLDSig/XAdES unimplemented and wait for `.p12`. Wave 2 added `JsonUblDocumentSigner`. Default path is unsigned 1.0, which the README’s “V1.0 stability” half gets right. The “signatures unimplemented” half is stale. Claiming XAdES in a demo is a lie. Claiming “we have no signer” is also a lie.

### B06-D29 — Tax Invoice / Credit Note email falls back to Official Receipt template (P1)

```38:50:apps/lazuar-api/Modules/Communications/Infrastructure/EventHandlers/DocumentPublishedIntegrationEventHandler.cs
        var preferredTemplate = @event.DocumentType switch
        {
            "Official Receipt" => "Official Receipt",
            "Draft Quotation" => "Quotation Ready",
            "Tax Invoice" => "Tax Invoice",
            "Credit Note" => "Credit Note",
            _ => null
        };
        ...
        var fallbackTemplate = preferredTemplate is "Tax Invoice" or "Credit Note"
            ? "Official Receipt"
            : null;
```

If the merchant never created a Tax Invoice template, the buyer gets an email that talks like a receipt, linking a PDF titled Tax Invoice. Draft quotes use document type `"Proforma Invoice"`, which is **not** in the switch (`GenerateDraftDocumentQueryHandler.cs:71`). `"Draft Quotation"` is never published. Quote emails, if any, are the invoice-reminder job (out of this slice except to note they exist).

### B06-D30 — Draft proforma identity and date are thin (P2)

Draft customer is session CRM `FullName` + email only (`CommerceDocumentLookup.cs:86–89`, `GenerateDraftDocumentQueryHandler.cs:66–68`). TIN is not printed. Issue date is `DateTime.UtcNow` at download (`73`), not session created-at — the date **moves** on every click. Currency is hardcoded `MYR` (`78`). Quote SST does not exist (CreateQuoteModal totals `qty * unit_price` only).

### B06-D31 — Quote-only buyer cannot open portal documents; same-email union (P2)

Portal documents require a subscription id from the magic-link token (`PortalDocumentQueryService.cs:44–52`). Quote-only buyers have no subscription. QuoteView “Open buyer portal” has no token (`QuoteView.tsx:96–98`).

Within a tenant, profiles that share an email are unioned (`57–63`) and all of those emails’ transaction logs become one table. Two clients of the same merchant who share a billing mailbox see each other’s documents. Not cross-tenant. HMAC download binds `tenantSlug + ledgerEntryId` (`PublicBillingEndpoints.cs:44–46`). Cross-tenant PDF theft via slug swap does not work without the JWT secret.

Public GET does **not** verify the ledger belongs to the tenant beyond the key path `vault/{tenantId}/documents/{id}.pdf`. Wrong-tenant GUID + valid HMAC for **this** slug would presign a missing object, not the other tenant’s file.

### B06-D32 — Large B2C `NEEDS_BUYER_TIN` has no resolution product (P2)

Pay-time (`GatewayPaymentCompletedHandler.cs:94–98`) and the cons job (`B2cConsolidationJob.cs:225–230`) both park above-threshold B2C as `NOT_REQUIRED` / `NEEDS_BUYER_TIN`. There is no flow that then collects a TIN. They sit in ops forever. Honesty of the badge is fine. Completeness of the product is not.

### B06-D33 — Buyer reject is not implemented (P2, honestly labelled)

Ops footer: “Supplier cancel only… Buyer reject is not implemented.” (`TaxInvoiceDetailPanel.tsx:296–298`). True. No portal reject button, no IRBM reject webhook consumer. Domain cancel is 72h from **local** `ValidatedAt` (`CancelWindowMustBeValidRule.cs:12–26`), which is `DateTime.UtcNow` at `MarkAsValid`, not IRBM’s clock. Close enough for a first cut; not proven.

Cancel applies `doc.Cancel()` **before** the gateway call (`CancelTaxDocumentCommand.cs:50–58`). If the gateway succeeds and `SaveChanges` fails, MyInvois is cancelled and Lazuar still shows VALID. Next cancel attempt will 400 at LHDN. Narrow window, real split-brain.

### B06-D34 — Stationery empty TIN is omitted, not “TIN not on file” (P2)

Factory fallback seller name is workspace name, then `"Merchant"` (`InvoiceDocumentFactory.cs:30`). Empty TIN is omitted (`BaseInvoiceDocument.cs:50–51`). W2-LP-107-done.md’s “TIN not on file” string is not in the factory. `InvoiceDocumentFactoryTests` locks “not Lazuar Merchant.” That part of the done-file is still overstated. Not a customer-facing lie today.

Legal profile Card 2 never auto-provisions. Submit without config throws “LHDN Tenant Configuration is missing.” (`SubmitTaxDocumentCommand.cs:99–103`). Seed genesis row is a hardcoded org + sandbox-looking TIN (`LhdnDbContext.cs:47–61`). Irrelevant unless that GUID is a live tenant.

### B06-D35 — Quotes page leftover “Tracking ad-hoc invoices” (P2)

`QuotesPage.tsx:42–43` title is honest (“Quotes & Proforma Invoices”). Line 57 still says “Tracking ad-hoc invoices.” Word “invoices” should not be there.

### B06-D36 — JSON 1.1 signer exists; ACCEPT does not (honesty, not a code bug)

`JsonUblDocumentSigner` hashes unsigned JSON, RSA-SHA256, appends `UBLExtensions` (`13–50`). Unit test with a **self-signed** cert asserts `SignatureValue` and no placeholder (`MyInvoisLoopTests.cs:205–219`). That is not MyInvois ACCEPT.

`run_all.sh` runs 00, 01, 02, 03, 06, 07 — **skips 04 cert + 05 v1.1** (`run_all.sh:7–12`). `LhdnSandboxE2ETests` is `[Ignore]` and only gets a token + polls a known submission uid. It does **not** submit. It does not assert `acceptedDocuments` or `overallStatus=Valid` (`LhdnSandboxE2ETests.cs:20–63`).

Until `docs/honesty/lhdn-sandbox-valid.md` is replaced, LP-110/111/113/117 stay unproven. That is **not** B06-D36 as a code defect. It is the honesty fence around every VALID-shaped claim.

---

## 5. 008 re-verify

`plans/008-evals/04-lhdn-invoicing-documents.md` (16 August 2026) is re-read against `297ba98`. Post-008 commits on this slice are honesty (`e1d5407`) and unrelated commerce/payments fixes. **No quote-arity fix. No PDF-identity fix. No “don’t title Tax Invoice until VALID” fix.**

| 008 claim | Still in tree? | 009 id |
|-----------|----------------|--------|
| Quote session branch puts `CompanyName` in `IdValue` | **Yes** `InitiateCheckoutCommandHandler.cs:134–142` | B06-D01 |
| Quote create puts company in `FullName` | **Yes** `CreateCustomCheckoutCommandHandler.cs:30–35` | B06-D01 / D04 |
| QuoteView no ID pair / no validate-tin | **Yes** | B06-D04 |
| `INV-` Tax Invoice PDF on pay | **Yes** | B06-D02 |
| Log short-circuit omits buyer TIN on PDF | **Yes** | B06-D03 |
| ProductForm “we do not validate TIN” | **Yes** | B06-D07 |
| Offline product pay drops B2B | **Yes** | B06-D08 |
| Country `MY` vs `MYS` | **Yes** | B06-D16 |
| `Environment` unused by `GetBaseUrl` | **Yes** | B06-D16 |
| QR host preprod | **Yes** | B06-D15 |
| Cons `TaxInvoiceId` overwritten on VALID; banner dies | **Yes** | B06-D24 |
| Cons receipts inherit VALID | **Yes**, test-locked | B06-D06 |
| Cons event idempotency is a new Guid | **Yes** | B06-D24 |
| Sequence “prevents gaps” comment | **Yes** | B06-D25 |
| InvoiceIssued dead + stale name | **Yes**, worse (`B2bSaleSubmitHandler`) | B06-D27 |
| README signatures unimplemented | **Yes** | B06-D28 |
| Partial refund skips LHDN | **Yes** | B06-D20 |
| Refund+cancel double ledger | **Yes** | B06-D22 |
| Types 03/04/11–14 strategy-only | **Yes** | B06-D23 |
| Credit Notes “Debit” title | **Yes** | B06-D23 |
| Email Tax Invoice → Official Receipt template | **Yes** | B06-D29 |
| Quote-only portal history | **Yes** | B06-D31 |
| Official Receipt disclaimers present | **Yes** — still honest | (not a bug) |
| Product path named CRM args | **Yes** — still correct | (not a bug) |
| QR only when VALID | **Yes** — still correct | (not a bug) |
| No sandbox VALID artifact | **Yes** — honesty, not a defect | B06-D36 fence |
| Tax rate fraction vs percent | **Missed by 008** | B06-D09 |
| B2B event TaxAmount vs resolved SST | **Missed by 008** | B06-D10 |
| TIN 200+garbage false-accept | **Missed by 008** | B06-D12 |
| Poll UUID not written back | **Missed by 008** | B06-D14 |
| Type 02 totals / `ID=NA` | **Missed by 008** | B06-D19 |
| Second `CN-` race + no CN PDF | **Missed by 008** | B06-D21 |
| `alreadyConsolidated` missing `IgnoreQueryFilters` | **Missed by 008** (008 saw the TaxInvoiceId clobber only) | B06-D24 |
| CRM enrich-only permanence | **Missed by 008** | B06-D05 |
| Product B2B checkout blocked without MyInvois | **Missed by 008** | B06-D18 |
| Deduct-after-persist fail-open | **Mentioned as log; not filed as submit-without-credits** | B06-D17 |

008’s “top next actions” 1–4 are all still unpaid.

---

## 6. Lying tests

A test is lying when it is green while the product claim it appears to guard is false, or when it **locks in** the wrong behaviour.

### 6.1 Tests that lock the lie in

| Test | What it freezes |
|------|-----------------|
| `GatewayPaymentCompletedHandlerTests.HandleAsync_WhenB2B_BooksB2b_SkipsReceiptAndOfficialPdf` | B2B pay **must** generate `"Tax Invoice"` and must **not** generate Official Receipt. B06-D02 becomes “the spec.” |
| `InvoiceDocumentFactoryTests.CreateHeader_TaxInvoice_DoesNotAddReceiptDisclaimer` | Tax Invoice PDFs must have `Notes == null`. No pending-MyInvois sentence can be added without “breaking” this test. |
| `LhdnDocumentValidatedIntegrationEventHandlerTests.Valid_ConsolidationRef_UpdatesAllRows_DoesNotGenerateReceiptPdfs` | After cons VALID, **every** receipt row `LhdnValidationStatus == Valid`. The skip of QR regen is tested. The badge lie is the asserted outcome. |

Those three are the most expensive. Fixing B06-D02 / D06 requires changing tests that today look like coverage.

### 6.2 Tests that miss the bug they are named for

| Test | Gap |
|------|-----|
| `CheckoutB2bIdentityTests.InitiateCheckout_CustomSession_CopiesIsB2bRequiredIntoMetadata` | Name says identity; body only checks gateway metadata. Session CRM arity (B06-D01) is unasserted. |
| `CheckoutB2bIdentityTests.InitiateCheckout_RequiresTaxId_WithTinAndCompany_ResolvesCrmWithoutIdValue_AndStampsB2b` | Method name says “WithoutIdValue”; body **asserts** `IdValue == "202401001234"`. Stale name. Product path is actually covered. |
| `B2cConsolidationJobTests.SecondRun_SamePeriod_IsIdempotent` | Double-run before VALID. Does not overwrite `TaxInvoiceId`. Does not run under empty-tenant filter. Does not prove `alreadyConsolidated`. |
| `MyInvoisLoopTests.InvoiceIssuedHandler_DoesNotSubmitStubTin` | Only `NotThrowAsync`. InvoiceIssued never submitted anything. Does not prove stub TIN is refused on the live hook (that is a different test, and it exists). |
| `LhdnSingleCreditPathTests` | Buyer TIN `IG1234567890`; TIN service stubbed valid. Proves credit amount, not stub refusal, not deduct-failure fail-open. |
| `LhdnSandboxE2ETests` | `[Ignore]`. Token + poll known uid. Comment says it proves “document status polling.” It does not submit. It cannot prove VALID. |
| `MyInvoisLoopTests.JsonSigner_WithSelfSignedCert_EmitsNonPlaceholderSignature` | Honest as a unit test. Dangerous if cited as MyInvois ACCEPT. |
| `GatewayRefundCompletedIntegrationEventHandlerTests.FullRefund_After72h_SubmitsCreditNoteWithCrmTin` | Does not assert `Total_including_tax` or original document number. Green while B06-D19 is live. |
| `B2bTaxInvoiceRequestedIntegrationEventHandlerTests.RealTin_SubmitsType01WithInvoiceNumber` | Does not assert `Tax_rate` scale or line description. Green while B06-D09 / D11 are live. |
| `GenerateAndStoreDocumentCommandHandlerTests` | Lookup is substituted. Never exercises the transaction-log short-circuit. Green while B06-D03 is live. |
| `InvoiceDocumentFactoryTests.CreateHeader_MapsSstSsmAndFullAddress` | Passes a **full** `CommerceCustomerDisplay`. Proves the factory *can* print TIN. Does not prove the lookup delivers it. |

### 6.3 Tests that are honest and should stay

- `MyInvoisLoopTests.StandardInvoiceXml_UsesTenantCity_NotMerdeka` — supplier address is tenant config.
- `MyInvoisLoopTests.GetDocument_Pending_HasNoQr` / `GetDocument_Valid_HasShareQr` — QR gate on VALID (on the **query**, assuming `LhdnUuid` was stored).
- `MyInvoisLoopTests.Submit_Type01InvalidTin_ThrowsAndDoesNotPersist` — submit-time TIN re-check.
- `MyInvoisLoopTests.Submit_GeneralPublic_DoesNotValidateTin` — cons path.
- `B2bTaxInvoiceRequestedIntegrationEventHandlerTests.MissingTin_DoesNotSubmit` / `StubTin_DoesNotSubmit` — mapper skip. Honest.
- `GatewayRefundCompletedIntegrationEventHandlerTests.PartialRefund_DoesNotCancelDocument` — documents B06-D20 on purpose.
- `InvoiceDocumentFactoryTests.OfficialReceiptDisclaimer_OnlyForReceipts` — Official Receipt honesty.
- `CancelTaxDocumentCommandTests.After72Hours_DomainRefuses` — window.
- `TaxDocumentClaimLeaseTests` — lease mechanics, not MyInvois.

### 6.4 What “green CI” does not mean

A green `Lazuar.ModuleTests` Lhdn + Billing documents run means: our state machine, our XML city string, our credit deduct **call**, our QR **omission** on PENDING. It does not mean `acceptedDocuments`, `overallStatus=Valid`, a scannable QR, or a Tax Invoice PDF that names the buyer.

---

## 7. Unread / not verified in this pass

These were not fully walked. Absence from the catalog is not a clean bill.

- Full OASIS XSD / Schematron review of every template (`StandardInvoice.xml`, `ConsolidatedInvoice.xml`, `CreditNote.xml`, `SelfBilledInvoice.xml`, `SelfBilledCreditNote.xml`) line-by-line against the current IRBM kit.
- `UblValidatorService` resolver edge cases and whether JSON 1.1 is ever XSD-validated (it is not — only unsigned XML 1.0 hits the validator, `SubmitTaxDocumentCommand.cs:264–271`).
- `LhdnInboxConsumerJob` / `LhdnOutboxPublisherJob` retry/DLQ behaviour beyond “the handlers exist.”
- TypeSpec vs Minimal for `/lhdn/*` and `/public/billing/*` (belongs to report 10 if it is a contract lie).
- Hub SaaS `SAAS-` stationery (`PlatformSaasInvoiceFactory`) except to note it is a different seller and out of merchant GMV.
- Invoice reminder email amounts (008 comms report already has the MYR / 0.00 SST issue).
- Live sandbox HTTP. No credentials were used. Scripts were read, not run.
- Whether `IExecutionContextAccessor.IsTestMode` is true in any hosted environment that merchants can reach.
- R2 object lifetime / overwrite of `vault/{org}/documents/{ledgerId}.pdf` on VALID regen (same key — good — but no audit of old bytes).
- Ops Legal profile PUT / `SyncSupplierStationeryCommand` field-by-field beyond the 008 description (stationery sync still does not touch secrets; that part was not re-diffed line by line).
- `LhdnStuckMetricsContributor` thresholds.
- Portal `/pay/{id}` CSS / i18n.
- Debit note / refund note XML when `doc_type_code` is 03/04 — template injection exists; no live caller.

---

## 8. Hunt checklist — verdicts

| Hunt item | Verdict at `297ba98` |
|-----------|----------------------|
| Quote B2B identity wrong | **Confirmed.** B06-D01, D04, D05. |
| `INV-` issued as if VALID | **Confirmed.** B06-D02, D06. PDF title + portal type. Status badge is usually `NOT REQUIRED`, not the string `VALID`, unless cons inheritance paints receipts. UI does **not** print `VALID` on a pre-submit INV- unless a human misreads the title. |
| Poll never advances | **Mostly refuted as a hang.** Poller does SUBMITTED → VALID/INVALID. 404 retries. Real poll bugs are UUID not written back (B06-D14) and QR host (B06-D15). Quote/B2B skip means poll **never starts**. |
| TIN validate false-accept | **Confirmed on 200+garbage.** B06-D12. 404 is correctly invalid. Stub lists disagree (B06-D13). |
| Credit note against wrong doc | **Partially confirmed.** Candidate walk is usually the right INV-/UUID. Real failures: second `CN-` (B06-D21), type 02 math (B06-D19), partial skip (B06-D20), cancel+refund double row (B06-D22), `ID=NA`. |
| Number series collision | **No cross-series collision.** `QT`/`RCPT`/`INV`/`CN` are separate. Real issues: gap comment lie; Lhdn vs Billing double `CN-`; non-unique `TaxDocument` internal id; year rollover is a new prefix (fine). |
| QR payload wrong | **Host wrong for prod** (always preprod default). Path `{uuid}/share/{longId}` is the official shape. Ops third-party QR renderer. No in-repo scan. GET QR empty if submit missed uuid (B06-D14). |
| Submit without credits | **Confirmed fail-open after persist.** Pre-check exists. Deduct failure does not roll back the TaxDocument. Test mode skips. |
| Self-bill vs normal mixup | **No live self-bill product.** Entity swap is real. Empty TIN on type 01 selects cons strategy (mixup on the integrator door). README overclaims 11–14. |
| Document shown to wrong tenant | **Not found as a cross-tenant PDF leak.** HMAC binds slug. Portal union is same-tenant same-email. Admin presign uses `ctx.TenantId`. |
| Receipt disclaimer missing vs present | **Present on Official Receipt** (factory + footer). **Absent on Tax Invoice** (intentional, tested). Official Receipt ≠ MyInvois holds. |

---

## 9. Ranked open bugs

Order is what a lawyer or a sandbox run would hit first.

1. **B06-D01 + D04 + D05** — Fix quote B2B CRM arity (named args). Collect ID type/value + `validate-tin` on QuoteView. Stop putting client name only in `FullName`. Allow CRM `IdValue` to be corrected. Until this lands, “B2B tax invoice after payment” on a quote is a lie.
2. **B06-D02 + D06** — Do not title a PDF “Tax Invoice” until `LhdnValidationStatus == VALID`. First B2B document should be a payment confirmation / Official Receipt / “Tax invoice (pending MyInvois).” Regen on VALID can say Tax Invoice and print UUID + QR. Change the three tests in §6.1. Portal `Classify` must not say Tax Invoice on a pre-clearance `INV-`.
3. **B06-D03** — Stop returning the transaction-log stub when a CRM profile exists for that email. Merge TIN / company / address. A Tax Invoice PDF without buyer TIN is worse than an Official Receipt.
4. **B06-D09 + D10 + D11** — Percent scale, resolved SST on the event, real line items. Otherwise the first product B2B with SST is INVALID even if identity is perfect.
5. **B06-D19 + D20 + D21** — Type 02 math; generate a Credit Note PDF when `CN-` is allocated; do not mint a second series value; decide what a partial refund is at LHDN.
6. **B06-D12 + D13** — 200+unparseable body is **not** valid. Unify stub TIN lists. Do not cache garbage.
7. **B06-D14 + D15 + D16** — Write poll UUID onto `TaxDocument`. Wire `Environment` to `GetBaseUrl` / `GetPortalUrl`. Stop sending LongId to `api.qrserver.com`.
8. **B06-D24** — `IgnoreQueryFilters` on `alreadyConsolidated`. Stable cons idempotency key (`b2c-cons:{org}:{yyyyMM}`). Do not store the cons ref only on `TaxInvoiceId` if VALID clobbers it. Do not paint every `RCPT-` `VALID`. Banner search must survive clearance.
9. **B06-D17 + D18** — Deduct must be in the same success path as persist, or persist must roll back. Stop calling Lazuar 200 “accepted.” Decide whether product B2B checkout may proceed without MyInvois (commercial INV- only) or not; today quote can and product cannot.
10. **B06-D07, D27, D28, D35, sequence comment** — Delete leftover lies. They are cheap and they get repeated in demos.

After (1)–(4), a founder can sit next to a Malaysian merchant, take a B2B **product** payment, and either show a **real** UUID or honestly stay on Official Receipts. Quote B2B should not be demoed at all until (1) is done.

Do **not** sell buyer reject, debit notes, self-billed, quote AR, or “we filed your January B2C cons” without watching SUBMITTED→VALID on the remounted panel **and** replacing `docs/honesty/lhdn-sandbox-valid.md`.

---

## 10. File:line index (quick)

| Topic | Evidence |
|-------|----------|
| Quote CRM arity | `InitiateCheckoutCommandHandler.cs:134–142` |
| Product CRM named args | `InitiateCheckoutCommandHandler.cs:198–208` |
| Quote create FullName | `CreateCustomCheckoutCommandHandler.cs:30–35` |
| CRM enrich-only | `ResolveClientProfileCommandHandler.cs:45–58` |
| QuoteView TIN only | `QuoteView.tsx:36–55`, `163–179` |
| Product TIN validate | `CheckoutForm.tsx:96–110` |
| ProductForm lie | `ProductForm.tsx:221–222` |
| Tax Invoice on pay | `GatewayPaymentCompletedHandler.cs:119–126` |
| OR disclaimer | `InvoiceDocumentFactory.cs:90–93`, `BaseInvoiceDocument.cs:185–191` |
| Log short-circuit | `CommerceDocumentLookup.cs:98–102`, `157–161` |
| Portal Classify | `PortalDocumentQueryService.cs:189–200` |
| Offline product B2C | `MarkCheckoutAsPaidOfflineCommandHandler.cs:166–175` |
| Tax rate fraction | `B2bTaxInvoiceRequestedIntegrationEventHandler.cs:82` |
| Synthetic line | same file `:74–88` |
| TIN 200=valid | `LhdnGatewayAdapter.Tin.cs:27–38` |
| Stub lists | `MyInvoisBuyerRules.cs:16–17` vs `LhdnBuyerMapper.cs:11–16` |
| Poll UUID | `LhdnStatusPollingJob.cs:89–99`, `TaxDocument.cs:91–98` |
| QR host | `LhdnLinkService.cs:15–18` |
| GetBaseUrl | `LhdnGatewayAdapter.cs:44–47` |
| Deduct fail-open | `SubmitTaxDocumentCommand.cs:152–169` |
| accepted_for_processing | `DocumentEndpoints.cs:37–38` |
| Type 02 totals | `GatewayRefundCompletedIntegrationEventHandler.cs:119–136` |
| Partial skip | same file `:49–55` |
| Second CN | same file `:173–184` |
| No CN PDF | `GatewayRefundCompletedHandler.cs` (no `GenerateAndStore`) |
| Cons alreadyConsolidated | `B2cConsolidationJob.cs:209–211` |
| Cons Guid key | `ConsolidatedInvoiceIssuedIntegrationEventHandler.cs:57` |
| TaxInvoiceId clobber | `LedgerEntry.cs:140–147` |
| Sequence comment | `GenerateNextSequenceNumberCommandHandler.cs:26–27` |
| Non-unique internal id | `LhdnDbContext.cs:76` |
| Dead InvoiceIssued | `InvoiceIssuedIntegrationEventHandler.cs:8–25` |
| Country MY | `CheckoutForm.tsx:53` |
| Honesty fence | `docs/honesty/lhdn-sandbox-valid.md` |

---

*End of report 06. Uncondensed. Code at `297ba98`. No fixes applied.*
