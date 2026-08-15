# 15 — Invoicing, quotes, and receipts

**Program:** `plans/007-feats` — competitor features vs Lazuar Pay  
**Date:** 2026-08-16  
**Status:** Analysis only — **no product code from this file**  
**Scope:** Commercial documents (quotes, proforma, receipts, branded PDFs, buyer download) and legal Malaysian documents (LHDN / MyInvois tax invoices, credit / debit / refund notes, consolidated B2C). Not a ship ticket. Not a claim that LHDN is live in production UI.  
**Author role:** staff product / compliance analyst for Lazuar Pay document surfaces  
**Workspace researched:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay`  
**Output path (this file):** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/007-feats/15-invoicing-quotes-receipts.md`

**Standing constraints (do not contradict):**

- Lazuar Pay is **BYOK software**, not a Merchant of Record and not an acquiring bank.
- Buyer money on Billplz / Stripe / CHIP (tenant keys) is not Lazuar's SaaS fee.
- Guest money (System B / Lazuar Pay / Billplz) is **not** SaaS money (System A / Paddle). Paddle invoices for Aura Pro are a different seller, a different TIN, and a different legal story.
- Production guest fulfillment is **not** claimed until a sandbox three-book soak. This file may say “the receipt PDF path exists.” It may not say “every live guest already got an Official Receipt.”
- Do not sell WhatsApp dunning or LHDN e-invoice as live product until those loops are closed and (for LHDN) un-hidden.
- Do not become a website builder, marketplace, POS, or ERP to “match competitors.”
- Wrap rails (Stripe, Billplz, CHIP, later Xendit) — do not rebuild acquiring.
- Aura (salon) is a **customer** of Hub, not a competitor. System A (Paddle SaaS) and System B (Hub guest money) stay separate.
- Do **not** claim Aura (the salon OS) does LHDN. LHDN lives in **Lazuar Pay**. This file is the Pay document of record for that distinction.
- Do **not** treat a QuestPDF titled “Tax Invoice” as an LHDN-validated e-invoice. Those are different objects.
- Do **not** treat ADR 023 UI hide as deletion. Backend is dark matter, not gone.

---

## Method

### What this file is for

This chapter answers one product question:

> If a Malaysian merchant (or an Aura salon that connected Lazuar Pay) asks “can I send a quote, get paid, give a receipt, and stay legal with LHDN / SST?”, what do Stripe Invoicing, Chargebee, HitPay, Xendit, Xero, and Paddle actually sell — and what does **our** repo implement, hide, or only pretend to implement?

It is written as Subagent 15 of the 007-feats program. Sibling chapters (`01` inventory, `04` Stripe, `06` HitPay/Xendit, `07` MoR, `08` Chargebee, `10` LHDN competitors, `11` subscriptions, `12` dunning) own rails, billing engines, and the MyInvois competitor map. This chapter owns **Pay’s document stack**: ops invoicing module, Billing PDFs, portal draft/final downloads, and LHDN document types as a **merchant product**, not as an API catalogue.

It does **not**:

- Commit to resurfacing ADR 023 pages this week.
- Re-open Aura packaging (Paddle RM 149 / 1,490).
- Claim MyInvois production clearance.
- Turn Xero into a “must build an ERP” ticket.
- Collapse quote, receipt, tax invoice, and e-invoice into one word.

### How evidence was gathered

**Our product (code and ADRs, not marketing):**

| Surface | Absolute path | Role |
|---------|---------------|------|
| Ops invoicing UI | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/modules/invoicing/` | Quotes, Tax Invoices, Credit Notes pages + panels |
| Ops routes / hide | `.../apps/lazuar-ops/src/App.tsx` | `[MVP-HIDE]` routes for Phase D.3 |
| Ops sidebar | `.../apps/lazuar-ops/src/components/Sidebar.tsx` | No Invoicing module in live nav |
| Billing profile UI | `.../apps/lazuar-ops/src/modules/workspace/pages/BillingProfilePage.tsx` | Legal name, TIN, SST, logo, address — also hidden |
| Portal quote page | `.../apps/lazuar-portal/src/app/[tenantSlug]/pay/[sessionId]/page.tsx` | Forced `notFound()` |
| Portal QuoteView | `.../apps/lazuar-portal/src/modules/checkout/components/QuoteView.tsx` | Proforma UI + HMAC draft PDF |
| Portal buyer dashboard | `.../apps/lazuar-portal/src/app/[tenantSlug]/portal/page.tsx` | Tax invoice download commented out |
| Checkout TIN fields | `.../apps/lazuar-portal/src/modules/checkout/components/CheckoutForm.tsx` | Company / TIN `[MVP-HIDE]` |
| Custom checkout TypeSpec | `.../packages/api-spec/modules/commerce/models/custom-checkout.tsp` | Quote DTO + `draft_pdf_url` |
| Billing TypeSpec | `.../packages/api-spec/modules/billing/models.tsp` + `routes.tsp` | Ledger, profile, draft PDF |
| LHDN TypeSpec | `.../packages/api-spec/modules/lhdn/models.tsp` + `routes.tsp` | Document types `01`–`04`, `11`–`14` |
| QuestPDF | `.../Modules/Billing/Infrastructure/Documents/` | `BaseInvoiceDocument` + model |
| Draft PDF handler | `.../Modules/Billing/Infrastructure/Queries/GenerateDraftDocumentQueryHandler.cs` | On-the-fly “Proforma Invoice” |
| Final PDF + R2 | `.../Modules/Billing/Infrastructure/Commands/GenerateAndStoreDocumentCommandHandler.cs` | Receipt / tax invoice / credit note |
| Sequences | `.../Modules/Billing/Infrastructure/Commands/GenerateNextSequenceNumberCommandHandler.cs` | Atomic `PREFIX-00001` |
| Payment → receipt | `.../Modules/Billing/Infrastructure/EventHandlers/GatewayPaymentCompletedHandler.cs` | B2C `RCPT-yyyy` + Official Receipt |
| Manual mark-paid | `.../Modules/Commerce/Application/Commands/MarkCheckoutAsPaidOfflineCommandHandler.cs` | Offline bank transfer |
| Refund ledger | `.../Modules/Billing/Infrastructure/EventHandlers/GatewayRefundCompletedHandler.cs` | Contra-revenue + tax reverse |
| LHDN refund | `.../Modules/Lhdn/Infrastructure/EventHandlers/GatewayRefundCompletedIntegrationEventHandler.cs` | 72h cancel vs credit note `02` |
| LHDN strategies | `.../Modules/Lhdn/Infrastructure/Services/DocumentStrategyFactory.cs` | Type → UBL template |
| LHDN README | `.../Modules/Lhdn/README.md` | Claimed type coverage + V1.1 unsigned |
| B2C consolidation | `.../Modules/Billing/Infrastructure/Workers/B2cConsolidationJob.cs` | 28th MYT monthly batch |
| ADR 021 | `.../docs/architecture-decision-log/021-compliance-caas-pivot.md` | Compliance CaaS thesis |
| ADR 023 | `.../docs/architecture-decision-log/023-pure-caas-mvp-ui-lobotomy.md` | Why the UI is hidden |
| Gap notes | `.../docs/001-gaps/05-billing-module.md`, `09-lhdn-module.md`, `15-event-driven-architecture.md`, `19-frontend-backend-integration.md` | Orphan `InvoiceIssued`, stub buyer TIN |

**Competitors (public product pages and docs, researched 2026-08-16):**

| Competitor | Primary sources |
|------------|-----------------|
| **Stripe Invoicing** | [stripe.com/invoicing](https://stripe.com/invoicing), [docs.stripe.com/invoicing](https://docs.stripe.com/invoicing), [docs.stripe.com/invoicing/customize](https://docs.stripe.com/invoicing/customize), partial-payments + reminder docs |
| **Chargebee** | Invoice / credit note / quote numbering and PDF customize docs; CPQ “new business quote” + convert-to-subscription |
| **HitPay** | [hitpayapp.com/my/invoicing](https://hitpayapp.com/my/invoicing), HitPay Malaysia invoicing blog (updated 2026), free invoice generator |
| **Xendit** | Payment Links / Invoices product pages (SG/ID/VN); dashboard invoice tutorials; duration + reminder settings |
| **Xero** | Invoice/quote numbering, branding, credit notes; Malaysia MyInvois / Invoici intermediary guides; 2026 consolidation RM10k rule coverage |
| **Paddle** | Help center “how is the invoice sent?”, customer portal docs, MoR model pages, community “receipts vs invoices” threads |
| **LHDN / MyInvois 2026** | SDK document types; ClearTax / Denpyo / Axway 2026 mandate write-ups; Xero Malaysia e-invoice guides (consolidation cutoff 1 Jan 2026) |

**Honesty rules used throughout:**

1. **Code wins over README.** Lhdn README says debit/refund notes are “supported via CreditNoteStrategy.” That is XML routing, not an ops “issue debit note” product.
2. **A hidden page is not a shipped job.** QuotesPage.tsx is real React. It is not a merchant-facing feature while `App.tsx` comments the route and Sidebar omits the module.
3. **A PDF title is not a legal type.** `DocumentType = "Tax Invoice"` on QuestPDF is commercial stationery. LHDN type `01` + MyInvois UUID + QR is the legal object.
4. **An event handler is not a flow** if nothing publishes the event. `InvoiceIssuedIntegrationEvent` has two consumers and **zero publishers**.
5. **Subscription dunning is not invoice reminders.** Commerce `DunningEngineJob` recovers PAST_DUE cards. It does not email “Net 30 invoice #INV-00012 is due in 3 days.”
6. **Paddle documents are Paddle’s.** They do not satisfy a Malaysian tenant’s SST / LHDN obligation for **their** sales.
7. **TaxAmount = 0 on Billplz** means the SST line on a receipt is usually empty even when `TenantBillingProfile.SstRegistrationNumber` is filled.

### Product identity (do not flatten)

Lazuar Pay is a **Checkout-as-a-Service / Compliance CaaS** engine (ADR 019 + 021 + 023). The invoicing stack was designed as the moat: pay at the hosted checkout, book a double-entry ledger, emit a customer PDF, and (for Malaysia) submit UBL to MyInvois.

The live MVP is narrower: **checkout link + gateway + subscription dunning + silent B2C Official Receipt PDF**. Quotes, tax-invoice UI, credit-note UI, billing profile, buyer TIN, and portal download were **lobotomized** so the first sale did not require explaining LHDN.

That split is the whole chapter. Competitors are not compared to the README’s Compliance CaaS. They are compared to **what a merchant can click today** versus **what the backend will do if we uncomment `[MVP-HIDE]`**.

### Letter collision reminder

| Name in this file | What it is | What it is not |
|-------------------|------------|----------------|
| **Quote / proforma** | Commerce `CheckoutSession` with `AdHocLineItems`, no `ProductId` | Xero/Chargebee accepted quote that becomes an AR invoice with a due date |
| **Official Receipt** | QuestPDF after `GATEWAY_PAYMENT` / manual enrollment; sequence `RCPT-yyyy-#####` | LHDN type `01` |
| **Tax Invoice (PDF)** | Same QuestPDF template after LHDN `VALID`; title string changes | MyInvois XML |
| **Tax Invoice (LHDN)** | UBL 2.1 submitted through `SubmitTaxDocumentCommand` | The customer PDF |
| **Credit Note (ops page)** | Ledger `type_filter=reversals` | Manual AR credit memo |
| **Credit Note (LHDN `02`)** | Generated on refund after 72h | Debit note `03` / refund note `04` as first-class products |
| **Platform Billing** | Tenant utility credits for LHDN submit + WhatsApp | Customer invoices |
| **Utility Ledger** | Credit wallet history | Sales ledger / tax invoices |

