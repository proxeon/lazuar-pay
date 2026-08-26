---
number: "249"
id: B06-D30
severity: P2
status: resolved
resolved_branch: fix/249-draft-proforma-identity
source: plans/009-bugs/06-lhdn-invoices-documents.md
head: "297ba98"
---

# 249 — B06-D30 — Draft proforma identity and date are thin

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/06-lhdn-invoices-documents.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B06-D30 — Draft proforma identity and date are thin (P2)

Draft customer is session CRM `FullName` + email only (`CommerceDocumentLookup.cs:86–89`, `GenerateDraftDocumentQueryHandler.cs:66–68`). TIN is not printed. Issue date is `DateTime.UtcNow` at download (`73`), not session created-at — the date **moves** on every click. Currency is hardcoded `MYR` (`78`). Quote SST does not exist (CreateQuoteModal totals `qty * unit_price` only).

## Evaluation (current tree, 2026-08-18)

### What the bug is
The HMAC draft proforma (`GET /public/billing/{slug}/documents/draft/{sessionId}`) is a thin document. Commerce loads the quote session’s CRM profile but the draft DTO only carries `FullName` + email, so buyer TIN / company / address never reach QuestPDF. The PDF issue date is `DateTime.UtcNow` at **download**, so every click reprints a new date. Currency is hardcoded `MYR`. Ops Create Quote totals `qty * unit_price` with no SST line. A buyer who downloads the draft twice can get two different dates and a net-only total that no longer matches QuoteView when the merchant has an SST id.

### Still present?
**PARTIAL**

Draft identity is still name+email only. `DraftCheckoutSessionDisplay` has no TIN/company fields (`ICommerceDocumentLookup.cs:68–72`). Lookup still drops the CRM profile down to two strings even though `GetClientProfileAsync` is already called:

```85:89:apps/lazuar-api/Modules/Commerce/Infrastructure/Services/CommerceDocumentLookup.cs
        return new DraftCheckoutSessionDisplay(
            CustomerName: profile?.Full_name ?? "Customer",
            CustomerEmail: profile?.Email ?? "",
            AdHocLineItemsJson: (string?)sessionData.AdHocLineItems,
            DocumentNumber: (string?)sessionData.DocumentNumber);
```

Handler still builds a two-arg customer, stamps now, hardcodes MYR, and sets `Total = Subtotal` (no tax):

```66:84:apps/lazuar-api/Modules/Billing/Infrastructure/Queries/GenerateDraftDocumentQueryHandler.cs
        var customer = new CommerceCustomerDisplay(
            sessionData.CustomerName ?? "Customer",
            sessionData.CustomerEmail ?? "");
        var model = InvoiceDocumentFactory.CreateHeader(
            "Proforma Invoice",
            quoteNumber,
            DateTime.UtcNow,
            ...
        model.Currency = "MYR";
        ...
        model.Subtotal = model.LineItems.Sum(x => x.Amount);
        model.Total = model.Subtotal;
```

Create Quote modal is still net-only (`CreateQuoteModal.tsx:27–29`, `193–195`).

What moved: **034** (`f1f7ba03`) made QuoteView show exclusive SST when the merchant has an SST id (`QuoteView.tsx:61`, `277–286` via `customQuoteBreakdown`). Hop-2 custom initiate also stamps SST metadata (`InitiateCheckoutCommandHandler.cs:120–142`). The **draft PDF** and **ops create modal** do not. QuoteView on-screen date uses `checkout.created_at` (`QuoteView.tsx:183`); the PDF does not.

