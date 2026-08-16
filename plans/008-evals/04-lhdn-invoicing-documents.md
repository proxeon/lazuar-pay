# 04 — LHDN invoicing + documents (after Wave 2 un-hide)

**Program:** `plans/008-evals`  
**Date:** 16 August 2026  
**Branch (parent):** `feat/007-waves-1-4-implement` (see [README.md](./README.md))  
**Workspace:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay`  
**This file is evidence.** It evaluates the **code as it is after Waves 0–4**, not the August 16 competitor inventory in `plans/007-feats`. Those Wave 2 `*-done.md` notes are treated as claims to re-check, not as truth.

**Does not implement product code.**

**Standing constraints (do not contradict):**

- Lazuar Pay is **BYOK software**, not a Merchant of Record and not an acquiring bank.
- Buyer money on Billplz / Stripe / CHIP (tenant keys) is not Lazuar’s SaaS fee.
- A QuestPDF titled “Tax Invoice” is **not** an LHDN-validated e-invoice.
- A hidden page is not a shipped job. After Wave 2, invoicing **is remounted**. Leftover `[MVP-HIDE]` is **ops chat only**.
- A unit test that asserts `SubmitTaxDocumentCommand` was sent is **not** MyInvois `acceptedDocuments`.
- `InvoiceIssuedIntegrationEvent` is still an orphan. The live B2B submit hook is `B2bTaxInvoiceRequestedIntegrationEvent`.
- Paddle / Aura / System A invoices are a different seller. This file is Pay’s tenant document stack.

---

## 0. How to read this file

Wave 2 in [00-evaluation.md](../007-feats/00-evaluation.md) was:

> Un-hide invoicing + legal profile. Checkout TIN / company. Submit → poll VALID/INVALID → QR on receipt → buyer download. B2C consolidation visible. Credit/debit/refund notes tied to original UUID. V1.1 signing when we have a real `.p12`. SST codes on lines.

ADR 023 said reactivation is “remove the `[MVP-HIDE]` comments.” That part is **true for ops nav and portal routes**. It is **not** true that un-hide produced a sellable MyInvois product.

This report answers one product question:

> After Wave 2 remounted the pages, what can a merchant or buyer actually click, what does the backend honestly do, and what is still a lie if we demo “Compliance CaaS”?

Honesty rules used:

1. **Code wins** over `W2-LP-*-done.md`, the Lhdn README, and ADR 021 slogans.
2. **A remounted ledger list is not a MyInvois inbox.** Ops “Sales documents” is `GET /admin/billing/ledger?type_filter=sales`.
3. **A PDF title is not a legal type.** `DocumentType = "Tax Invoice"` on QuestPDF is stationery. Type `01` + MyInvois UUID + LongId + QR is the legal object.
4. **An event handler is not a flow** if the publisher is missing, or if the handler skips when CRM identity is incomplete.
5. **Sandbox scripts that print “VALIDATED” are not evidence** unless a run log exists. None exists in this repo.
6. **Positional constructor arguments are evidence.** Quote B2B still puts company name in the wrong CRM slot.

---

## 1. Absolute paths (live anchors)

| Concern | Path |
|---------|------|
| Ops routes | `apps/lazuar-ops/src/App.tsx` |
| Ops sidebar | `apps/lazuar-ops/src/components/Sidebar.tsx` |
| Quotes UI | `apps/lazuar-ops/src/modules/invoicing/pages/QuotesPage.tsx` |
| Create quote | `apps/lazuar-ops/src/modules/invoicing/components/CreateQuoteModal.tsx` |
| Quote detail | `apps/lazuar-ops/src/modules/invoicing/components/QuoteDetailPanel.tsx` |
| Sales documents | `apps/lazuar-ops/src/modules/invoicing/pages/TaxInvoicesPage.tsx` |
| Credit notes UI | `apps/lazuar-ops/src/modules/invoicing/pages/CreditNotesPage.tsx` |
| Tax document panel | `apps/lazuar-ops/src/modules/invoicing/components/TaxInvoiceDetailPanel.tsx` |
| Legal profile | `apps/lazuar-ops/src/modules/workspace/pages/BillingProfilePage.tsx` |
| Product B2B + SST | `apps/lazuar-ops/src/modules/commerce/components/ProductForm.tsx` |
| Portal `/pay/{id}` | `apps/lazuar-portal/src/app/[tenantSlug]/pay/[sessionId]/page.tsx` |
| QuoteView | `apps/lazuar-portal/src/modules/checkout/components/QuoteView.tsx` |
| Checkout TIN | `apps/lazuar-portal/src/modules/checkout/components/CheckoutForm.tsx` |
| Portal documents | `apps/lazuar-portal/src/app/[tenantSlug]/portal/page.tsx` |
| Portal documents API | `apps/lazuar-api/Modules/Commerce/Infrastructure/Services/PortalDocumentQueryService.cs` |
| Sequences | `Modules/Billing/Contracts/DocumentSeries.cs`, `…/Commands/GenerateNextSequenceNumberCommandHandler.cs` |
| QuestPDF | `Modules/Billing/Infrastructure/Documents/` |
| Pay → ledger + PDF | `Modules/Billing/Infrastructure/EventHandlers/GatewayPaymentCompletedHandler.cs` |
| B2B MyInvois request | `Modules/Billing/Contracts/Events/B2bTaxInvoiceRequestedIntegrationEvent.cs` |
| B2B submit consumer | `Modules/Lhdn/Infrastructure/EventHandlers/B2bTaxInvoiceRequestedIntegrationEventHandler.cs` |
| Submit | `Modules/Lhdn/Application/Commands/SubmitTaxDocumentCommand.cs` |
| Submit worker | `Modules/Lhdn/Infrastructure/Workers/LhdnSubmissionJob.cs` |
| Poll worker | `Modules/Lhdn/Infrastructure/Workers/LhdnStatusPollingJob.cs` |
| Gateway submit / status / TIN | `Modules/Lhdn/Infrastructure/Gateways/LhdnGatewayAdapter*.cs` |
| JSON 1.1 signer | `Modules/Lhdn/Infrastructure/Services/JsonUblDocumentSigner.cs` |
| TIN service | `Modules/Lhdn/Infrastructure/Services/TaxpayerValidationService.cs` |
| Public TIN | `Modules/Lhdn/Infrastructure/Endpoints/PublicTinValidationEndpoints.cs` |
| Cancel | `Modules/Lhdn/Application/Commands/CancelTaxDocumentCommand.cs`, `Domain/Rules/CancelWindowMustBeValidRule.cs` |
| B2C job | `Modules/Billing/Infrastructure/Workers/B2cConsolidationJob.cs` |
| CRM resolve | `Modules/CRM/Contracts/ResolveClientProfileCommand.cs`, `…/ResolveClientProfileCommandHandler.cs` |
| Custom checkout create | `Modules/Commerce/Application/Commands/CreateCustomCheckoutCommandHandler.cs` |
| Checkout initiate | `Modules/Commerce/Application/Commands/InitiateCheckoutCommandHandler.cs` |
| Customer for PDF | `Modules/Commerce/Infrastructure/Services/CommerceDocumentLookup.cs` |
| Buyer mapper | `Modules/Lhdn/Infrastructure/Services/LhdnBuyerMapper.cs` |
| Sandbox scripts | `scripts/lhdn_sandbox/` |
| Ignored live E2E | `apps/lazuar-api/tests/Lazuar.ModuleTests/Lhdn/LhdnSandboxE2ETests.cs` |
| ADR 023 | `docs/architecture-decision-log/023-pure-caas-mvp-ui-lobotomy.md` |
| XML-DSig pain | `docs/lhdn/000-xml-vs-json.md`, `001-xml-vs-json-2.md` |

---

## 2. Wave 2 un-hide: what actually remounted

### 2.1 Ops

`App.tsx` now mounts invoicing + legal profile. The file comment still talks about ADR 023 floating islands, then admits they are remounted:

```166:175:apps/lazuar-ops/src/App.tsx
/**
 * Ops routes (Pure CaaS MVP — ADR 023).
 *
 * Intentionally unrouted "floating islands":
 * - components/OpsChatWorkspace + ConversationsDirectory (ops AI chat)
 * Legal & Billing profile is remounted (LP-122). Invoicing pages remounted (Wave 2).
 *
 * Re-mount by adding Route entries + Sidebar links; do not delete backends.
 * See docs/contracts/openapi-vs-minimal-api.md and ADR 023.
 */