---

## Legal vs commercial documents

Malaysian merchants, Stripe, Xero, and LHDN use the word “invoice” for four different jobs. Mixing them is how teams ship a pretty PDF and still fail a desk audit.

### The four jobs

| Job | Who needs it | Legal force in MY (2026) | Typical trigger | What “paid” means |
|-----|--------------|--------------------------|-----------------|-------------------|
| **1. Quote / estimate / proforma** | Buyer deciding; seller locking scope | **None.** Offer or request for payment. Not a tax invoice. Not a receipt. | Sales conversation, custom work, B2B retainer | Not paid. May expire. |
| **2. Commercial invoice / payment request** | AR: “you owe us RM X by date D” | Commercial contract evidence. Still **not** an LHDN e-invoice. | Net 15/30/60, retainers, agency work | Open / partial / paid. Balance due is first-class. |
| **3. Official receipt / proof of payment** | Buyer expense claim, consumer protection, “I paid you” | Proof of money movement. For B2C below thresholds, this is often what the buyer keeps. **Not** a substitute for e-invoice when e-invoice is required. | Card/FPX/e-wallet/cash cleared | Already paid. No due date. |
| **4. LHDN e-invoice (MyInvois)** | IRB, SST registrant, buyer input-tax / expense claim under e-invoice rules | **The legal tax document.** XML/JSON to MyInvois, UUID, long ID, QR. 72-hour cancel window, then credit note. | B2B sale, B2C above 2026 individual threshold, monthly B2C consolidation | Validation status `VALID` / `INVALID` / `CANCELLED` is independent of gateway paid. |

Stripe Invoicing is mostly job 2 + 3 (hosted invoice page + receipt after pay). Xero is jobs 2 + 4 (AR books + MyInvois via intermediary). Chargebee is jobs 1 + 2 + 3 for SaaS. HitPay and Xendit are job 2 shaped as a **payment link**. Paddle is job 3 issued **by Paddle as MoR**, which is the wrong seller for a Malaysian tenant’s own SST return.

Lazuar Pay’s architecture **intended** all four jobs (ADR 021 pillars). Implementation **ships job 3 silently**, **built job 1 and job 4 in the dark**, and **never closed job 2** (there is no AR invoice with a due date that a buyer can pay in parts).

### LHDN / MyInvois document types (legal catalogue)

TypeSpec `DocumentType` and `DocumentStrategyFactory` use LHDN’s two-digit codes:

| Code | LHDN name | Our strategy | Template | Productized? |
|------|-----------|--------------|----------|--------------|
| `01` | Invoice (standard) | `StandardInvoiceStrategy` if buyer TIN is a real TIN; `ConsolidatedInvoiceStrategy` if buyer TIN is general public `EI00000000010` | `StandardInvoice.xml` / `ConsolidatedInvoice.xml` | Backend yes. Ops “Tax Invoices” page lists **ledger** rows, not MyInvois submissions. B2B submit path is orphaned (see below). |
| `02` | Credit Note | `CreditNoteStrategy` | `CreditNote.xml` | Refund >72h generates a `TaxDocument` with type `02`. Ops Credit Notes page is a **ledger reversal list**, not a CN composer. |
| `03` | Debit Note | Same `CreditNoteStrategy` (`02` or `03` or `04`) | Same `CreditNote.xml` with `doc_type_code` | **No product.** No ops button. No event. Factory will render XML if an API client sends `03`. |
| `04` | Refund Note | Same as `02`/`03` | Same template | Factory supports it. Refund handler hardcodes `02`, not `04`. |
| `11` | Self-billed Invoice | `SelfBilledInvoiceStrategy` + entity swap | `SelfBilledInvoice.xml` | API/strategy only. Affiliate/contractor payout product is not a live ops job. |
| `12` | Self-billed Credit Note | `SelfBilledCreditNoteStrategy` | `SelfBilledCreditNote.xml` | Same. |
| `13` | Self-billed Debit Note | Routed to SelfBilledCredit | Same family | Same. |
| `14` | Self-billed Refund Note | Routed to SelfBilledCredit | Same family | Same. |

Sample XML corpus under `docs/xml/` includes all eight families (`invoice-v1-1`, `credit-v1-1`, `debit-v1-1`, `refund-v1-1`, and four self-billed). That is **spec literacy**, not a UI.

**Version honesty:** Lhdn README states V1.0 **unsigned** UBL. Templates wrap XAdES blocks in `{{ if document_version == "1.1" }}` with `<!-- SIGNATURE_PLACEHOLDER -->`. Certificates can be stored (`UpdateLhdnCertificateCommand`). Signing is **not** implemented. Do not tell a merchant “we submit signed 1.1 e-invoices.”

**Cancel window:** `CancelWindowMustBeValidRule` = 72 hours after validation. Ops `TaxInvoiceDetailPanel` mirrors this: cancel button only if `lhdn_validation_status === "VALID"` and `< 72h`; otherwise a disabled “Expired” control that tells the operator to issue a credit note. The panel **does not** create that credit note. After 72h, only the refund event handler (or a raw `POST /lhdn/documents` with type `02`) does.

### 2026 mandate facts that change the product (do not ignore)

Research as of 16 August 2026 (ClearTax, Xero/Invoici partner blogs, Axway CTC write-ups, LHDN SDK):

- Malaysia is a **clearance (CTC)** regime. The e-invoice is not “a PDF we emailed.” It is a document **validated by MyInvois before** (or as) it is shared with the buyer. Validated documents get a UUID and a QR the buyer can scan.
- **From 1 January 2026**, the widely reported rule is: a **single transaction above RM 10,000 must be an individual e-invoice** and **must not** be stuffed into the monthly consolidated B2C invoice.
- Consolidated e-invoices (general public TIN `EI00000000010`, classification `004`) remain the path for **small B2C** receipts, typically submitted early the following month. Our `B2cConsolidationJob` fires on the **28th 02:00 MYT** and catch-up-closes prior calendar months — that is **our** schedule, not LHDN’s “by the 7th” folklore. Align the calendar with a tax advisor before claiming compliance.
- Required fields on a real e-invoice include supplier legal name, address, TIN, **SST registration if registered**, buyer identity, invoice number, date/time, line items, **tax type and tax amount**, totals, and (for B2B) buyer TIN. Payment terms are on LHDN’s field list; our UBL templates do **not** emit a commercial `PaymentTerms` note from merchant settings because we have none.

Our consolidation job **does not inspect RM 10,000**. Every B2C `GATEWAY_PAYMENT` with `AssignB2cReceipt` is `ConsolidationStatus = PENDING` and will be batched. That is a **2026 legal hole** in a worker that otherwise looks production-grade.

### How our code maps the four jobs

```
Quote (job 1)
  CreateCustomCheckoutCommand
    → CheckoutSession (ProductId = null, AdHocLineItems JSON, IsB2bRequired, ExpiresAt default +30d)
    → Public GET /public/commerce/{slug}/custom-checkouts/{id}
         attaches HMAC draft_pdf_url
    → GET /public/billing/{slug}/documents/draft/{sessionId}?sig&exp
         GenerateDraftDocumentQuery → QuestPDF "Proforma Invoice" QUOTE-{8 hex}
    → Portal /{slug}/pay/{id}  === notFound() today

Pay the quote
  QuoteView "Proceed to Payment" → public checkout with product_slug "custom"
  OR ops "Mark as Paid (Bank Transfer)" → MarkCheckoutAsPaidOfflineCommand
       → ManualSubscriberEnrolledIntegrationEvent
       → always books B2C Official Receipt (even if is_b2b_required)

Clear a product checkout (job 3, live)
  GatewayPaymentCompleted
    → LedgerEntry GATEWAY_PAYMENT
    → if metadata is_b2b_required != true:
         sequence RCPT-{yyyy}-#####
         AssignB2cReceipt
         GenerateAndStoreDocument "Official Receipt"
         DocumentPublished → email HMAC link (30 days)
    → if is_b2b_required:
         MarkConsolidationNotRequired
         **no PDF, no InvoiceIssued, no LHDN submit**

LHDN B2B (job 4, designed)
  InvoiceIssuedIntegrationEvent
    → Billing InvoiceIssuedHandler (AR + deferred revenue)
    → Lhdn InvoiceIssuedIntegrationEventHandler
         hardcoded buyer "Resolved via CRM", TIN C1234567890
         SubmitTaxDocument type 01
  *** NOTHING IN THE REPO PUBLISHES InvoiceIssuedIntegrationEvent ***

LHDN B2C monthly (job 4, worker live)
  B2cConsolidationJob (28th MYT + catch-up)
    → ConsolidatedInvoiceIssuedIntegrationEvent
    → buyer General Public / EI00000000010 / classification 004
    → SubmitTaxDocument type 01 via ConsolidatedInvoiceStrategy

After MyInvois VALID
  LhdnDocumentValidatedIntegrationEvent
    → ledger UpdateLhdnStatus
    → GenerateAndStoreDocument "Tax Invoice" or "Credit Note" + QR

Refund
  Billing: GATEWAY_REFUND contra + proportional tax
  Lhdn: if original VALID and ≤72h → Cancel
        else → new TaxDocument CN-{paymentId} type 02 (buyer still stub)
```