### Related files
- `apps/lazuar-api/Modules/Commerce/Infrastructure/Services/CommerceDocumentLookup.cs` — draft DTO omits TIN/company.
- `apps/lazuar-api/Modules/Commerce/Contracts/ICommerceDocumentLookup.cs` — `DraftCheckoutSessionDisplay` shape.
- `apps/lazuar-api/Modules/Billing/Infrastructure/Queries/GenerateDraftDocumentQueryHandler.cs` — UtcNow / MYR / no tax.
- `apps/lazuar-api/Modules/Billing/Infrastructure/Documents/InvoiceDocumentFactory.cs` — would print TIN if the display object had it.
- `apps/lazuar-ops/src/modules/invoicing/components/CreateQuoteModal.tsx` — net total only.
- `apps/lazuar-portal/src/modules/checkout/components/QuoteView.tsx` — UI SST + created_at (034).
- `apps/lazuar-portal/src/modules/checkout/lib/grossBreakdown.ts` — `customQuoteBreakdown`.
- `apps/lazuar-api/Modules/Billing/Infrastructure/Endpoints/PublicBillingEndpoints.cs` — draft HMAC GET.
- `issues/034-p1-b01-c08-custom-quotes-and-offline-mark-paid-never-apply-sst-on-first-cha.md` — hop-2 / QuoteView SST, not the draft PDF.
- `issues/229-p2-b05-l25-document-year-is-utc-not-myt.md` — adjacent UTC-vs-MYT numbering.

### Tests
- `GenerateDraftDocumentQueryHandlerTests.Handle_UsesPersistedQuoteNumber_AndWorkspaceNameWhenNoProfile` — PDF starts with `%` and uses mocked name/email. Does **not** assert issue date, currency, TIN, or SST.
- `GenerateDraftDocumentQueryHandlerTests.Handle_UnknownSession_Throws`.
- `CommerceDocumentLookupTests` cover **final** `GetCustomerForDocument` (TIN on paid docs after CRM fallback), not `GetDraftCheckoutSessionAsync`.
- `CommerceDocumentLookupBoundaryTests.CommerceDocumentLookup_Has_No_Crm_Schema_Sql` — no crm JOIN (does not require TIN on the draft DTO).
- No test would fail if the date kept moving or TIN stayed off the draft.
- First regression: persist a session created-at + CRM TIN; generate the draft twice; assert issue date equals session created-at (not now) and `CustomerTin` is printed; if merchant has SST id, draft total equals `customQuoteBreakdown` / hop-2 gross.

### Reproduction today
Create a quote in ops (total is qty×price). Open `/{slug}/pay/{id}` with an SST-registered merchant: QuoteView shows SST 8%. Download the draft PDF twice, minutes apart: date changes, no buyer TIN, total is net, currency MYR. Compare to QuoteView’s created_at date.

### Blast radius
Buyer-facing commercial PDF (proforma, not a tax invoice). Wrong date and missing TIN are embarrassing and can mismatch the paid INV later (013 still matters on the paid path). SST mismatch vs QuoteView is money-shaped on the **draft** only; hop-2 charge uses GrossBreakdown after 034. Frequency: every draft download.

### Suggested fix
Widen `DraftCheckoutSessionDisplay` to pass CRM TIN/company/address already loaded in `GetDraftCheckoutSessionAsync`. Use session `CreatedAt` (add it to the DTO) instead of `DateTime.UtcNow`. Apply the same `CustomQuoteBreakdown` as hop-2 when the merchant has an SST id; keep CreateQuoteModal net **or** show the same SST preview. Do not title the draft “Tax Invoice.” Currency: do not invent FX; if quotes are MYR-only, say so in the handler comment. No TypeSpec regen unless the draft DTO is public (it is not).

### Evaluation notes
034 fixed QuoteView/hop-2 SST, not this PDF. 013 is the paid-document identity sibling. 229 is UTC numbering, not draft issue date. Still P2 as a thin proforma; SST draft mismatch is the sharpest leftover. Not blocked.

## Resolution

Draft DTO carries CRM identity + session `CreatedAt`. Proforma date is session created-at. Exclusive 8% SST applies when the merchant billing profile has an SST id (same as hop-2 custom quote). Currency stays MYR. CreateQuoteModal remains net.