```

Live routes:

```232:244:apps/lazuar-ops/src/App.tsx
        <Route path="/workspace/billing-profile" element={<BillingProfilePage />} />
        ...
        <Route path="/invoicing/quotes" element={<QuotesPage />} />
        <Route path="/invoicing/tax-invoices" element={<TaxInvoicesPage />} />
        <Route path="/invoicing/credit-notes" element={<CreditNotesPage />} />

        {/* [MVP-HIDE] ADR 023 — ops chat remains disconnected
        <Route path="/ops/chat" element={<OpsChatWorkspace />} />
        */}
```

Sidebar has an Invoicing module. The tax-invoice list is **not** labelled “Tax Invoices” in nav; it is “Sales documents”:

```26:31:apps/lazuar-ops/src/components/Sidebar.tsx
const MODULES = [
  { id: "commerce", title: "Commerce", basePath: ["/commerce"], icon: ShoppingCart },
  { id: "invoicing", title: "Invoicing", basePath: ["/invoicing"], icon: FileText },
```

```259:275:apps/lazuar-ops/src/components/Sidebar.tsx
                ] : mod.id === "invoicing" ? [
                  { label: "Quotes", href: "/invoicing/quotes" },
                  { label: "Sales documents", href: "/invoicing/tax-invoices" },
                  { label: "Credit Notes", href: "/invoicing/credit-notes" }
                ] : ...
                  { label: "Legal & Billing", href: "/workspace/billing-profile" },
```

That nav label is one of the few honesty wins. The empty-state string on the same page still says “No tax invoices found” (`TaxInvoicesPage.tsx:169`).

### 2.2 Portal

`/{tenantSlug}/pay/{sessionId}` is **not** `notFound()`. It loads the custom checkout and renders `QuoteView`:

```16:42:apps/lazuar-portal/src/app/[tenantSlug]/pay/[sessionId]/page.tsx
  const { data: checkout, error: checkoutError } = await serverClient.GET("/public/commerce/{tenantSlug}/custom-checkouts/{sessionId}", {
    params: { path: { tenantSlug, sessionId } },
    next: { revalidate: 0 }
  });

  if (checkoutError || !checkout) {
    notFound();
  }
  ...
      <QuoteView
        tenantSlug={tenantSlug}
        checkout={checkout}
        branding={branding}
        profile={checkout.is_b2b_required ? profile : null}
        isCancelled={isCancelled}
      />
```

Buyer portal no longer has a commented “Download Tax Invoice” fake href. It has:

1. Per-subscription `document_url` / `document_label` when the query service attached a latest receipt or tax invoice (`portal/page.tsx:123–127`).
2. A **Documents** table from `GET /public/commerce/{tenantSlug}/portal/documents` (`portal/page.tsx:48–52`, `200–237`).

### 2.3 Leftover `[MVP-HIDE]`

Workspace search of `*.tsx` / `*.ts` / `*.cs` finds **one** remaining marker: ops chat in `App.tsx:242`. Invoicing, legal profile, checkout TIN, `/pay/{id}`, and portal downloads are live routes.

Historical 007 reports that still say “quotes are 404” are **stale**. This file supersedes them for 008.

### 2.4 What Wave 2 `*-done.md` claimed vs what “Y” still required

The impl notes themselves never claimed a sandbox clearance:

| Ticket | Done-file claim | Gate they left open |
|--------|-----------------|---------------------|
| LP-102 | Quotes remounted, `/pay/{id}` restored | — (they marked Y) |
| LP-101 | `QT-` / `RCPT-` / `INV-` / `CN-` series | — (they marked Y) |
| LP-103 | Tax Invoice PDF on **pay**, not only VALID | Y needs 110/111/022 + sandbox VALID + QR |
| LP-104 | 72h cancel / type 02 after window | Y when sandbox cancel/CN is proven |
| LP-106 / 175 | HMAC downloads + history table | — (they marked Y together) |
| LP-110 | CRM-backed submit, no stub TIN | Y when a real-TIN checkout lands PENDING/SUBMITTED |
| LP-111 | Poller owns VALID/INVALID | Y when sandbox VALID shows on ops **without SQL** |
| LP-112 | Checkout TIN + ID pair + re-validate on submit | Y on sandbox checkout with a real TIN/ID pair |
| LP-113 | QR only when VALID | Y when a sandbox VALID PDF shows a scannable QR |
| LP-114 | RM 10k threshold + 28th job + banner | P; banner shipped |
| LP-116 | Cancel uses document number + `validated_at` | P (cancel Y, **buyer reject N**) |
| LP-117 | Default unsigned XML 1.0; JSON 1.1 if Auto + `.p12` | Y **only after sandbox accepts JSON 1.1** |
| LP-118 | Product SST 02/06 | Y after a merchant marks a link as service tax |
| LP-122 | Legal profile remounted | Y for editor + UBL address |

Those “Y when …” sentences are still unpaid. There is no `plans/007-feats/impl` evidence file, no `scripts/lhdn_sandbox` log, and no un-ignored test that submitted a document and saw `acceptedDocuments`.

---

## 3. Quotes / proforma / `/pay/{id}`

### 3.1 What a quote is (and is not)

A Lazuar “quote” is a Commerce `CheckoutSession` with `ProductId == null` and `AdHocLineItems`. It is **not**:

- an AR invoice with a due date the buyer can pay in parts
- a Chargebee/Xero accepted quote that converts to a subscription
- an LHDN document

Ops copy is mostly honest: “Quotes & Proforma Invoices” (`QuotesPage.tsx:42–43`). The table still has a leftover “Tracking ad-hoc invoices” (`QuotesPage.tsx:57`) — that word “invoices” should not be there.

Create flow: `POST /admin/commerce/custom-checkouts` from `CreateQuoteModal.tsx:42–44`. Payload is client name, email, optional expiry, `terms` (`due_on_receipt` / `net_7` / `net_15` / `net_30`), `is_b2b_required`, and line items (`CreateQuoteModal.tsx:89–100`).

Payment terms **are persisted** (`CreateCustomCheckoutCommandHandler.cs:41–48`, `ResolveDueAt` at `104–121`). QuoteView **does not show** `due_at` or terms. The buyer page is “Total Due” + “Proceed to Payment” (`QuoteView.tsx:189–217`). There is no Net-30 AR reminder job. LP-105 (payment terms / AR reminders) is Wave 3 and still absent. **Job 2 from the four-job model (commercial invoice / payment request with balance due) is still not closed.** A quote is a hosted payment request that expires, not an open invoice.

### 3.2 Numbering and draft PDF

On create, Billing allocates `QT-yyyy-#####` and stamps it on the session:

```70:73:apps/lazuar-api/Modules/Commerce/Application/Commands/CreateCustomCheckoutCommandHandler.cs
        var quoteNumber = await _mediator.Send(
            new GenerateNextSequenceNumberCommand(request.OrganizationId, DocumentSeries.QuotePrefix()),
            ct);
        session.AssignDocumentNumber(quoteNumber);
```

Public GET attaches an HMAC draft URL (`PublicCustomCheckoutEndpoints.cs:35–40`). Draft QuestPDF title is **"Proforma Invoice"** (`GenerateDraftDocumentQueryHandler.cs:70–77`). QuoteView heading is also “Proforma Invoice” (`QuoteView.tsx:121–122`). That pairing is honest.

Draft customer block is **only** session CRM `FullName` + email. The draft handler builds a `CommerceCustomerDisplay` from `sessionData.CustomerName` / `CustomerEmail` and does not pass TIN (`GenerateDraftDocumentQueryHandler.cs:66–77`). `GetDraftCheckoutSessionAsync` reads `profile?.Full_name` (`CommerceDocumentLookup.cs:86–89`). So a merchant who typed “Acme Corp” as client name will see Acme as the billed party on the proforma — and that string lives in `ClientProfile.FullName`, not `CompanyName`. See §11.

### 3.3 Buyer pay path

QuoteDetailPanel copies `{VITE_PORTAL_URL}/{slug}/pay/{sessionId}` (`QuoteDetailPanel.tsx:35–38`). That URL now 200s.

QuoteView pay:

```36:55:apps/lazuar-portal/src/modules/checkout/components/QuoteView.tsx
  const handleProceedToPayment = async () => {
    if (checkout.is_b2b_required && !taxId.trim()) {
      setGlobalError("Company tax ID (TIN) is required for this payment request.");
      return;
    }
    ...
      const payload = {
        tenant_slug: tenantSlug,
        product_slug: "custom",
        session_id: checkout.id,
        name: checkout.client_name || "Customer",
        email: checkout.client_email || "customer@example.com",
        company_name: checkout.is_b2b_required ? companyName.trim() || undefined : undefined,
        tax_id: checkout.is_b2b_required ? taxId.trim() : undefined,
        is_guest_checkout: true
      };
```