### Quote / proforma / custom checkout (job 1) — what we actually built

A “quote” in Lazuar Pay is **not** a Chargebee CPQ quote and **not** a Xero quote. It is a **custom checkout session**:

- Created by `POST /admin/commerce/custom-checkouts` with `client_name`, `client_email`, line items (`description`, `quantity`, `unit_price`), optional `expires_at`, `is_b2b_required`, optional `gateway_name`.
- CRM `ResolveClientProfileCommand` upserts the buyer by email.
- Default expiry is **30 days** if the merchant leaves the datetime empty. That is **link expiry**, not Net 30 payment terms. There is no “due date” field distinct from expiry. There is no “accepted / declined / revised” state machine. Statuses observed in UI: `OPEN` (shown as amber), `COMPLETED`, `EXPIRED` (or computed expired if `expires_at` < now).
- Amount is `Σ qty × unit_price`. **No tax line on the quote.** No SST rate. No inclusive/exclusive toggle. `CreateQuoteModal` totals in MYR only.
- `is_b2b_required` is a checkbox “Require B2B Tax Details (LHDN)”. It does **not** collect TIN at quote time. QuoteView copy says TIN will be collected at checkout. CheckoutForm TIN fields are `[MVP-HIDE]`. So the flag is a landmine: it changes ledger customer type and **suppresses** the Official Receipt PDF, then never issues the B2B tax invoice.
- Public GET adds `draft_pdf_url` signed for 7 days (`DocumentLinkSigner.DraftDocumentPayload`). Admin list DTO does **not** populate `draft_pdf_url` (query service omits it). Ops QuoteDetailPanel copies the **portal pay URL**, not the PDF.
- Draft PDF title is **“Proforma Invoice”**. Number is `QUOTE-{first 8 of session GUID}`. Not sequential. Not gapless. Not per-year. Company block is `TenantBillingProfile` legal name + TIN + **address line 1 only**. Logo is **not** fetched on drafts (final PDF fetches `LogoUrl`; draft handler never sets `CompanyLogo`). SST number is **not printed**.
- `DocumentPublished` with type `"Draft Quotation"` would send the “Quotation Ready” email template. **CreateCustomCheckout does not publish that event.** The template is seeded and tested; the quote-create path never fires it. Merchants must copy a link by hand — and the page that link hits is `notFound()`.

This is a **payment-request builder** wearing a proforma hat. It is closer to Stripe Payment Links / Xendit Invoices / HitPay invoices than to Xero Quotes.

### Tax invoices vs receipts vs LHDN legal invoices (jobs 3 and 4)

**Official Receipt (commercial, live backend):**