Missing versus product checkout (`CheckoutForm.tsx:79–82`, `96–110`):

- no `id_type` / `id_value`
- no `POST /public/commerce/{slug}/validate-tin`
- company name is **optional** (only TIN is required)
- country / address not collected

Initiate with `session_id` stamps `is_b2b_required` onto gateway metadata (`InitiateCheckoutCommandHandler.cs:106–112`) and refuses a missing TIN (`116–119`). That is enough to book the ledger as B2B. It is **not** enough to file type `01`. See §11 and §5.

### 3.4 Mark as paid (bank transfer)

Ops “Mark as Paid (Bank Transfer)” posts `/admin/commerce/checkouts/{id}/mark-paid` (`QuoteDetailPanel.tsx:51–56`). Custom sessions complete, write an `OFFLINE-…` transaction log, and publish `ManualSubscriberEnrolledIntegrationEvent` with `session.IsB2bRequired` (`MarkCheckoutAsPaidOfflineCommandHandler.cs:207–220`). Billing then allocates `INV-` or `RCPT-` and, if B2B, publishes `B2bTaxInvoiceRequested` (`ManualSubscriberEnrolledIntegrationEventHandler.cs:57–89`).

Product-session offline pay **does not pass** `IsB2bRequired` (`MarkCheckoutAsPaidOfflineCommandHandler.cs:166–175` — the bool defaults to `false` on the event). A product flagged “Require Company Name & Tax ID” that is marked paid offline is booked **B2C**. That is a lie if the merchant thought they issued a B2B tax invoice.

Custom quotes do **not** create a `Subscription`. Success is `/checkout/custom/success` (`InitiateCheckoutCommandHandler.cs:103`, `checkout/custom/success/page.tsx`). “Open buyer portal” on a completed quote (`QuoteView.tsx:96–98`) goes to `/{slug}/portal` **without a token**. Portal documents require a magic-link token bound to a **subscription id** (`PublicPortalEndpoints.cs:36–38`, `58–59`). A quote-only buyer with no subscription cannot open document history. Demoable only if that email already has a subscription.

### 3.5 Verdict — quotes

| Claim | After Wave 2 |
|-------|----------------|
| Merchant can create a proforma and copy a pay link | **Demoable** |
| Buyer can open `/pay/{id}` and pay | **Demoable** |
| Draft PDF says Proforma, not Tax Invoice | **Honest** |
| Quote is an AR invoice with reminders / partials | **Lie** |
| B2B quote → validated e-invoice | **Lie** (identity + TIN/ID + submit; §11, §5) |
| Quote-only buyer downloads from portal history | **Mostly lie** (needs a subscription magic link) |

---

## 4. Official Receipt vs Tax Invoice honesty

This is the load-bearing distinction. Mixing them is how a demo becomes a desk-audit problem.

### 4.1 The four jobs (still true after un-hide)

| Job | After Wave 2 |
|-----|----------------|
| **1. Quote / proforma** | Remounted. Commercial offer. No legal force. |
| **2. Commercial invoice / AR** | **Still not built.** Terms exist on the session. No open balance, no reminder, no partial. |
| **3. Official Receipt** | Silent on every non-B2B cleared payment. Sequence `RCPT-yyyy-#####`. QuestPDF + disclaimer. |
| **4. LHDN e-invoice** | Backend path exists. Product B2B can *request* type `01`. Clearance is unproven. Quote B2B is broken. PDF titled Tax Invoice is issued **before** VALID. |

### 4.2 What pay actually prints

`GatewayPaymentCompletedHandler` is the spine.

B2C (`is_b2b_required` metadata ≠ `"true"`):

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

```100:137:apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/GatewayPaymentCompletedHandler.cs
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

So on the **gateway paid** event, before MyInvois has said anything, the customer already has a PDF whose H1 is **Tax Invoice**. W2-LP-103-done.md states this as intentional: “B2B paid sales get an `INV-` number and a Tax Invoice PDF on pay (not only after VALID).”

That is commercially convenient and **legally unsafe** if anyone emails that PDF as the Malaysian tax invoice. MyInvois validation is independent of gateway paid.

### 4.3 What the PDF itself says

Factory + footer **do** tell the truth for Official Receipts:

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

Tax Invoice PDFs get **no** “pending MyInvois validation” note. UUID and QR are only attached when `LhdnValidationStatus == Valid` at generation time (`GenerateAndStoreDocumentCommandHandler.cs:79–82`). First PDF (at pay) has neither. A later regen on VALID can add UUID + QR (`LhdnDocumentValidatedIntegrationEventHandler.cs:48–63`).

W2-LP-107-done.md claims missing billing profile prints “TIN not on file”. The factory does not. Empty TIN is omitted (`InvoiceDocumentFactory.cs:31`, `BaseInvoiceDocument.cs:50–51`). Fallback seller name is workspace name, then `"Merchant"` (`InvoiceDocumentFactory.cs:30`) — not “Lazuar Merchant”. That part of the done-file is overstated.

### 4.4 Buyer identity on the PDF is often empty for B2B

`GenerateAndStoreDocumentCommandHandler` resolves the buyer only via `ICommerceDocumentLookup.GetCustomerForDocumentAsync` (`GenerateAndStoreDocumentCommandHandler.cs:60–63`). That method **returns the transaction log first** if it has an email:

```98:102:apps/lazuar-api/Modules/Commerce/Infrastructure/Services/CommerceDocumentLookup.cs
        var fromLog = await FindCustomerOnTransactionLogAsync(organizationId, referenceId, ct);
        if (fromLog != null && !string.IsNullOrWhiteSpace(fromLog.Email))
        {
            return fromLog;
        }
```

The log constructor only has name + email:

```157:161:apps/lazuar-api/Modules/Commerce/Infrastructure/Services/CommerceDocumentLookup.cs
        return new CommerceCustomerDisplay(
            string.IsNullOrWhiteSpace(log.CustomerName) ? "Customer" : log.CustomerName,
            log.CustomerEmail ?? "",
            null,
            null);
```

Gateway payment `ReferenceId` **is** `GatewayTransactionId` (`GatewayPaymentCompletedHandler.cs:47–48`), which matches `TransactionLogs.ExternalReference`. After a real pay, the log almost always exists. Therefore the Tax Invoice PDF’s “Billed To” is typically the **person name from checkout**, with **no buyer TIN, no company, no address** — even when CRM has them.

The factory *would* print company + TIN if they were on the display object (`InvoiceDocumentFactory.cs:37–41`, `BaseInvoiceDocument.cs:86–97`). The lookup never delivers them on the common path.

LHDN submit does a **second** CRM-by-email lookup (`B2bTaxInvoiceRequestedIntegrationEventHandler.cs:42–46`). MyInvois can have a real buyer while the customer PDF does not. That is a worse lie than “we have no e-invoice”: the PDF says Tax Invoice and names the wrong (or incomplete) buyer.

### 4.5 How ops and portal classify the same row

Ops Sales documents: any ledger sale. Type column is `customer_type` (`TaxInvoicesPage.tsx:194–196`). LHDN badge is `lhdn_validation_status` or **“NOT REQUIRED”** (`207–210`). B2C receipts sit on this page next to B2B invoices. The page title/description is honest (“Official receipts and tax invoices… B2C receipts stay receipts until monthly consolidation”, `116–117`). The empty state is not.

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

A B2B row is labelled Tax Invoice in the buyer table **whether or not** MyInvois is VALID. `lhdn_status` is a separate column (`PortalDocumentQueryService.cs:118`, `portal/page.tsx:224`). A careful buyer can see `B2C_RECEIPT` vs `VALID`. A hurried buyer clicks “Download tax invoice” on a pre-clearance PDF.

After consolidation VALID, individual B2C receipts can inherit status `VALID` because the validator updates **every** matching ledger row (`LhdnDocumentValidatedIntegrationEventHandler.cs:29–44`) and the join includes `TaxInvoiceId == B2C-CONS-…` after `MarkConsolidatedPending`. Those receipts are **not** individually validated e-invoices. The handler skips QR regen for the cons key (`51–53`) — correct — but the status badge on each `RCPT-` can still flip to VALID. That teaches the wrong lesson in ops and portal.

### 4.6 Email honesty

`DocumentPublishedIntegrationEventHandler` prefers templates named Official Receipt / Tax Invoice / Credit Note / Quotation Ready, and **falls back Tax Invoice and Credit Note to the Official Receipt template** (`DocumentPublishedIntegrationEventHandler.cs:38–59`). If the merchant never created a Tax Invoice template, the buyer gets an email that talks like a receipt, linking a PDF titled Tax Invoice. Demoable. Not honest.

### 4.7 Verdict — document types

| Object | Demoable? | Honest? |
|--------|-----------|---------|
| Official Receipt PDF after B2C pay | Yes | Yes (disclaimer + `RCPT-`) |
| Tax Invoice PDF after B2B pay | Yes | **No** until VALID, and buyer TIN is usually missing |
| Tax Invoice PDF after VALID regen | Code exists | Unproven (no sandbox VALID) |
| Calling the ops list “Tax Invoices” | Nav says Sales documents | Mostly honest; empty state + credit-note title still overclaim |
| Credit / Debit Notes page | Lists refunds | **Not** a debit-note product |

---

## 5. Sequential numbers

### 5.1 Series

```9:23:apps/lazuar-api/Modules/Billing/Contracts/DocumentSeries.cs
public static class DocumentSeries
{
    public const string Receipt = "RCPT";
    public const string Quote = "QT";
    public const string Invoice = "INV";
    public const string CreditNote = "CN";

    public static string Prefix(string series, DateTime? utcNow = null) =>
        $"{series}-{(utcNow ?? DateTime.UtcNow):yyyy}";
```

Customer-facing “No:” refuses a raw UUID (`CustomerFacingNumber`, `DocumentSeries.cs:38–46`). PDF uses that helper (`GenerateAndStoreDocumentCommandHandler.cs:73`).

Allocation is `INSERT … ON CONFLICT (OrganizationId, Prefix) DO UPDATE … RETURNING` (`GenerateNextSequenceNumberCommandHandler.cs:28–42`), format `{Prefix}-{value:D5}` so `RCPT-2026-00001`.

Who allocates what:

| Series | Writer |
|--------|--------|
| `QT-yyyy-#####` | `CreateCustomCheckoutCommandHandler` |
| `RCPT-yyyy-#####` | `GatewayPaymentCompletedHandler` (B2C), `ManualSubscriberEnrolledIntegrationEventHandler` (non-B2B) |
| `INV-yyyy-#####` | same two handlers on B2B |
| `CN-yyyy-#####` | `GatewayRefundCompletedHandler` (every refund row) |

LHDN `InternalReferenceId` for type `01` is the `INV-` number (`B2bTaxInvoiceRequestedIntegrationEventHandler.cs:65`). For consolidation it is `B2C-CONS-{yyyyMM}-{orgGuid:N}` (`B2cConsolidationJob.cs:209`). For type `02` it is the refund ledger’s `CN-` (`GatewayRefundCompletedIntegrationEventHandler.cs:173–184`).

`CustomerDocumentNumber` is immutable (`LedgerEntry.cs:72–73`, `102`). `UpdateLhdnStatus` **does** overwrite `TaxInvoiceId` with the UUID (`140–147`). Legacy readers that still key off `TaxInvoiceId` will see a UUID after VALID. Ops display prefers `customer_document_number` first (`TaxInvoicesPage.tsx:172`). Good. The consolidation idempotency check does **not** — see §7.5.

### 5.2 The “no gaps” comment is a lie

```26:27:apps/lazuar-api/Modules/Billing/Infrastructure/Commands/GenerateNextSequenceNumberCommandHandler.cs
        // Atomically upserts and returns the incremented sequence value. 
        // This is safe under concurrency and prevents sequence gaps during rollbacks.
```

The handler opens **its own** SQL connection. It is not in the ledger transaction. A crash after increment and before `SaveChanges` on the ledger **gaps the series**. Concurrent safety is real. Gaplessness is not. Malaysian practice (and LHDN) does not require gapless commercial numbers, so this is a comment lie, not a compliance hole.

Zero-amount 100% coupon checkouts write a ledger row with **no** `RCPT-`/`INV-` (`ZeroAmountCheckoutHandler.cs:27–41`). Portal history will not show a receipt for a free checkout.

### 5.3 Verdict — numbers

Demoable and mostly honest. UUID is not printed as “No:”. Quotes share one `QT-` across HTML and draft PDF. Do not claim audit-grade gapless numbering.

---

## 6. Submit, poll VALID/INVALID, QR, TIN, B2C consolidation, cancel window

### 6.1 How a type `01` is supposed to be born

There is **no** `new InvoiceIssuedIntegrationEvent(` in production code. The only construction is in tests (`MyInvoisLoopTests.cs:40`). Lhdn’s `InvoiceIssuedIntegrationEventHandler` is a no-op that logs “MyInvois submit uses B2bSaleReadyForEinvoice only” (`InvoiceIssuedIntegrationEventHandler.cs:8–26`) — and that name is **stale**; the real event is `B2bTaxInvoiceRequestedIntegrationEvent`.

Live path:

1. Pay (or offline B2B custom) → Billing books `INV-` and publishes `B2bTaxInvoiceRequested`.
2. Lhdn handler maps CRM + Commerce display → `SubmitDocumentRequestDto` type `_01`.
3. `SubmitTaxDocumentCommand` persists `TaxDocument` `PENDING` (meters 3 credits when not test mode; `appsettings.json` `Credits:Costs:LhdnSubmit = 3`).
4. `LhdnSubmissionJob` Base64-encodes `RawXmlContent` (which may be XML **or** JSON), sets `format` via `DetectSubmissionFormat` (`MyInvoisBuyerRules.cs:29–33`), POSTs `/api/v1.0/documentsubmissions`.
5. On HTTP success / 202, reads `acceptedDocuments[0].uuid` (`LhdnGatewayAdapter.Submit.cs:88–94`) and `MarkAsSubmitted`.
6. `LhdnStatusPollingJob` GETs `/api/v1.0/documentsubmissions/{submissionUid}`, maps `overallStatus` to upper-case, treats `VALID` / `INVALID`, otherwise reschedules. 404 from sandbox is “not processed yet”, retry 5s (`LhdnGatewayAdapter.Status.cs:32–37`).
7. VALID publishes `LhdnDocumentValidated` + outbound `invoice.valid`. Billing stamps UUID/status and **re-generates** the PDF with QR (except `B2C-CONS-` keys).

Integrator API: `POST /lhdn/documents` requires `Idempotency-Key` and returns `{ status: "accepted_for_processing" }` (`DocumentEndpoints.cs:23–38`). That “accepted” is **Lazuar’s** 200, not MyInvois `acceptedDocuments`.

### 6.2 What the B2B handler actually files

```64:93:apps/lazuar-api/Modules/Lhdn/Infrastructure/EventHandlers/B2bTaxInvoiceRequestedIntegrationEventHandler.cs
        var payload = new SubmitDocumentRequestDto
        {
            Internal_id = @event.InvoiceNumber,
            Document_type = SubmitDocumentRequestDtoDocument_type._01,
            ...
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

One synthetic line. Not the product name. Not the quote line items. Classification `022` (e-commerce) is hardcoded. Supplier MSIC comes from tenant config (or `"00000"` in the mapper, `ViewModelMapper.cs:42`). Phone is `+60000000000` when missing (`ViewModelMapper.cs:43`, `60`). Empty address becomes `NA` / `00000` / state `17` (`LhdnBuyerMapper.cs:35–42`).

If `TryCreatePayloadBuyer` fails (no TIN, stub TIN, or empty/`NA` id value), the handler **logs and returns** (`57–61`). No `TaxDocument`. No INVALID. Ops stays blank / NOT REQUIRED. The customer already has a Tax Invoice PDF.

Skip conditions in practice:

- Quote B2B: `IdType` is null, `IdValue` is the company name or empty → either skip or TIN-validate a company name as BRN (fails).
- Product B2B: checkout requires `id_type` + `id_value` and validates TIN **if** MyInvois is configured. This is the only path that can honestly reach PENDING.

### 6.3 TIN validate

Two doors:

| Door | Who | Behaviour |
|------|-----|-----------|
| `POST /public/commerce/{tenantSlug}/validate-tin` | Buyer, no integrator scope | Sets tenant on context; 400 “Merchant has not connected MyInvois.” if no creds (`PublicTinValidationEndpoints.cs:19–49`) |
| `POST /lhdn/taxpayer/validate` | Integrator / ops Check TIN | Same command (`DocumentEndpoints.cs:81–98`) |

`TaxpayerValidationService` HMAC-hashes id value with `Lhdn:TinHashSalt`, caches 30d valid / 7d invalid, then GET `/api/v1.0/taxpayer/validate/{tin}?idType=&idValue=` (`TaxpayerValidationService.cs:34–86`, `LhdnGatewayAdapter.Tin.cs:19`). HTTP 200 ⇒ valid (optionally with `name`). 404 ⇒ invalid pair. Other status ⇒ throw.

Product checkout **does** call this before pay (`CheckoutForm.tsx:96–110`). QuoteView **does not**.

Submit re-validates type `01` unless General Public `EI00000000010` + `NA` (`SubmitTaxDocumentCommand.cs:175–214`). Stub TIN `C1234567890` is refused (`MyInvoisBuyerRules.cs:16–17`). Mapper also treats `IG1234567890` and `EI00000000010` as stubs (`LhdnBuyerMapper.cs:11–16`) — the two lists are not identical.

Ops Legal card has Check TIN (`BillingProfilePage.tsx:486–507`).

ProductForm still says:

```221:222:apps/lazuar-ops/src/modules/commerce/components/ProductForm.tsx
                <span className="text-[12px] font-medium text-[#09090b] block">Require Company Name &amp; Tax ID (LHDN B2B)</span>
                <span className="text-[11px] text-[#71717a] block mt-0.5">Collects buyer company + TIN. We do not validate the TIN at checkout.</span>
```

That subtitle is a **leftover lie**. CheckoutForm validates. The form also now collects ID type/value (`CheckoutForm.tsx:227–252`).

### 6.4 QR

QR exists only after VALID:

- Poller builds `{portal}/{uuid}/share/{longId}` (`LhdnStatusPollingJob.cs:93–96`).
- GET `/lhdn/documents/{internalId}` sets `qr_link` only when status is VALID and both ids exist (`LhdnQueries.cs:35–39`).
- Default portal host is **preprod**: `Lhdn:PortalUrl` = `https://preprod.myinvois.hasil.gov.my` (`appsettings.json:63`, `LhdnLinkService.cs:17`).
- Ops panel renders an image via `api.qrserver.com` plus the URL (`TaxInvoiceDetailPanel.tsx:255–266`).
- QuestPDF draws a QRCoder PNG in the footer when `LhdnQrLink` is set (`BaseInvoiceDocument.cs:202–210`).

Official Receipts at pay have no QR. Correct.

There is **no** scanned QR from a real VALID UUID in this repo.

### 6.5 B2C consolidation

Job is registered (`Billing DependencyInjection.cs:81`). Schedule: 28th 02:00 MYT plus catch-up of closed months back 24 months (`B2cConsolidationJob.cs:42–54`, `90–137`). Threshold default RM 10,000 (`appsettings.json:62`, ctor `33`).

Above-threshold B2C at **pay time** is already `NOT_REQUIRED` / `NEEDS_BUYER_TIN` (`GatewayPaymentCompletedHandler.cs:94–98`). The job has the same defense (`B2cConsolidationJob.cs:225–230`). There is **no** product flow that then collects a TIN for those large B2C sales. They sit in ops as `NEEDS BUYER TIN` forever.

Below-threshold rows are batched. No new ledger row is created for the cons document. Existing receipts get `MarkConsolidatedPending(consolidationRef)` which sets `TaxInvoiceId = B2C-CONS-…` and status `CONSOLIDATED_PENDING` (`LedgerEntry.cs:129–135`). Then Lhdn files type `01` as General Public (`ConsolidatedInvoiceIssuedIntegrationEventHandler.cs:23–38`).

Idempotency of the **event** is a new Guid every time (`ConsolidatedInvoiceIssuedIntegrationEventHandler.cs:57`). Dedup is “does any ledger row still have `TaxInvoiceId == B2C-CONS-…`?” (`B2cConsolidationJob.cs:209–218`). After VALID, `UpdateLhdnStatus` **replaces** `TaxInvoiceId` with the UUID (`LedgerEntry.cs:142–147`). The next catch-up **will not see** the cons ref and can publish **another** type `01` for the same month.

Ops banner searches sales ledger for `B2C-CONS-` (`TaxInvoicesPage.tsx:51–61`). That search hits `TaxInvoiceId` / `CustomerDocumentNumber` / `ReferenceId` (`BillingQueryService.cs:51–53`). After VALID, `TaxInvoiceId` is a UUID and `CustomerDocumentNumber` is still `RCPT-…`. **The banner goes blank after the first successful clearance** — the exact moment you would want it.

Copy on the banner is honest about the calendar: “Submitted on the 28th for the prior MYT month. Not a guarantee of the IRBM calendar.” (`TaxInvoicesPage.tsx:125–127`).

### 6.6 Cancel window

Domain rule: 72 hours from `ValidatedAt` (`CancelWindowMustBeValidRule.cs:12–26`). `TaxDocument.Cancel()` requires status VALID (`TaxDocument.cs:116–128`).

Ops panel uses **MyInvois** `validated_at` from GET `/lhdn/documents/{internalId}`, not ledger timestamp (`TaxInvoiceDetailPanel.tsx:124–128`). Cancel POST uses `customer_document_number` (INV-/RCPT-/B2C-CONS-), not the ledger GUID (`30–31`, `51–59`). After 72h the button is replaced with “Cancel window closed — issue a credit note” (`291–293`). Footer: “Supplier cancel only… Buyer reject is not implemented.” (`296–298`).

That last sentence is true. There is no buyer-reject API, no portal reject button, no IRBM reject webhook consumer.

Full refund of a VALID document ≤72h sends `CancelTaxDocumentCommand` (`GatewayRefundCompletedIntegrationEventHandler.cs:68–74`). After 72h it files type `02` with `Original_lhdn_uuid` (`105–139`). Partial refunds **skip LHDN entirely** (`49–55`). Billing still allocates a `CN-` on every refund row (`GatewayRefundCompletedHandler.cs:73–76`). So ops Credit Notes can show a CN number with LHDN “NOT REQUIRED” after a partial refund of a validated invoice. The commercial number exists; the legal note does not.

Cancel of an e-invoice also posts a **second** ledger row `LHDN_CANCELLATION` that mirrors every original line with opposite sign (`LhdnDocumentCancelledIntegrationEventHandler.cs:41–65`). Credit Notes page lists both `GATEWAY_REFUND` and `LHDN_CANCELLATION` (`BillingQueryService.cs:60–62`). A 72h refund+cancel can appear twice.

Type `03` debit and `04` refund notes exist only as factory routing (`DocumentStrategyFactory.cs:33–34`). Refund handler hardcodes `_02`. No ops composer. Page title “Credit & Debit Notes” (`CreditNotesPage.tsx:100`) overclaims.

Self-billed `11`–`14` are strategy-only. No ops product. Affiliate payout is not a live job.

### 6.7 Verdict — MyInvois loop

| Step | Code | Demoable to a merchant | Proven against preprod |
|------|------|------------------------|------------------------|
| Collect TIN + ID on **product** checkout | Yes | Yes | Not in-repo |
| Collect TIN + ID on **quote** | TIN only, ID wrong slot | Partial | No |
| Validate TIN | Yes | Yes if creds exist | Not in-repo |
| Persist PENDING TaxDocument | Yes | Only if mapper + TIN pass | No |
| Submit → `acceptedDocuments` | Gateway parses it | Invisible in ops | **No proof** |
| Poll VALID/INVALID | Yes | Ops polls every 5s when PENDING/SUBMITTED | **No proof** |
| QR on PDF / panel | Yes | Only after VALID | **No proof** |
| B2C cons job | Yes | Banner until first VALID | **No proof** |
| 72h cancel | Yes | Button on VALID docs | **No proof** |
| Buyer reject | No | Explicitly unsold | — |

---

## 7. Signing (`.p12`, JSON UBL 1.1 vs XML XAdES)

### 7.1 What the README still says

Lhdn README §3: signatures unimplemented; wait for Pos Digicert / MSC Trustgate / TM Node `.p12`; templates have `SIGNATURE_PLACEHOLDER` behind `document_version == "1.1"`.

Wave 2 changed the **default path** without updating that README’s tone: default is still unsigned **XML 1.0**, but there is now a JSON 1.1 signer.

### 7.2 Why not XML XAdES

`docs/lhdn/000-xml-vs-json.md` records a real sandbox failure: dummy `.p12` + v1.1 XML → LHDN `Root element is missing`. The analysis (and `001-xml-vs-json-2.md`) is that LHDN’s XML-DSig/C14N/XPath path is brittle; the working community approach is proprietary JSON string-hash + RSA-SHA256 + `format: JSON`.

Wave 2 implemented that pivot.

### 7.3 What the code does

`Lhdn:Signing` default **Off** (`appsettings.json:61`, `LhdnSigningOptions.cs:8–12`).

`SubmitTaxDocumentCommand.RenderDocument` (`217–275`):

1. If caller asked `document_version=1.1` and we cannot sign → 400.
2. If `Signing=Auto` **and** tenant has encrypted PFX + password → `JsonUblDocumentSigner.SignJson`.
3. If that throws and 1.1 was not explicit → log and fall back to unsigned XML 1.0.
4. Unsigned 1.0 that still contains `SIGNATURE_PLACEHOLDER` is refused (the Scriban `if document_version == "1.1"` block keeps 1.0 clean; `StandardInvoice.xml:7–9`).

`JsonUblDocumentSigner`:

```13:50:apps/lazuar-api/Modules/Lhdn/Infrastructure/Services/JsonUblDocumentSigner.cs
/// MyInvois JSON UBL 1.1 signer. Hashes the unsigned JSON (no UBLExtensions), RSA-SHA256 signs,
/// then appends a signature object. XML XAdES is not used — LHDN's XML-DSig path is known-broken.
```

It builds unsigned JSON via `UblJsonDocumentBuilder`, `SignData` SHA256 PKCS1, appends `UBLExtensions` with `SignatureValue` + `X509Certificate` + `DigestValue` (`JsonUblDocumentSigner.cs:29–50`, `UblJsonDocumentBuilder.cs:61–96`). Unit test with a **self-signed** cert asserts `SignatureValue` and no placeholder (`MyInvoisLoopTests.cs:205–219`). That is not MyInvois ACCEPT.

Ops Legal card can upload `.p12` + passphrase to `PUT /lhdn/workspaces/{id}/lhdn-certificate` (`BillingProfilePage.tsx:251–263`, `615–647`). Vault is AES-256-CBC with `Kms:MasterKey` or `Jwt:Secret` (`CertificateVaultService.cs:20–22`). GET config reports `has_certificate`, `signing`, and `submission_kind` `signed_v1.1_json` vs `unsigned_v1.0` (`LhdnQueries.cs:99–103`). UI copy matches (`BillingProfilePage.tsx:617–622`).

### 7.4 What still cannot be claimed

- Default production submits are **unsigned 1.0 XML**. IRBM’s 1.1 mandate / signing requirement is not satisfied by the default.
- JSON 1.1 is **unproven** against preprod. W2-LP-117-done.md: “Y only after sandbox MyInvois accepts JSON 1.1.”
- `05_test_b2b_v1_1.sh` is the script that would prove it. `run_all.sh` does **not** include it (`run_all.sh:7–12` runs 00, 01, 02, 03, 06, 07 — skips 04 cert + 05 v1.1).
- Tenant `Environment` SANDBOX/PROD is stored and shown (`LhdnTenantConfig.cs:15`, `BillingProfilePage.tsx:512–515`) and **never read** by the gateway. `GetBaseUrl()` is always `Lhdn:BaseUrl` or `https://preprod-api.myinvois.hasil.gov.my` (`LhdnGatewayAdapter.cs:44–47`). Flipping the dropdown to Production does not move traffic to `api.myinvois.hasil.gov.my`.
- QR / portal links default to **preprod** even if someone later pointed `BaseUrl` at prod (`LhdnLinkService.cs:17`).

### 7.5 Verdict — signing

Demoable: upload a `.p12`, see “Certificate on file: yes”, see “unsigned v1.0” while Signing=Off.  
Lie: “we do XAdES v1.1.”  
Unproven: “JSON 1.1 is accepted by MyInvois.”

---

## 8. SST

### 8.1 What exists

Products have `SstTaxType` (`06` / `02`) and `SstRatePercent` (`Product.cs`, migration `20260818140000_AddProductSst.cs`). Ops editor exposes them only if Legal profile has an SST number (`ProductForm.tsx:229–257`).

Checkout math:

```8:24:apps/lazuar-api/Modules/Commerce/Application/SstTaxMath.cs
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
```

Exclusive SST is stamped on gateway metadata so Billplz’s `TaxAmount=0` still books `LIABILITY_TAX_PAYABLE` type `02` (`InitiateCheckoutCommandHandler.cs:230–241`, `337–341`; `GatewayPaymentCompletedHandler.ResolveTaxAmount` / `ResolveTaxType` at `159–188`).

PDF / ops label “SST:” when type `02` or SST number + tax > 0 (`GenerateAndStoreDocumentCommandHandler.cs:101–104`, `TaxInvoiceDetailPanel.tsx:211`). UBL emits `schemeID="SST"` when the number is present (`StandardInvoice.xml:33–37`, `UblJsonDocumentBuilder.cs:107–110`).

### 8.2 What does not exist

- Quotes have **no** SST lines. CreateQuoteModal totals `qty * unit_price` only (`CreateQuoteModal.tsx:27–29`). Draft PDF has no tax (`GenerateDraftDocumentQueryHandler.cs:83–84`).
- Manual offline product pay does not run `SstTaxMath`; it books gross = product price (`MarkCheckoutAsPaidOfflineCommandHandler.cs:90–92`, `ManualSubscriberEnrolledIntegrationEventHandler.cs:51–52`).
- No sales-tax type `01`, tourism tax, or export `E` / classification `032`. LP-119 is reserved later.
- Checkout UI still has **no tax line** in the order summary for the buyer (product form is name/email/TIN; SST is baked into the amount sent to the gateway).
- Classification `008` is not used. Lines are `022` or cons `004`. Do not tell an accountant “we send 008 for e-commerce.”

### 8.3 Verdict — SST

Demoable if the merchant fills SST # on Legal & Billing, marks a product `02` + rate, and pays through **product** checkout. Quote path and offline mark-paid will understate tax. Do not sell “SST returns.”

---

## 9. Merchant legal profile

Remounted at `/workspace/billing-profile`. Two cards.

**Card 1 — stationery** (`BillingProfilePage.tsx:296–435`): legal name, TIN, SSM, SST, logo (One presigned PUT), address with MY state codes `01`–`17`. `PUT /admin/billing/profile` then `SyncSupplierStationeryCommand` copies name/TIN/address onto existing `LhdnTenantConfig` **without** touching secret, cert, id type/value, MSIC, environment (`LhdnTenantConfig.SyncStationeryIdentity`, `76–91`; `UpdateTenantBillingProfileCommandHandler.cs:56–64`).

**Card 2 — MyInvois** (`439–658`): supplier TIN, ID type/value, Check TIN, SANDBOX/PROD (cosmetic; see §7.4), MSIC, intermediary + `onbehalfof`, client id/secret, `.p12`.

Public `GET /public/billing/{slug}/profile` still exists and QuoteView uses it **only when** `is_b2b_required` (`pay/[sessionId]/page.tsx:27–32`, `QuoteView.tsx:30–33`). Product checkout branding is still workspace name/logo (LP-025), not the legal TIN — by design, so a consumer checkout does not leak the merchant TIN.

UBL supplier address comes from `LhdnTenantConfig`, not hardcoded Bangunan Merdeka (`ViewModelMapper.cs:35–51`). Merdeka remains only in `docs/xml/**` samples. Tests lock this (`TenantLegalProfileTests`, `MyInvoisLoopTests`).

Placeholder on the TIN field is `e.g. C12345678` (`BillingProfilePage.tsx:328`). That is one digit short of the reserved stub `C1234567890`, but it is a bad example to leave in a compliance form.

If Card 2 was never saved, `SubmitTaxDocumentCommand` throws “LHDN Tenant Configuration is missing.” (`SubmitTaxDocumentCommand.cs:99–103`). Un-hiding the editor does not auto-provision MyInvois creds.

### 9.1 Verdict — legal profile

Demoable as a settings page. Necessary but not sufficient for clearance. PROD dropdown is a lie until `GetBaseUrl` reads tenant environment.

---

## 10. Portal document history

### 10.1 What shipped

`GET /public/commerce/{tenantSlug}/portal/documents?token=` (`PublicPortalEndpoints.cs:48–63`). Token is the same magic-link JWT as the subscription portal. Foreign slug → 404. Bad token → 401.

`PortalDocumentQueryService.ListForBuyerAsync`:

1. Resolve `ClientProfileId` from the token’s subscription.
2. Union that profile with any profile that shares the email.
3. Load Commerce `TransactionLogs` for that email / those profile subscriptions.
4. Ask Billing `GetDocumentsByReferenceIdsAsync` for those external refs / log ids.
5. Classify + HMAC final PDF URL (30 days).
6. Append quote sessions for those profile ids as type **Proforma** with draft HMAC (7 days).

Table columns: date, number, type, amount, LHDN status, Download (`portal/page.tsx:206–230`). Subscription cards get `document_label` “Download tax invoice” vs “Download receipt” (`PortalDocumentQueryService.cs:184–185`).

### 10.2 Holes

- **Quote-only buyers** have no subscription → no valid magic token → no table. Email may have been sent a HMAC link via `DocumentPublished` if a template exists.
- **Transaction-log-first PDF identity** (§4.4) means the downloaded Tax Invoice may omit buyer TIN even when the table says Tax Invoice.
- **B2C rows that inherited VALID** after cons (§4.5) show a VALID badge on an Official Receipt. The download is still the receipt PDF (cons QR is not stamped onto every RCPT).
- Draft quote URLs expire in 7 days; final in 30. After that the table still renders the link. The GET will 401/403 on bad sig. No “link expired” UX.
- Zero-amount checkouts never appear.

### 10.3 Verdict — portal history

Demoable for a subscriber who paid a product. Honest labels **if** you explain the LHDN status column. Not a substitute for MyInvois share QR.

---

## 11. Quote B2B identity — company name in the wrong CRM slot

This is the defect the Wave 2 notes claimed to have closed for **product** checkout, and then re-introduced on **quotes**.

### 11.1 The command shape

```7:18:apps/lazuar-api/Modules/CRM/Contracts/ResolveClientProfileCommand.cs
public record ResolveClientProfileCommand(
    Guid OrganizationId,
    string FullName,
    string Email,
    string Phone,
    string? Tin = null,
    string? IdType = null,
    string? IdValue = null,
    BillingAddressDto? BillingAddress = null,
    bool ConsentedToMarketing = false,
    string? CompanyName = null
)
```

`CompanyName` is the **last** parameter.

### 11.2 Product checkout — correct (named)

```198:208:apps/lazuar-api/Modules/Commerce/Application/Commands/InitiateCheckoutCommandHandler.cs
        var resolveCrmProfileCmd = new ResolveClientProfileCommand(
            tenantId.Value,
            request.Name,
            request.Email,
            request.Phone ?? "",
            Tin: request.TaxId,
            IdType: request.IdType,
            IdValue: request.IdValue,
            BillingAddress: billingAddress,
            CompanyName: request.CompanyName
        );
```

W2-LP-022 tests lock this (`CheckoutB2bIdentityTests.cs:49–54`, `ClientProfileCompanyNameTests.cs:19–39`). Product path is the one that can work.

### 11.3 Quote create — company goes to FullName

```30:35:apps/lazuar-api/Modules/Commerce/Application/Commands/CreateCustomCheckoutCommandHandler.cs
        var resolveCrmProfileCmd = new ResolveClientProfileCommand(
            request.OrganizationId,
            request.ClientName,
            request.ClientEmail,
            ""
        );
```

Ops placeholder is “e.g. Acme Corp” (`CreateQuoteModal.tsx:123`). Merchants will type a **company** into Client Name. That string is `FullName`. `CompanyName` stays null. No TIN at quote time (TIN is collected later on QuoteView).

### 11.4 Quote pay — company name is positional `IdValue`

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

Mapped:

| Arg | Parameter | Value |
|-----|-----------|-------|
| 5 | `Tin` | tax id (ok) |
| 6 | `IdType` | `null` |
| 7 | `IdValue` | **`request.CompanyName`** |
| 8 | `BillingAddress` | address or null |
| 10 | `CompanyName` | **never set** |

W2-LP-102-done.md: “B2B quotes collect TIN on QuoteView and persist it to CRM.” TIN yes. Company name **into `IdValue`**. The LP-022 promise “never company name in `IdValue`” is violated on the session branch.

### 11.5 What MyInvois then does

`LhdnBuyerMapper.TryCreatePayloadBuyer` (`49–61`):

- `buyerName` = company if present, else full name. Company is null → name is “Acme Corp” from FullName. Accidental save.
- `idType` defaults to **BRN** when `IdType` is null.
- `idValue` = the company name string (“Acme Sdn Bhd”) if the buyer filled Company name; else empty → **return false** → no submit.
- If they filled company name, submit proceeds to TIN validate with BRN = “Acme Sdn Bhd” → MyInvois 404/invalid → `SubmitTaxDocumentCommand` throws → inbox retry → still no VALID.

QuoteView never sends `id_type` / `id_value` and never calls validate-tin. Product checkout does both.

### 11.6 Country code landmine (product path)

CheckoutForm defaults `countryCode` to `"MY"` (`CheckoutForm.tsx:53`). CRM / LHDN want `"MYS"`. Initiate stores `request.CountryCode ?? "MYS"` (`InitiateCheckoutCommandHandler.cs:194`). If the buyer leaves the default and `requires_address` is on, CRM gets `MY`. UBL `Country.IdentificationCode` becomes `MY`. That is a realistic INVALID.

### 11.7 Verdict — identity

| Path | Company | TIN | ID type/value | Can file type 01? |
|------|---------|-----|---------------|-------------------|
| Product + requires_tax_id | `CompanyName` | `Tin` | collected + validated | **Yes, if creds + TIN pair are real** |
| Quote B2B | `FullName` and/or **`IdValue`** | `Tin` | missing / wrong | **No** |
| PDF “Billed To” | usually log name only | usually missing | n/a | n/a |

This is the highest-leverage Wave 2 bug that is not “go get a sandbox UUID.”

---

## 12. Sandbox VALID risk — is there any proof of ACCEPT?

**No.**

What exists:

1. **Gateway parser** for `acceptedDocuments` (`LhdnGatewayAdapter.Submit.cs:88–94`). That is code, not a receipt.
2. **Scripts** `scripts/lhdn_sandbox/01_test_b2b.sh` … `07_test_self_billed.sh` that poll **our** GET `/lhdn/documents/{id}` for `status == VALID`. They use hardcoded sandbox buyer `IG56848407100` / NRIC `990806086487` (`01_test_b2b.sh:17–20`). There is **no** committed `.env.test`, no log, no UUID.
3. **`run_all.sh`** never runs the v1.1 signed case.
4. **`LhdnSandboxE2ETests`** is `[Ignore("Requires active Sandbox credentials…")]` (`LhdnSandboxE2ETests.cs:20–21`). It only gets a token and polls a `LHDN_KNOWN_SUBMISSION_UID`. It does **not** submit a document. It does not assert `acceptedDocuments` or `overallStatus=Valid`.
5. **Unit tests** (`MyInvoisLoopTests`, handler tests) substitute the gateway. They prove our state machine, not IRBM.
6. **Wave 2 done files** explicitly leave LP-110/111/113/117 at P until a human sees VALID on remounted ops.

What “ACCEPT” would have to mean, in order:

| Layer | MyInvois field | Do we record it? |
|-------|----------------|------------------|
| HTTP | `202 Accepted` | Treated as success if body parses (`Submit.cs:33`) |
| Body | `acceptedDocuments[]` | UUID stored; **not** shown as “Accepted” in ops |
| Body | `rejectedDocuments[]` | `MarkAsFailed` with message |
| Later | `overallStatus=Valid` | Our `VALID` |
| Later | `Invalid` | Our `INVALID` + details fetch |

Ops never displays “Accepted.” It shows PENDING → SUBMITTED → VALID/INVALID/FAILED. A merchant cannot tell “MyInvois accepted the bytes” from “our worker has a submissionUid.”

**Do not demo “we got VALID from LHDN.”** The honest demo is: “the workers and the panel are wired; we have not landed a UUID in this repo.”

Unsigned 1.0 **can** be valid on preprod for some document types (that is why `01_test_b2b.sh` exists). That does not make it a production 1.1 clearance. JSON 1.1 is the path we chose after XML-DSig died, and it is the path with **zero** ACCEPT evidence.

---

## 13. Ops invoicing pages vs leftover `[MVP-HIDE]`

| Surface | State | Honest job? |
|---------|-------|-------------|
| Quotes | Routed + sidebar | Yes, as payment requests |
| Sales documents | Routed; ledger `sales` | Yes, as a ledger. No, as a MyInvois inbox |
| Credit Notes | Routed; ledger `reversals` | Yes, as contra. No, as CN/DN composer |
| Legal & Billing | Routed | Yes, as a form |
| Product require TIN + SST | Visible | Subtitle about TIN is stale |
| Ops chat | Still `[MVP-HIDE]` | Unrelated |

Tax invoice panel **does** poll LHDN (`TaxInvoiceDetailPanel.tsx:33–47`) and can cancel. It cannot submit, retry a FAILED document, or show raw UBL. There is no merchant “MyInvois submissions” list — only ledger rows that may never have a `TaxDocument`.

Credit Notes info box: “Manual creation of Credit Notes is restricted to preserve double-entry ledger integrity” (`CreditNotesPage.tsx:109–110`). True. Also: we do not issue type `03`/`04` at all.

---

## 14. Demoable vs lies (after Wave 2)

### 14.1 Demoable (click it, it works, copy is close enough)

- Create a quote, see `QT-2026-#####`, open `/pay/{id}`, download a **Proforma** PDF.
- Pay a **non-B2B** product; get `RCPT-` Official Receipt with the two disclaimers; see it on Sales documents and (if subscribed) portal Documents.
- Toggle Require Company Name & Tax ID on a product; checkout shows company, TIN, ID type/value; invalid TIN can block pay **if** MyInvois creds exist.
- Fill Legal & Billing; logo/name/TIN/SST print on the next PDF; UBL supplier is that address, not Merdeka.
- Mark a product as SST 02 + rate when SST # is on file; ledger books `LIABILITY_TAX_PAYABLE`.
- Ops Sales documents badge machine (VALID / SUBMITTED / B2C_RECEIPT / NEEDS_BUYER_TIN / CANCELLED).
- Copy that cancel is supplier-only, 72h, buyer reject not implemented.

### 14.2 Demoable but a lie if you use the words “tax invoice” / “e-invoice” / “LHDN”

- B2B pay produces a PDF headed **Tax Invoice** before MyInvois knows the document exists.
- That PDF usually **omits buyer TIN/company** because of the transaction-log short-circuit.
- Portal labels the same row “Tax Invoice”.
- Quote B2B checkbox “B2B tax invoice after payment” (`QuoteDetailPanel.tsx:172`, `CreateQuoteModal.tsx:184`). After payment you get an `INV-` PDF and **no** type `01` (wrong CRM slot, no ID pair).
- ProductForm: “We do not validate the TIN at checkout.”
- Environment = Production.
- Credit & Debit Notes (no debit notes).
- Last B2C consolidation banner after the first VALID (search key overwritten).
- “XAdES v1.1” (README still implies XML; we sign JSON or we sign nothing).
- InvoiceIssued → MyInvois (handler is a no-op; event unpublished).

### 14.3 Not demoable (code or job exists, loop is open)

- A UUID + LongId + scannable QR from **this** codebase’s sandbox.
- JSON UBL 1.1 ACCEPT.
- Quote-only buyer document history.
- Large B2C `NEEDS_BUYER_TIN` resolution.
- Partial-refund credit note at LHDN.
- Buyer reject.
- Self-billed 11–14 as a product.
- AR / Net-30 / invoice reminders.
- Debit notes as a product.
- Tenant-specific prod vs sandbox API host.

---

## 15. Top next actions (order is the point)

Do **not** open Wave 3 billing depth or Xero until the first four are done. Un-hide already happened; the remaining work is **trust**, not more pages.

1. **Fix quote B2B CRM arity** (`InitiateCheckoutCommandHandler` session branch). Use named args: `Tin`, `IdType`, `IdValue`, `CompanyName`. Collect ID type/value + validate-tin on QuoteView, same as CheckoutForm. Stop putting client name only in `FullName` on create — add an optional company field at quote time or label Client Name as a person.
2. **Stop short-circuiting PDF identity on the transaction log.** `GetCustomerForDocumentAsync` must merge CRM TIN/company/address (or prefer CRM whenever a profile exists for that email). A Tax Invoice PDF without buyer TIN is worse than an Official Receipt.
3. **Do not title a PDF “Tax Invoice” until `LhdnValidationStatus == VALID`.** First document on B2B pay should be “Payment confirmation” / “Official Receipt” / “Tax invoice (pending MyInvois)”. Regen on VALID can say Tax Invoice and print UUID + QR. This is LP-100-class honesty; it is also the only way a lawyer will let you demo.
4. **Land one sandbox ACCEPT + VALID and paste the UUID into this folder.** Run `01_test_b2b.sh` (unsigned 1.0) **and** `04`+`05` (JSON 1.1) against preprod with a real intermediary cert if 1.1 is required. Commit a redacted log (`plans/008-evals/evidence/lhdn-sandbox-valid.md`). Until that file exists, LP-110/111/113/117 stay P. Un-ignore `LhdnSandboxE2ETests` only after it **submits** and asserts `acceptedDocuments` + `Valid`.
5. **Fix consolidation idempotency.** Do not store the cons ref only on `TaxInvoiceId` if VALID overwrites that field. Dedicated cons ledger row, or a `ConsolidationBatchId` that `UpdateLhdnStatus` cannot clobber. Banner search must survive VALID.
6. **Wire tenant `Environment` to `GetBaseUrl` / `GetPortalUrl`.** Until then hide or disable the PROD option.
7. **Delete leftover lies:** ProductForm TIN subtitle; Credit Notes “Debit”; Sales documents empty state; InvoiceIssued handler comment (`B2bSaleReadyForEinvoice`); Lhdn README “signatures unimplemented” vs JSON signer; sequence “prevents gaps.”
8. **Do not sell** buyer reject, debit notes, self-billed, quote AR, or “we filed your January B2C cons” without watching SUBMITTED→VALID on the remounted panel.

After (1)–(4), a founder can sit next to a Malaysian merchant, take a B2B product payment, and either show a **real** UUID or honestly stay on Official Receipts. That is the Wave 2 exit that ADR 021 actually needed. Un-hiding the pages was the cheap half.

---

## 16. File:line index (quick)

| Topic | Evidence |
|-------|----------|
| Un-hide routes | `apps/lazuar-ops/src/App.tsx:232–244` |
| Leftover hide | `App.tsx:242–244` |
| `/pay/{id}` live | `apps/lazuar-portal/src/app/[tenantSlug]/pay/[sessionId]/page.tsx:16–42` |
| Proforma title | `QuoteView.tsx:121`, `GenerateDraftDocumentQueryHandler.cs:70–77` |
| OR disclaimer | `InvoiceDocumentFactory.cs:90–93`, `BaseInvoiceDocument.cs:185–191` |
| Tax Invoice on pay | `GatewayPaymentCompletedHandler.cs:119–126` |
| Sequence series | `DocumentSeries.cs:9–22` |
| Sequence SQL | `GenerateNextSequenceNumberCommandHandler.cs:28–42` |
| B2B submit hook | `B2bTaxInvoiceRequestedIntegrationEvent.cs:10–18`, handler `36–96` |
| InvoiceIssued dead | `InvoiceIssuedIntegrationEventHandler.cs:21–26`; no production `new InvoiceIssued` |
| Submit + sign | `SubmitTaxDocumentCommand.cs:217–275` |
| JSON signer | `JsonUblDocumentSigner.cs:13–50` |
| Signing Off | `appsettings.json:61` |
| Accept parse | `LhdnGatewayAdapter.Submit.cs:88–94` |
| Poll VALID | `LhdnStatusPollingJob.cs:87–114` |
| QR | `LhdnQueries.cs:35–39`, `BaseInvoiceDocument.cs:202–210` |
| Public TIN | `PublicTinValidationEndpoints.cs:19–49`, `CheckoutForm.tsx:96–110` |
| Quote no TIN validate | `QuoteView.tsx:36–55` |
| B2C job | `B2cConsolidationJob.cs:66–85`, `209–218`, `309–324` |
| RM 10k | `appsettings.json:62`, `GatewayPaymentCompletedHandler.cs:94–98` |
| 72h | `CancelWindowMustBeValidRule.cs:12–26`, `TaxInvoiceDetailPanel.tsx:124–128` |
| No buyer reject | `TaxInvoiceDetailPanel.tsx:296–298` |
| SST math | `SstTaxMath.cs:8–24`, `ProductForm.tsx:229–257` |
| Legal profile | `BillingProfilePage.tsx:290–293`, `439–451` |
| Portal history | `portal/page.tsx:200–237`, `PortalDocumentQueryService.cs:36–168` |
| Company → IdValue | `InitiateCheckoutCommandHandler.cs:134–142` vs `ResolveClientProfileCommand.cs:7–18` |
| PDF misses CRM | `CommerceDocumentLookup.cs:98–102`, `157–161` |
| Sandbox E2E ignored | `LhdnSandboxE2ETests.cs:20–21` |
| BaseUrl always preprod | `LhdnGatewayAdapter.cs:44–47` |

---

*End of 04. Do not condense this file into the 008 README. Do not flip LP-110/111/113/117 to Y without a UUID. Do not implement from this file.*