- Triggered for **non-B2B** gateway payments and for **all** manual enrollments (manual path hardcodes `customerType = "B2C"`).
- Sequence: `GenerateNextSequenceNumberCommand` prefix `RCPT-{yyyy}` → `RCPT-2026-00001`. Postgres `INSERT … ON CONFLICT (OrganizationId, Prefix) DO UPDATE CurrentValue + 1 RETURNING`. Concurrent-safe. Comment claims it “prevents sequence gaps during rollbacks”; an upsert that increments in the same statement **does** consume a number even if a later step fails after the sequence command committed. Treat as **mostly sequential**, not legally gapless.
- Stored on `LedgerEntry.CustomerDocumentNumber` and never overwritten by LHDN UUID (migration `20260804021522_SeparateReceiptAndConsolidationFields` exists specifically because `TaxInvoiceId` used to hold receipt #, consolidation ref, **and** UUID).
- PDF: A4 Helvetica, blue “Official Receipt” heading, logo if `LogoUrl` downloads, TIN, address line 1, billed-to name/email, description/amount table, subtotal / discount / tax / total. Tax row only if `LIABILITY_TAX_PAYABLE` ≠ 0. LHDN UUID + QR only after `VALID`.
- Email: Communications “Official Receipt” template with HMAC link, 30-day expiry, `GET /public/billing/{slug}/documents/{ledgerEntryId}` → 302 to R2. That public final-document route is **intentionally not in TypeSpec** (Billing README §9 / honesty allowlist).

**Tax Invoice PDF (commercial stationery, only after LHDN VALID):**

- `LhdnDocumentValidatedIntegrationEventHandler` regenerates the **same** QuestPDF class with `DocumentType = "Tax Invoice"` (or `"Credit Note"` if `ReferenceType` contains `REFUND`) and stamps QR.
- Invoice number preference: `CustomerDocumentNumber` ?? `TaxInvoiceId` ?? first 8 of ledger GUID. Comment: “never use LHDN UUID as invoice number.” After validation, `UpdateLhdnStatus` still writes UUID into legacy `TaxInvoiceId`, so a row that never got `RCPT-` can display a UUID in the PDF header. B2B rows skip `AssignB2cReceipt`, so they are exactly in that bucket **if** a B2B invoice were ever submitted.

**LHDN e-invoice (legal, pipeline exists, B2B trigger dead):**

- Submit path: credits check (live) → strategy XML → LF normalize → XSD → SHA-256 → `TaxDocument PENDING` → `LhdnSubmissionJob` → MyInvois → `LhdnStatusPollingJob`.
- B2C consolidation path is wired (`ConsolidatedInvoiceIssued` is published by the worker).
- B2B path is **not** wired. `InvoiceIssuedIntegrationEvent` is subscribed in Billing and Lhdn. Grep of `new InvoiceIssuedIntegrationEvent` across `*.cs` is empty. Gap note `15-event-driven-architecture.md` calls it a full orphan.
- Even if published, Lhdn’s handler fills buyer as `"Resolved via CRM"` / TIN `C1234567890` / BRN `202001012345` / KL address. That would submit **false taxpayer data**. Do not turn the publisher back on without CRM/TIN resolution.
- StandardInvoice.xml **hardcodes supplier postal address** to the LHDN sample (“Lot 66 / Bangunan Merdeka / Persiaran Jaya / 50480 / state 14”) even though `ViewModelMapper` maps `config.AddressLine1`. Buyer address is templated. Supplier address is not. A “VALID” e-invoice can still carry the SDK sample HQ. That is a compliance defect, not a polish issue.

**Ops “Tax Invoices & Receipts” page:**

- Reads `GET /admin/billing/ledger?type_filter=sales`.
- Columns: date, `tax_invoice_id` or short GUID, `customer_type` (B2C/B2B), net from `REVENUE_*`, tax from `LIABILITY_TAX_PAYABLE`, LHDN badge.
- Badges: `VALID`, `SUBMITTED`/`PENDING` (pulse), `B2C_RECEIPT` / `CONSOLIDATED_PENDING`, `REJECTED`/`CANCELLED`, else **NOT REQUIRED**.
- This is a **ledger browser**, not an invoice editor. You cannot create an invoice here. You cannot set due dates. You can download the stored PDF and, if validated and <72h, cancel at LHDN.

### Credit / debit / refund notes

**Commercial / ledger (ops Credit Notes page, hidden):**

- `GET /admin/billing/ledger?type_filter=reversals`.
- Copy on the page is honest: “Credit Notes are generated automatically when a refund is issued or an e-Invoice is cancelled. Manual creation … is restricted to preserve double-entry ledger integrity.”
- Trigger label: `reference_type === "GATEWAY_REFUND"` → “Refund”, else “Cancellation.”
- Math: `CONTRA_REVENUE_REFUNDS` or `REVENUE_GROSS` as refund amount; tax reverse from `LIABILITY_TAX_PAYABLE`.
- Reuses `TaxInvoiceDetailPanel` (download + LHDN cancel). There is no “apply this credit to invoice X” and no remaining-credit balance.

**Gateway refund ledger (live if refunds complete):**

- `GatewayRefundCompletedHandler` mirrors payment signs. Prefers event `TaxAmount`; else scales original tax by `RefundedAmount / originalPaid`. Full refund reverses full tax. This is the only **partial money** logic in Billing, and it is on **refunds**, not on invoice collection.

**LHDN (live handler, stub buyer):**

- If original `TaxDocument` is `VALID` and ≤72h since `ValidatedAt`: MyInvois cancel, reason “Customer requested refund.”
- Else: build type `02` credit note `CN-{PaymentRecordId}`, tax 0, buyer stub `IG1234567890`. Persist `TaxDocument`. The snippet shown in the handler **saves the XML locally**; it does not obviously go through `SubmitTaxDocumentCommand` (no credit check, no XSD, no outbox submit in that else-branch). Treat post-72h CN as **generated, not necessarily submitted**. That is a product/compliance hole.

**Debit notes (`03`):** no issuer. Competitors (Xero) use debit notes to increase an already-issued invoice. We have a factory case and a sample XML.

**Refund notes (`04`):** LHDN’s distinct type for refunds vs credit notes. We collapse `02`/`03`/`04` onto one template and always emit `02` from the refund handler.

**Self-billed `11`–`14`:** entity swap in `ViewModelMapper` (tenant becomes buyer). Intended for affiliate / unregistered supplier payouts (ADR 021 adjacent). No ops “pay contractor and self-bill” job.

### Payment terms, due dates, reminders

| Concept | Our implementation | Competitor baseline |
|---------|--------------------|---------------------|
| Quote expiry | `ExpiresAt`, default +30 days | Chargebee/Xero quote valid-until |
| Invoice due date | `InvoiceIssuedIntegrationEvent.DueDate` **exists on the event and is unused**. No column on a customer invoice. | Stripe default payment terms; HitPay due date on every invoice; Xero terms |
| Net 15/30/60 / Due on receipt / Custom | **None** | Stripe, Xero, Chargebee CPQ payment terms |
| Late fees | **None** | HitPay (fixed, % of total, % of outstanding + grace) |
| Invoice reminders (before / on / after due) | **None** | Stripe automatic reminders; HitPay scheduled; Xendit payment-link reminders; Chargebee dunning **and** invoice reminders |
| Subscription dunning | **Yes, live ops** — `DunningEngineJob`, campaigns, pause/resume, email path; WhatsApp still stub | Different job. Do not count it as invoice AR. |
| Hosted “pay this invoice” page | Quote pay page **404**. Product checkout is a **buy now** page, not an invoice. | Stripe Hosted Invoice Page; HitPay invoice link; Xendit invoice; Chargebee hosted pages |
| AR aging | **None** | Stripe AR charts; Xero A/R |

A merchant who wants “I did the work last week, pay me in 30 days, remind them twice” cannot do that in Lazuar Pay. They can send a checkout link that expires. That is **CIA (cash in advance)**, not invoicing.

### Partial payments

- Custom checkout and product checkout collect **the full `total_amount`**. There is no “deposit 40% / remainder later” on a quote. (Aura salon deposits are a **different product** on guest bookings; do not import them here as Pay invoicing.)
- Ledger does not store “amount due / amount paid / balance” on a document. A sale is a payment event, not an invoice that accrues payments.
- Refunds can be partial (tax scaled). That is the inverse of partial collection.
- HitPay and Stripe Invoicing treat partial pay / deposits as a first-class invoice feature. We do not.

`InvoiceIssuedHandler` books `ASSET_ACCOUNTS_RECEIVABLE` + `LIABILITY_DEFERRED_REVENUE` and never applies cash against AR. `ManualPaymentRecordedIntegrationEvent` is dead. Even the designed B2B path has **no settlement**.

### PDF branding

What `TenantBillingProfile` can store: `legal_name`, `tin`, `registration_number` (SSM), `sst_registration_number`, `logo_url`, address (line1–3, city, postal, state, country).

What `BaseInvoiceDocument` prints:

- Logo (final docs only, best-effort HTTP GET; failures swallowed)
- Legal name
- `TIN: {tin}`
- Address **line 1 only**
- Document type string (Official Receipt / Proforma Invoice / Tax Invoice / Credit Note)
- Number + date
- Billed-to name + email (no buyer TIN, no buyer address, no SSM)
- Line description + amount (draft quotes flatten qty×price into one amount; no qty column on PDF)
- Subtotal, discount if any, **“Tax”** if any (not “SST 8%”), total
- LHDN UUID + QR when validated
- Page n of m
- Font: Helvetica. Accent: `Colors.Blue.Darken2`. No merchant brand color. No memo. No footer terms. No payment instructions. **No SST number. No SSM number.**

Ops BillingProfilePage can upload a logo via One presigned URL. The page is `[MVP-HIDE]`. Public `GET /public/billing/{slug}/profile` still works and QuoteView would show logo + legal name + TIN + SSM **on the HTML proforma** — if the route were not 404.

Chargebee and Stripe let you set logo, color, memo, footer, template. Xero has full branding themes. HitPay has footer + custom fields (up to 3) + QR to the invoice link. We have a single QuestPDF class.

### Sequential numbering

| Document | Scheme | Sequential? | Configurable prefix? | Per customer? |
|----------|--------|-------------|----------------------|---------------|
| Quote / proforma PDF | `QUOTE-{sessionId[0..8]}` | No | No | No |
| B2C Official Receipt | `RCPT-{yyyy}-{D5}` | Yes, per org+prefix+year | Prefix hardcoded | No |
| B2B tax invoice PDF | Receipt # if any, else UUID/GUID | Broken for B2B | No | No |
| LHDN `internal_id` | Receipt #, or `B2C-CONS-{yyyyMM}-{org}`, or `CN-{paymentId}` | Mixed | No | No |
| Credit note | `CN-{PaymentRecordId}` | No | No | No |

Stripe: customer-level or account-level sequential schemes, custom prefix. Chargebee: separate sequences for invoices, credit notes, **and quotes**, with format rules. Xero: independent sequences for invoices, quotes, credit notes, POs; you can set “next number” when migrating. We only sequence receipts.

LHDN requires a unique document number (`cbc:ID`). Using a UUID as `internal_id` is unique but ugly. Using `CN-{guid}` is unique. Using `QUOTE-A1B2C3D4` can theoretically collide across a huge keyspace but is not an auditor-friendly series.

### SST line treatment

LHDN tax type codes in our TypeSpec: `01` | `02` | `03` | `04` | `05` | `06` | `E`.

Industry mapping (MyInvois):

| Code | Meaning |
|------|---------|
| `01` | Sales Tax |
| `02` | Service Tax (this is the SST services line merchants think of) |
| `03` | Tourism Tax |
| `04` | High-Value Goods Tax |
| `05` | Sales Tax on Low Value Goods |
| `06` | Not applicable |
| `E` | Exempt |

`LedgerEntry.AddLine(..., taxTypeCode = "06", msicCode = "004")` **defaults every line to Not Applicable + classification 004** (004 is the consolidated B2C classification). Unless a caller overrides, consolidation groups everything as tax type `06`.

Where tax money actually appears:

- `GatewayPaymentCompletedHandler`: `grossRevenue = AmountPaid - TaxAmount`; if `TaxAmount > 0`, credit `LIABILITY_TAX_PAYABLE`.
- Stripe checkout.session.completed: `TaxAmount = session.TotalDetails.AmountTax / 100`. That is **Stripe Tax**, not Malaysian SST, and only if the Checkout Session was created with Stripe Tax. PaymentIntent-only branch sets `TaxAmount: 0`.
- Billplz: `taxAmount = 0` always.
- CHIP: `TaxAmount: 0`.
- Razorpay: may parse a tax field; not MY SST.
- Manual mark-paid: **no tax line at all** (cash = revenue).
- Quote line items: no tax fields.
- Product catalog: `requires_tax_id` exists and is forced `false` in CreateProductForm (`[MVP-HIDE]`). There is **no product SST rate**.

`TenantBillingProfile.SstRegistrationNumber` is stored and returned on the public profile DTO. It is **not** rendered on QuestPDF. UBL templates do not show a dedicated SST ID scheme node from the profile in the snippet inspected on `StandardInvoice.xml` (supplier PartyIdentification is TIN + id_type only). Sample XML in `docs/xml` has `schemeID="SST"` — our live template is thinner than the sample.

Ops TaxInvoiceDetailPanel labels tax as `Tax ({B2C ? 'Inclusive' : 'Added'})`. That is UI copy, not a calculation policy. B2C receipts typically have tax = 0, so the label is decorative.

**Bottom line:** we have a liability account and a profile field. We do **not** have an SST engine (rate per product, inclusive vs exclusive, 8% service tax, exemptions, tourism tax). Do not advertise “SST-ready invoices.”

### Buyer portal download

Designed path:

1. After PDF store, `DocumentPublishedIntegrationEvent` (org, ledger id, type, R2 key, slug, names, email).
2. Communications builds HMAC URL, sends “Official Receipt” (or “Quotation Ready” if type is Draft Quotation).
3. Buyer clicks → public billing redirect → R2 presign 5 minutes.
4. Portal subscriptions card had `<a>Download Tax Invoice</a>` → `[MVP-HIDE]`.
5. Quote HTML page had “Download PDF Quote” if `draft_pdf_url` → page 404.

Admin path (also hidden with the invoicing module): `GET /admin/billing/ledger/{id}/document` → JSON `{ url }` presign. No existence check; a missing object is a dead R2 URL.

There is **no** authenticated document list on the buyer portal (“all my invoices”). Paddle and Stripe customer portals are that list. Ours is a magic-link subscriptions page plus an email deep link.

### What ADR 021 promised vs what documents do

ADR 021 three pillars, scored as documents:

| Pillar | Promise | Document reality |
|--------|---------|------------------|
| B2C low-ticket | Silent monthly consolidated e-invoice | Worker + general public TIN **exist**. No RM10k split. Tax type defaults `06`. UI hidden. |
| B2B high-ticket | TIN at checkout, immediate validated tax invoice + QR | Quote flag + hidden TIN fields. `InvoiceIssued` unpublished. Handler stubs TIN. Pay page 404. |
| Cross-border | Zero-rated export classification | No export tax code product. Ledger FX exists on payment events; PDF currency follows the line. |

ADR 023 then hid the incomplete B2B UX rather than shipping a lie. That was the correct temporary call. It also hid the **receipt download** and the **legal profile**, which are not B2B-only. That is the expensive part of the lobotomy.

---

## Competitor invoices

Competitors are grouped by **job they actually sell**, not by “they have a button named Invoice.”

### Stripe Invoicing

**What it is:** Accounts-receivable SaaS bolted to Stripe payments. One-off and recurring. Dashboard no-code plus Invoicing API. Hosted Invoice Page. PDF. Receipt after payment. Not a Malaysian tax authority integration.

**Quotes / proforma / checkout sessions:**

- Stripe has **Quotes** (Billing) that convert to invoices/subscriptions, and **Payment Links** / Checkout Sessions for “pay now.”
- Invoicing proper is job 2: you create an invoice, it is `draft` → `open` → `paid` / `void` / `uncollectible`.
- A quote is a first-class object with acceptance, expiry, and one-click convert (third-party write-ups and Stripe Billing quotes). This is closer to Chargebee than to our custom checkout.

**Tax invoices vs receipts vs legal:**

- Stripe generates a **commercial invoice PDF** and, after payment, a **receipt**. Both are Stripe-branded-or-white-labeled stationery.
- Stripe Tax computes US/EU/etc. rates. It is **not** MyInvois. It will not give you an LHDN UUID. A Malaysian SST registrant who only uses Stripe Invoicing still needs Xero/AutoCount/SQL or a MyInvois intermediary.
- Invoice email includes line items, tax line, due date, pay button, download invoice. After pay: download invoice **and** download receipt (marketing page mock).

**Credit / debit / refund notes:**

- **Credit notes** are a listed Invoicing feature (marketing “Each invoice includes… Credit notes” / dashboard AR toolkit).
- Refunds go through Stripe Refunds and can attach to the invoice.
- No LHDN `02`/`03`/`04` distinction.

**Payment terms, due dates, reminders:**

- Default payment terms in Invoice settings (due on receipt, Net 7/15/30/45/60, custom).
- Automatic email reminders: before due, on due, past due (no-code guide).
- Smart Retries / AI dunning for **auto-collect** invoices (saved card). Claim on marketing page: 87% of Stripe invoices paid within 24 hours — that is **pay-now behavior**, not Net 30 reality.
- AR aging charts.

**Partial payments:**

- First-class: [Accept partial payments](https://docs.stripe.com/invoicing/partial-payments), payment application, payment plans. This is a real gap vs us.

**PDF branding:**

- Branding settings: logo, brand color, accent, icon — shared across Checkout, invoices, receipts.
- Invoice template: memo, footer, custom fields, numbering scheme, page size.
- 25+ languages, 135+ currencies, 100+ methods on the hosted page.

**Sequential numbering:**

- Account-level sequential **or** per-customer prefix + sequence. Documented in customize docs.

**SST line:**

- Tax rates as objects. You can create an 8% “SST” rate by hand. It will not populate LHDN tax type `02` or SST registration on a MyInvois XML.

**Buyer portal:**

- Hosted invoice page is the portal for that invoice (status, pay, download).
- Customer portal (Billing) lists invoices and payment methods.

**Vs us:** Stripe is what our **hidden** Quotes + hosted pay page **wanted to be**, plus real AR (due dates, reminders, partials, credit notes). Stripe is **weaker** than our dark LHDN module on Malaysian legal invoices — they have nothing there. Our live MVP is weaker than Stripe on every commercial invoice job.

### Chargebee

**What it is:** Subscription billing OS. Invoices are the output of subscriptions, one-time charges, and CPQ quotes. Credit notes are a core object (adjustment, refundable, promotional). Quotes are a product (legacy quotes + newer CPQ).

**Quotes:**

- CPQ: new-business quote, renewal quote, notes, payment terms, T&Cs.
- Email a secure quote link. Customer accepts. Convert to subscription + invoice.
- Quote numbering is a **separate sequence** from invoices and credit notes (`Settings → Invoices, credit notes and quotes → Quotes → Manage numbering`).
- This is the grown-up version of our custom checkout: we have line items + expiry + pay link; they have accept / convert / contract terms / catalog products.

**Tax invoices vs receipts:**

- Chargebee invoices are commercial subscription invoices (and can include tax via Chargebee Tax / Avalara).
- Not MyInvois. Same hole as Stripe for LHDN.

**Credit notes:**

- Three types. Apply to invoices. Refundable vs adjustment. Customizable PDF and numbering.
- API-complete.

**Payment terms / reminders:**

- Invoice due dates from subscription billing dates (advance / arrears).
- Dunning (smart retries) is a paid-plan feature; this is **failed recurring collection**, not “Net 30 reminder,” but Chargebee also emails invoices.
- Consolidated invoicing (multi-subscription → one invoice) exists on higher plans — conceptually similar to our B2C consolidation, but commercial, not LHDN.

**Partial payments:**

- Credits and partial payments exist in the billing object model (apply credit note, record payment). Stronger than us; not as loudly marketed as Stripe/HitPay partials.

**PDF branding:**

- `Settings → Configure Chargebee → Invoices, credit notes and quotes` — logo, address, notes, custom fields, PDF layout. Applies to invoices, CNs, **and quotes**.

**Sequential numbering:**

- Best-in-class configurability among billing engines (prefix, date tokens, separate series).

**SST:**

- Tax profiles / Avalara. Manual MY tax possible. No LHDN.

**Buyer portal:**

- Chargebee customer portal: invoices, payment methods, self-serve. Mature.

**Vs us:** Chargebee is the SaaS-billing ceiling. Our Commerce subscriptions + Official Receipt is a thin slice. Our LHDN module is a capability Chargebee will not grow for us. Do not copy CPQ. Do copy **separate sequences** and **quote ≠ invoice ≠ credit note** as three numbered documents.

### HitPay (SEA / Malaysia)

**What it is:** The local “send an invoice, get FPX / DuitNow / TnG / GrabPay / cards” product. This is the competitor a KL freelancer actually opens. Marketing: [hitpayapp.com/my/invoicing](https://hitpayapp.com/my/invoicing) (researched 2026-08-16).

**Quotes / checkout:**

- HitPay sells **invoices**, not a separate quote object. Draft invoices exist. The invoice **is** the payment link (`hitpay.shop/s/…`) plus PDF.
- That is the same collapse we made (quote = payable session). They **shipped the page**. We 404 it.

**Tax invoices vs receipts vs LHDN:**

- Invoice PDF shows invoice # (`INV-0128HYY` in mocks), invoice date, **due date**, bill-to, line items, discount, **tax line** (examples: “Service tax (10%)”, “GST (9%)” on SG mocks), footer, QR to pay online.
- Mark as paid (cash) — same job as our “Mark as Paid (Bank Transfer).”
- **Not** MyInvois. HitPay’s 2026 Malaysia invoicing blog talks reminders, late fees, custom fields, mobile — not LHDN UUID. A HitPay invoice is job 2+3. Job 4 still needs Xero or MyInvois portal.

**Credit notes:**

- Not a headline feature. Refunds via HitPay payments. No LHDN CN.

**Payment terms / reminders:**

- Due date on every invoice (mock: invoice 23 Nov, due 27 Nov).
- Automated reminders: days **before** due; repeating invoices.
- 2026 blog: three reminder types (manual resend, manual overdue, scheduled auto before/after due). Late fees: fixed, % of total, or % of outstanding, with grace.
- Recurring invoices.

**Partial payments:**

- Explicit feature: “Allow customers to partially pay invoices” / “Add Payment” for deposits. This is table-stakes for agencies and tuition. We have zero.

**PDF branding:**

- Logo, footer notes, description, QR, invoice link on PDF. Custom fields (up to 3). Email templates. Mobile compose.

**Sequential numbering:**

- `INV-…` visible. Treat as sequential commercial numbers (confirm prefix settings in-product; marketing shows INV-).

**SST:**

- “Offer tax” — add a tax line. You can name it SST. No e-invoice.

**Buyer portal:**

- The invoice link **is** the portal. Pay, see balance. Not a historical archive like Paddle/Stripe portal.

**Vs us:** HitPay is the **MVP we hid**. Due date + reminders + tax line + mark paid + partials + PDF + FPX. If Phase D.3 only unhides our quote page without due dates, reminders, tax, and partials, we still lose to HitPay on commercial invoicing and we still cannot claim LHDN until B2B publish + TIN + RM10k rules are real.

### Xendit (SEA payment invoices)

**What it is:** Payment-gateway invoices / payment links across ID, PH, VN, TH, SG, MY. Dashboard “create invoice,” API invoices, duration, reminders, pay-now.

**Quotes:**

- No CPQ. Invoice duration = expiry (same as our `expires_at`).

**Tax invoices vs receipts vs LHDN:**

- Xendit “invoice” is a **collecting document** (amount, description, payer email, success redirect). PDF generation exists in ecosystem tutorials. Not a Malaysian tax invoice. Not MyInvois.
- Currencies for “Pay now” on third-party invoice tools include MYR.

**Credit notes:**

- Refunds via Xendit. No CN product.

**Payment terms / reminders:**

- Invoice duration.
- “Set reminders for upcoming or overdue payments” on Payment Links pages (SG/ID/VN).
- Notifications when paid.

**Partial payments:**

- Not a headline. Typically pay-in-full links (confirm per API version; do not claim they match HitPay partials without a dashboard pass).

**PDF branding:**

- Limited vs Stripe/Xero. Brandable payment pages more than accounting stationery.

**Sequential numbering:**

- Xendit IDs / external_id. Not an accountant’s series.

**SST / LHDN:**

- None.

**Buyer portal:**

- The invoice URL.

**Vs us:** Xendit is a **gateway-shaped** invoice, like our custom checkout. We already match this job in the backend. They win on a **live** pay page and reminders. We win (only in the dark) on ledger + LHDN types.

### Xero

**What it is:** The SME general ledger. Invoices, quotes, credit notes, bills, sequential numbering, branding themes, A/R. In Malaysia, e-invoice is typically **Xero + Invoici (or similar) as MyInvois intermediary**, not a native LHDN XML engine inside Xero core.

**Quotes:**

- First-class Quotes, own number sequence, convert to invoice, branding. Accept/decline workflow. This is job 1 done properly.

**Tax invoices vs receipts vs LHDN:**

- Xero **tax invoice** is the accounting invoice (GST/SST tax rates, tax codes, sequential INV-).
- **Receipt** is a payment against an invoice (or a receive-money).
- **E-invoice** is a **submission** of that invoice to IRBM via intermediary. Guides (Caltrix, Adventus, Fusioneta, Xero MY accountant guides, 2025–2026): register Xero as intermediary in MyTax, connect Invoici, send individual e-invoices; credit notes emailed to the gateway for validation in some flows.
- 2026 rule coverage in Xero-partner blogs: transactions **above RM 10,000 cannot be consolidated**.

**Credit / debit notes:**

- Credit notes: create from invoice or standalone; apply full or partial; print PDF. Tutorial-grade UX.
- Debit notes exist in MY e-invoice discussions on Xero’s idea board (customers asking to submit DN/CN). Xero’s accounting model is stronger than ours; e-invoice coverage depends on the intermediary.

**Payment terms / reminders:**

- Invoice payment terms (Net 30 etc.) as a Xero staple.
- Reminders via Xero or add-ons (Paidnice for late fees).
- Online payments via Stripe / GoCardless apps — Xero is **not** the acquirer.

**Partial payments:**

- Native. Allocate payment to invoice. Balance due remains. Credit notes apply partially. This is what `InvoiceIssuedHandler`’s AR line was sketched to support and never did.

**PDF branding:**

- Branding themes: logo, colors, payment advice, terms. Separate themes for invoices vs quotes vs POs.

**Sequential numbering:**

- `Change the transaction number sequence` — invoices, quotes, credit notes, POs; set next number on migration. The gold standard.

**SST:**

- Tax rates / tax codes. SST 8% as a tax rate. Reports. This is a real SST **line**, still not a replacement for e-invoice.

**Buyer portal:**

- Email + online invoice. Customer statements. Not a SaaS “manage subscription” portal.

**Vs us:** Xero is the **CFO system of record**. ADR 021 said “Keep: Xero / Cloud Accounting Sync.” **There is no Xero sync in the Pay repo.** Our double-entry ledger is a baby Xero for GMV + tax liability. We should not rebuild Xero. We should (later) emit documents Xero can ingest, or submit MyInvois ourselves and let Xero stay the books. Our hidden Tax Invoices page is a ledger viewer, not Xero.

### Paddle

**What it is:** Merchant of Record for digital products. Paddle is the **legal seller**. Paddle invoices / receipts show **Paddle** (plus vendor name). Paddle collects VAT/sales tax and remits. Customer portal downloads **Paddle** invoices.

**Quotes:**

- Paddle Billing has checkout overlays, prices, discounts. It is not a B2B quote-to-cash studio for a Malaysian agency invoicing a Sdn Bhd client under **the agency’s** TIN.

**Tax invoices vs receipts:**

- Paddle sends invoice email + hosted page + PDF. Community threads still argue “they only send receipts / I need a tax invoice with *my* company.” Under MoR, the **correct** tax invoice for the **buyer’s** purchase is often **Paddle’s**, not the vendor’s.
- That is exactly why Aura uses Paddle for **System A (salon → Aura)** and must **not** use Paddle for **System B (guest → salon)**. A Paddle receipt for a haircut would make Paddle the hairdresser. The same rule applies to any Hub merchant: Paddle-as-MoR would put the wrong TIN on the e-invoice.

**Credit notes:**

- Paddle generates credit notes for refunds / adjustments as MoR. Vendor does not issue the LHDN CN.

**Payment terms / reminders:**

- Recurring billing dates, dunning for failed cards. Not Net 30 AR for custom work.

**Partial payments:**

- Not the HitPay deposit model. Subscriptions + one-time charges.

**PDF branding:**

- Limited. Paddle’s brand is on the document by design. Vendor logo may appear; legal seller remains Paddle.

**Sequential numbering:**

- Paddle’s series, not the tenant’s.

**SST / LHDN:**

- Paddle’s global tax engine. **Not** Malaysian SST registrant returns for the tenant. **Not** MyInvois on behalf of the tenant (intermediary mode would be a different, huge product).

**Buyer portal:**

- Best-in-class for “see past payments, download invoices, update method, cancel.” Our portal is a subscription list + cancel. We hid the download button.

**Vs us:** Paddle is the **wrong comparison** for tenant invoicing and the **right comparison** for Aura Pro (System A). Do not “match Paddle invoices” on Lazuar Pay. Do match “buyer can download a document without asking WhatsApp.” ADR 019/021 already refused MoR because it **breaks LHDN** (the Malaysian seller must issue the e-invoice).

### Competitor one-glance (document jobs)

| Job | Stripe Invoicing | Chargebee | HitPay | Xendit | Xero | Paddle | **Lazuar Pay today (live UI)** | **Lazuar Pay backend / hidden** |
|-----|------------------|-----------|--------|--------|------|--------|--------------------------------|----------------------------------|
| Quote / proforma | Quotes + payment links | CPQ quotes + sequences | Draft invoice | Expiring invoice | First-class quotes | Prices / checkout | **No** | Custom checkout + proforma PDF |
| Hosted pay-the-document page | Hosted Invoice Page | Hosted pages | Invoice link | Invoice URL | Online invoice + apps | Paddle checkout | Product checkout only | Quote route 404 |
| Due date / Net terms | Yes | Billing dates + CPQ terms | Yes | Duration / reminders | Yes | Subscription cycle | **No** | Event field unused |
| Invoice reminders | Yes | Invoice email + dunning | Yes + late fees | Yes | Yes + add-ons | Dunning | Subscription dunning only | — |
| Partial pay / deposit | Yes | Credits / record payment | Yes | Weak / full pay | Yes | Weak | **No** | Refunds can be partial |
| Official receipt PDF | Yes | Invoice/receipt | Invoice PDF | Limited | Payment receipt | MoR receipt | Silent email link | QuestPDF Official Receipt |
| Branded PDF | Strong | Strong | Medium | Weak | Strong | Paddle-branded | Default Helvetica | Logo + TIN if profile exists |
| Sequential # | Yes, 2 schemes | Yes, 3 series | INV- | External id | Yes, many series | Paddle series | **Invisible** `RCPT-` | `RCPT-` yes; quotes no |
| SST line | Manual tax rate | Tax profiles | Named tax line | No | Tax codes | MoR tax | Usually RM 0.00 | Liability account + unused SST # |
| LHDN e-invoice | No | No | No | No | Via intermediary | No | **No UI** | Types 01–04, 11–14; B2C job; B2B orphan |
| Credit note | Yes | Yes, 3 types | Refunds | Refunds | Yes | MoR CN | **No UI** | Ledger reversal + LHDN `02` |
| Debit note | Rare | Rare | No | No | Yes (acct) | No | No | Factory only |
| Buyer download | Portal + hosted | Portal | Link | Link | Email | Portal | Email HMAC only | Portal button hidden |
| Mark paid offline | Yes | Record payment | Yes | Limited | Yes | N/A (MoR) | **No UI** | `mark-paid` API + handler |
| Self-bill / affiliate | No | No | No | No | Manual | No | No | Strategies 11–14 |

---

## Our hidden vs live surfaces

ADR 023 called this a “UI lobotomy”: keep the .NET and TypeSpec, comment the routes, let the bundler tree-shake. As of 2026-08-16 that is still the truth.

### Live merchant UI (ops)

Sidebar modules: **Commerce**, **Developer**, **Workspace**.

| Live route | What it is | Document relevance |
|------------|------------|--------------------|
| `/commerce/dashboard` | CaaS stats | Not a document list |
| `/commerce/products` | Checkout links | `requiresTaxId` forced false |
| `/commerce/subscribers` | Subscriptions | Portal link = Stripe portal, not our invoice list |
| `/commerce/transactions` | Payment logs + refund | Refunds can create hidden credit-note ledger rows |
| `/commerce/coupons` | Discounts | Discount line on receipt if booked |
| `/commerce/dunning-campaigns` | Subscription recovery | **Not** invoice reminders |
| `/commerce/templates` | Email/WA templates | Includes Official Receipt + Quotation Ready if seeded |
| `/developer/*` | Keys, webhooks, logs | LHDN API keys live here as One façade |
| `/workspace/general` | Workspace | — |
| `/workspace/payment-gateways` | BYOK | Determines whether TaxAmount can ever be non-zero (Stripe Tax) |
| `/workspace/email` | Email provider | Receipt email delivery |
| `/workspace/billing` | **Utility credits** | Pays for LHDN submit + WA; not customer invoices. Routed, **not in Sidebar**. |
| `/workspace/ledger` | Credit wallet history | Same. Routed, **not in Sidebar**. |

A merchant who does not guess `/workspace/billing` never sees credits. A merchant cannot reach `/invoicing/*` without un-hiding routes (direct URL still fails because the Route is commented, not merely unlinked).

### Hidden merchant UI (code present)

| Hidden surface | Marker | File | What you would get if uncommented |
|----------------|--------|------|-----------------------------------|
| `/invoicing/quotes` | `[MVP-HIDE]` ADR 023 Phase D.3 | `App.tsx` + `QuotesPage.tsx` | List custom checkouts, create proforma, copy pay URL, mark paid |
| `/invoicing/tax-invoices` | same | `TaxInvoicesPage.tsx` | Ledger sales browser, LHDN badges, PDF download, 72h cancel |
| `/invoicing/credit-notes` | same | `CreditNotesPage.tsx` | Ledger reversals, no manual CN |
| `/workspace/billing-profile` | same | `BillingProfilePage.tsx` | Legal name, TIN, SSM, SST #, logo upload, address |
| Product “Requires TIN” | `[MVP-HIDE]` | `CreateProductForm.tsx` | `requiresTaxId` always `false` |
| Ops chat | `[MVP-HIDE]` | `App.tsx` | Unrelated, listed for completeness |

`App.tsx` header comment names these “floating islands” and says “Re-mount by adding Route entries + Sidebar links; do not delete backends.” Sidebar was **rewritten without an Invoicing module**, so remount is not “uncomment one block” — you must also add a fourth `MODULES` entry and links (Quotes, Tax Invoices, Credit Notes, Legal & Billing).

### Live buyer UI (portal)

| Route | Status | Documents |
|-------|--------|-----------|
| `/{slug}/checkout/{product}` | Live | No invoice. Optional address. TIN hidden. |
| `/{slug}/checkout/{product}/success` | Live | Success view; not a document vault |
| `/{slug}/update-payment/{subId}` | Live | Dunning recovery |
| `/{slug}/portal` | Live | Subscriptions + cancel. **Download Tax Invoice commented out.** |
| `/{slug}/pay/{sessionId}` | **`notFound()`** | Entire QuoteView (proforma, draft PDF, pay) dead |
| Legal pages | Live | Static privacy/terms/refund — not tax docs |

### Hidden buyer UI

| Surface | Marker | Effect |
|---------|--------|--------|
| Quote / proforma page | `[MVP-HIDE]` + `notFound()` | Custom checkout links 404 even if ops copied them |
| TIN / company at checkout | `[MVP-HIDE]` | `tax_id` / `company_name` always `undefined` |
| Portal tax invoice button | `[MVP-HIDE]` | Buyer cannot pull documents from the portal |

### Live backend (dark matter that still runs)

These execute in production even while UI is gone. That is ADR 023’s point — and its risk (receipts go out with empty legal profiles).

| Mechanism | Live? | Notes |
|-----------|-------|-------|
| `POST /admin/commerce/custom-checkouts` | Yes | No UI |
| `GET /public/commerce/{slug}/custom-checkouts/{id}` | Yes | Adds 7-day draft PDF URL |
| `GET /public/billing/{slug}/documents/draft/{id}` | Yes | HMAC; generates PDF in request |
| `GET /public/billing/{slug}/profile` | Yes | 404 if no profile row |
| `GET /admin/billing/ledger` | Yes | No UI except hidden pages |
| `GET /admin/billing/ledger/{id}/document` | Yes | Presign; no object-exists check |
| `GET /public/billing/{slug}/documents/{id}` | Yes, not in TypeSpec | Email link |
| `PUT /admin/billing/profile` | Yes | No UI |
| `GenerateNextSequenceNumber` | Yes | On every B2C payment |
| `GenerateAndStoreDocument` Official Receipt | Yes | R2 `vault/{org}/documents/{ledger}.pdf` |
| `DocumentPublished` → email | Yes if template + email configured | Quotation Ready never triggered |
| `B2cConsolidationJob` | Yes (hosted) | 28th MYT; no RM10k filter |
| LHDN submit/poll/cancel | Yes | Needs tenant MyInvois config + credits |
| LHDN `InvoiceIssued` handler | Registered, **never fed** | Stub TIN |
| LHDN refund cancel / CN | Registered | CN branch may not fully submit |
| `mark-paid` | Yes | Always B2C receipt |
| Dunning engine | Yes | Subscriptions |

### What a buyer actually receives today (honest path)

1. Merchant creates a **product checkout link** (not a quote).
2. Buyer pays on Billplz/Stripe/CHIP/etc.
3. Billing writes a ledger row, allocates `RCPT-2026-00xxx`, renders Official Receipt **without SST number, often without tax line, with “Lazuar Merchant” if profile empty**.
4. If Communications has the Official Receipt template and an email provider, buyer gets a 30-day HMAC link.
5. Buyer cannot see that PDF in `/portal`.
6. No LHDN QR unless a consolidation/submit path validated — and the buyer of an individual B2C sale is **not** emailed the monthly consolidated e-invoice (that document is for IRB, buyer = General Public).

### What a merchant believes they have (support-debt risk)

If anyone unhides Tax Invoices without shipping `InvoiceIssued` + real TIN:

- B2B quotes marked `is_b2b_required` will show **no Official Receipt** and **no tax invoice**.
- LHDN column will say NOT REQUIRED or sit empty.
- “View Ledger Entry” from a completed quote searches ledger by session id; payment rows key `ReferenceId` as **gateway transaction id**, not session id. The search may miss.

### Remount cost (Phase D.3) — not a one-comment job

ADR 023 said “remove the `[MVP-HIDE]` comments.” Reality:

1. Uncomment routes **and** restore Sidebar Invoicing + Legal profile + (optionally) credits/ledger links.
2. Turn `pay/[sessionId]` back on **or** change QuoteDetailPanel to a URL that exists.
3. Decide whether portal download returns Official Receipt, Tax Invoice, or both — and only if a file exists.
4. Do **not** enable `is_b2b_required` / TIN until `InvoiceIssued` is published with CRM-resolved taxpayer data and supplier address templates stop using Lot 66.
5. Print SST + SSM on PDFs if you show a Legal profile; otherwise merchants will fill SST and see nothing.
6. Sequence quotes (`QT-yyyy-#####`) before showing proformas to B2B buyers.
7. Implement RM10k individual e-invoice split before claiming 2026 compliance.

---

## Gap table

Status key for **Us (live UI)** / **Us (code)**: **Y** shipped and reachable · **P** partial or dark · **N** not a job · **X** refuse.

| # | Capability | Stripe | Chargebee | HitPay | Xendit | Xero | Paddle | Us live UI | Us code | Gap / notes |
|---|------------|:------:|:---------:|:------:|:------:|:----:|:------:|:----------:|:-------:|-------------|
| 1 | Create quote / proforma with line items | Y | Y | P (draft invoice) | P (invoice) | Y | N | N | Y | Ops QuotesPage complete; unrouted. Pay page 404. |
| 2 | Email quote automatically | Y | Y | Y | Y | Y | — | N | N | “Quotation Ready” template unused. |
| 3 | Hosted pay-the-quote page | Y | Y | Y | Y | P | Y | N | P | QuoteView exists; `notFound()`. |
| 4 | Quote → invoice conversion (accept) | Y | Y | N | N | Y | N | N | N | We convert to **payment**, not AR invoice. |
| 5 | Quote expiry | Y | Y | Y | Y | Y | — | N | Y | Default 30d. |
| 6 | Distinct due date / Net terms | Y | Y | Y | P | Y | P | N | N | `DueDate` on dead event only. |
| 7 | Invoice reminders (not card dunning) | Y | P | Y | Y | Y | N | N | N | Do not count DunningEngineJob. |
| 8 | Late fees | P | P | Y | N | P | N | N | N | HitPay has this; we should not rush it. |
| 9 | Recurring invoices | Y | Y | Y | P | Y | Y | N | P | Subscriptions exist; they emit receipts, not invoices. |
| 10 | Partial payment / deposit on document | Y | P | Y | N | Y | N | N | N | Refunds only. |
| 11 | Mark paid offline | Y | Y | Y | P | Y | — | N | Y | Bank-transfer button on hidden panel. Always B2C receipt. |
| 12 | Official receipt PDF after pay | Y | Y | P | P | Y | Y | P | Y | Email link live; portal button hidden. |
| 13 | Tax invoice PDF (commercial) | Y | Y | Y | P | Y | Y | N | P | Only after LHDN VALID. |
| 14 | LHDN e-invoice type `01` individual | N | N | N | N | P (intermediary) | N | N | P | Pipeline yes; B2B publisher no; stub TIN; sample supplier address. |
| 15 | LHDN consolidated B2C | N | N | N | N | P | N | N | P | Job live; no RM10k exclusion; tax type default `06`. |
| 16 | LHDN credit note `02` | N | N | N | N | P | N | N | P | >72h path; submit uncertain; buyer stub. |
| 17 | LHDN debit note `03` | N | N | N | N | P | N | N | P | Factory only. |
| 18 | LHDN refund note `04` | N | N | N | N | P | N | N | P | Factory only; refunds use `02`. |
| 19 | Self-billed `11`–`14` | N | N | N | N | P | N | N | P | Strategies + entity swap; no product. |
| 20 | 72h cancel + reason | N | N | N | N | P | N | N | Y | Hidden panel + API. |
| 21 | Sequential receipt numbers | Y | Y | Y | N | Y | Y | N | Y | `RCPT-yyyy-#####`. Invisible. |
| 22 | Sequential quote numbers | Y | Y | P | N | Y | N | N | N | GUID slice. |
| 23 | Sequential credit-note numbers | Y | Y | N | N | Y | Y | N | N | `CN-{paymentId}`. |
| 24 | Configurable number prefix | Y | Y | P | N | Y | N | N | N | Hardcoded `RCPT-`. |
| 25 | Logo on PDF | Y | Y | Y | P | Y | P | N | P | Final only; profile hidden so logo often missing. |
| 26 | Brand colors / memo / footer terms | Y | Y | P | N | Y | N | N | N | Single Helvetica template. |
| 27 | SST registration on PDF | P | P | P | N | Y | N | N | N | Field stored, not printed. |
| 28 | SST / tax **line** calculated | Y | Y | Y | N | Y | Y (MoR) | N | P | Only if gateway sends TaxAmount (Billplz = 0). |
| 29 | Inclusive vs exclusive tax | Y | Y | Y | N | Y | Y | N | N | UI copy only on hidden panel. |
| 30 | Buyer TIN capture | P | P | P | N | Y | Y (VAT ID) | N | P | Hidden fields; quote flag orphan. |
| 31 | TIN validate vs MyInvois | N | N | N | N | P | N | N | Y | `POST /lhdn/taxpayer/validate` + cache. No UI. |
| 32 | Buyer portal document list | Y | Y | N | N | P | Y | N | N | One email link. |
| 33 | HMAC / expiring download | — | — | — | — | — | — | P | Y | Better than naked R2; portal unused. |
| 34 | AR aging / open invoices | Y | Y | Y | P | Y | N | N | N | No open-invoice object. |
| 35 | Apply credit to invoice | Y | Y | N | N | Y | P | N | N | Explicitly refused at UI (integrity). |
| 36 | Xero / accounting sync | Apps | Apps | Apps | Apps | — | Apps | N | N | ADR 021 keep; not built. |
| 37 | Multi-currency documents | Y | Y | Y | Y | Y | Y | N | P | Ledger has FX; quotes hardcoded MYR. |
| 38 | UBL 1.1 signed e-invoice | N | N | N | N | P | N | N | N | Placeholder only. |
| 39 | RM10k individual e-invoice rule (2026) | N | N | N | N | P | N | N | N | Consolidation would mis-batch large B2C. |
| 40 | MoR invoice in **tenant** name | Y | Y | Y | Y | Y | **N** (feature) | Y | Y | Correct: we are not Paddle. Keep. |

### Gaps ranked by Malaysian merchant pain (not by envy)

1. **Buyer cannot pay or download a quote** — HitPay/Xendit/Stripe table stakes. Our code is 80% there; UI is off. Highest leverage remount **if** we do not turn on B2B lies.
2. **No due date / reminders on a document** — the actual meaning of “invoicing.” Without this we are a checkout, not an invoicer.
3. **Receipt PDF is legally thin** — missing SST #, SSM, full address, tax line. Fine for a digital good; weak for an SST registrant.
4. **B2B e-invoice path is a trap** — flag exists, publisher missing, TIN stub, supplier sample address.
5. **2026 RM10k consolidation** — silent worker can produce the wrong legal document.
6. **Credit notes are not a product** — refunds book ledger; LHDN CN is incomplete; no buyer PDF unless VALID regeneration runs.
7. **Portal download hidden** — we already generate the file and email it; hiding the button only increases WhatsApp “pls send invoice.”
8. **Partials / deposits on invoices** — HitPay-class; later than 1–3.
9. **Xero sync** — promised in ADR 021; a connector, not an invoicing MVP.
10. **Debit notes / self-bill / signed 1.1** — keep in LHDN SDK; do not put in Sidebar.

### What to refuse (company-shape, not nits)

| Refuse | Why |
|--------|-----|
| Become Xero | ADR 021 said sync, not clone. Double-entry in Billing is enough for GMV + tax liability. |
| Become Paddle MoR for tenant sales | Wrong seller on the tax invoice. Breaks LHDN. System A only. |
| Manual credit-note composer that bypasses the ledger | Ops page is right: integrity first. |
| “Download Tax Invoice” that is an Official Receipt titled Tax Invoice | Legal lie. Name the PDF what it is. |
| Enable `is_b2b_required` before `InvoiceIssued` + real TIN + real supplier address | Would collect hope and submit fiction. |
| Stripe Tax as Malaysian SST | Different tax. Billplz TaxAmount is already 0. |
| Marketplace invoicing / split invoices / take-rate | Standing constraint. |
| Self-billed affiliate OS to “use” types 11–14 | No payout product. |
| Gapless legal numbering across rollbacks as a v1 promise | Sequence increment is not a journal. |
| Claiming LHDN compliance in marketing while UI is hidden and B2B is orphaned | Support and IRB both punish this. |

---

## Tracker IDs

Mint a Pay-document family **`INV`** for promotion into `00-checklist-tracker.md`. Do **not** overload salon-OS compliance rows with Pay’s full e-invoice stack. Cross-link only where the same **buyer job** appears.

**How to read:** Depth = Lazuar Pay. **V** = suggested verdict for Pay. **W** = suggested wave on the **Pay** resurfacing track (D.3+). Wave **D0** = honesty / unhide without new legal claims. **D1** = commercial invoicing. **D2** = SST stationery. **D3** = LHDN that is not a lie. **—** = refuse or park.

| ID | Feature | Pay depth | Stripe | Chargebee | HitPay | Xendit | Xero | Paddle | V | W | Class | Evidence |
|----|---------|-----------|:------:|:---------:|:------:|:------:|:----:|:------:|---|----|-------|----------|
| INV-001 | Custom quote / proforma with line items | stub (UI hide) | Y | Y | P | P | Y | N | Partial | D0 | table-stakes | `QuotesPage`, `CreateCustomCheckoutCommand`; routes `[MVP-HIDE]` |
| INV-002 | Hosted quote pay page | killed-live (404) | Y | Y | Y | Y | P | Y | Partial | D0 | table-stakes | `pay/[sessionId]/page.tsx` `notFound()`; `QuoteView.tsx` complete |
| INV-003 | HMAC draft proforma PDF | shipped-dark | P | Y | Y | P | Y | N | Partial | D0 | table-stakes | `GenerateDraftDocumentQueryHandler`; public billing draft route |
| INV-004 | Quote email (“Quotation Ready”) | stub | Y | Y | Y | Y | Y | — | Later | D1 | table-stakes | Template seeded; create path does not `DocumentPublished` |
| INV-005 | Quote sequential numbering | none | Y | Y | P | N | Y | N | Later | D1 | hygiene | `QUOTE-{guid8}` |
| INV-006 | Official Receipt PDF on pay | shipped-dark | Y | Y | P | P | Y | Y | Partial | D0 | table-stakes | `GatewayPaymentCompletedHandler` + QuestPDF + R2 |
| INV-007 | Receipt email download link | shipped | Y | Y | P | P | Y | Y | Both | D0 | table-stakes | `DocumentPublishedIntegrationEventHandler`; 30d HMAC |
| INV-008 | Buyer portal document download | stub (hide) | Y | Y | N | N | P | Y | Partial | D0 | table-stakes | portal `[MVP-HIDE]` anchor |
| INV-009 | Sequential `RCPT-yyyy-#####` | shipped-dark | Y | Y | Y | N | Y | Y | Both | D0 | hygiene | `GenerateNextSequenceNumberCommandHandler` |
| INV-010 | Legal & billing profile (TIN, SST, logo, address) | stub (hide) | Y | Y | Y | P | Y | P | Partial | D0 | table-stakes | `BillingProfilePage`; `TenantBillingProfile` |
| INV-011 | Print SST # + SSM + full address on PDF | none | P | P | P | N | Y | N | Later | D2 | table-stakes | Profile fields unused in `BaseInvoiceDocument` |
| INV-012 | SST / tax line calculated on quotes & receipts | none | Y | Y | Y | N | Y | Y | Later | D2 | table-stakes | Default tax type `06`; Billplz `TaxAmount=0` |
| INV-013 | Due date / payment terms (Net 30 etc.) | none | Y | Y | Y | P | Y | P | Later | D1 | table-stakes | No document due date |
| INV-014 | Invoice / quote reminders | none | Y | P | Y | Y | Y | N | Later | D1 | table-stakes | Distinct from subscription dunning |
| INV-015 | Partial payments on a document | none | Y | P | Y | N | Y | N | Later | D1 | later-nice | HitPay-class deposits |
| INV-016 | Mark quote paid offline | stub (hide) | Y | Y | Y | P | Y | — | Partial | D0 | table-stakes | `mark-paid`; always B2C receipt |
| INV-017 | Credit note as refund/cancel artefact | stub (hide) | Y | Y | N | N | Y | Y | Partial | D1 | table-stakes | `CreditNotesPage` + `GatewayRefundCompletedHandler` |
| INV-018 | Debit note product | none | N | N | N | N | P | N | Later | D3 | later-nice | Factory `03` only |
| INV-019 | LHDN type `01` individual (real buyer) | stub | N | N | N | N | P | N | Later | D3 | differentiator | Orphan `InvoiceIssued`; stub TIN |
| INV-020 | LHDN B2C consolidation | shipped-dark | N | N | N | N | P | N | Partial | D3 | differentiator | `B2cConsolidationJob`; needs RM10k rule |
| INV-021 | LHDN 72h cancel | stub (hide) | N | N | N | N | P | N | Partial | D3 | differentiator | Panel + `CancelTaxDocumentCommand` |
| INV-022 | LHDN credit note `02` after 72h | partial | N | N | N | N | P | N | Later | D3 | differentiator | Handler generates; submit/buyer data weak |
| INV-023 | TIN validation UX | none | N | N | N | N | P | N | Later | D3 | differentiator | API exists |
| INV-024 | MyInvois QR on customer PDF | partial | N | N | N | N | P | N | Later | D3 | differentiator | Only after VALID |
| INV-025 | Signed UBL 1.1 | none | N | N | N | N | P | N | Later | — | later-nice | Placeholder in templates |
| INV-026 | Self-billed `11`–`14` | stub | N | N | N | N | P | N | Never* | — | later-nice | *Never as a Sidebar product until payouts exist |
| INV-027 | RM10k individual e-invoice split | none | N | N | N | N | P | N | Later | D3 | hygiene | 2026 mandate |
| INV-028 | Xero / GL export | none | Apps | Apps | Apps | Apps | — | Apps | Later | — | later-nice | ADR 021 keep; not invoicing MVP |
| INV-029 | Configurable PDF brand (color, memo, terms) | none | Y | Y | P | N | Y | N | Later | D1 | later-nice | Single QuestPDF |
| INV-030 | AR open-invoice + aging | none | Y | Y | Y | P | Y | N | Later | D1 | later-nice | Requires job 2 object |
| INV-031 | MoR / Paddle-style vendor invoice | n/a | — | — | — | — | — | Y | Never | — | trap | Wrong seller for tenant SST |
| INV-032 | Manual ledger-bypassing CN | n/a | P | P | N | N | Y | N | Never | — | trap | Keep automated integrity |

\*INV-026 is **Never as a merchant invoicing row**. Keep the strategies. Do not tracker-shame ourselves for not building an affiliate self-bill OS.

### Cross-links to sibling 007-feats chapters

| Sibling | How this file uses it |
|---------|------------------------|
| `01` inventory | Ground truth: invoicing UI is hidden; receipt backend exists |
| `04` Stripe | Stripe Invoicing is the AR ceiling; Stripe Tax ≠ SST |
| `06` SEA fintech | HitPay is the commercial invoice MVP; Xendit is the gateway invoice |
| `07` MoR | Paddle invoices are the wrong seller; refuse INV-031 |
| `08` billing engines | Chargebee quotes/CNs/numbering — copy sequences, not CPQ |
| `10` LHDN competitors | Competitor *filing* map; this file owns *our* document types + hide state |
| `11` subscriptions | Recurring charges emit receipts, not AR invoices |
| `12` dunning | PAST_DUE card recovery ≠ invoice reminders (INV-014) |
| `16` communications | Official Receipt + Quotation Ready templates |

### Suggested Pay remount sequence (not a commitment)

**D0 — Unhide without lying (smallest honest surface)**

- INV-010 Legal profile (so PDFs stop saying “Lazuar Merchant”).
- INV-006/007/009 leave backend on; optionally INV-008 portal download of **Official Receipt only**.
- INV-001 + INV-002 + INV-003 quotes **with `is_b2b_required` forced false** and copy that this is a **payment request / proforma**, not an LHDN tax invoice.
- INV-016 mark-paid.

**D1 — Become a HitPay-class invoicer**

- INV-013 due dates, INV-014 reminders, INV-004 quote email, INV-005/029 numbering + light branding, INV-015 partials only if agencies demand it, INV-017 visible refund credit notes (ledger, not fake LHDN).

**D2 — SST stationery**

- INV-011 print SST/SSM, INV-012 real tax line (product rate + inclusive flag). Still not MyInvois.

**D3 — LHDN that can survive a desk audit**

- Fix supplier address templates, publish `InvoiceIssued` with CRM TIN, INV-023 validate UX, INV-020 RM10k split, INV-019 individual `01`, INV-021/022/024 cancel + CN + QR. Then and only then unmute B2B checkbox and portal “Tax Invoice.”

**Never**

- INV-031 Paddle-as-tenant-invoice.
- INV-032 manual unbalanced credit notes.
- INV-026 as a sidebar module.

### Promotion rule

A sentence in this file is **not** a tracker change. Promote `INV-*` into `00-checklist-tracker.md` only after the parent eval (`00-evaluation.md`) accepts a Pay document family. Until then this table is the working ID space so later programs do not invent a second vocabulary.

---

*End of 15 — Invoicing, quotes, and receipts. Do not condense this file into the README. Do not claim LHDN or SST invoices as live product. Do not remount `[MVP-HIDE]` B2B until INV-019’s publisher and taxpayer data are real.*
