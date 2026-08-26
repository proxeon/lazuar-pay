---
number: "251"
id: B06-D32
severity: P2
status: resolved
resolved_branch: fix/251-needs-buyer-tin
source: plans/009-bugs/06-lhdn-invoices-documents.md
head: "297ba98"
---

# 251 — B06-D32 — Large B2C `NEEDS_BUYER_TIN` has no resolution product

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/06-lhdn-invoices-documents.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B06-D32 — Large B2C `NEEDS_BUYER_TIN` has no resolution product (P2)

Pay-time (`GatewayPaymentCompletedHandler.cs:94–98`) and the cons job (`B2cConsolidationJob.cs:225–230`) both park above-threshold B2C as `NOT_REQUIRED` / `NEEDS_BUYER_TIN`. There is no flow that then collects a TIN. They sit in ops forever. Honesty of the badge is fine. Completeness of the product is not.

## Evaluation (current tree, 2026-08-18)

### What the bug is
Malaysian B2C sales above `Lhdn:B2cIndividualThresholdMyr` (default RM 10,000 from 1 Jan 2026) must not go into the monthly General Public consolidation. Pay-time and the 28th job correctly park those rows as `ConsolidationStatus=NOT_REQUIRED` and `LhdnValidationStatus=NEEDS_BUYER_TIN`. That badge is honest. There is still no ops or portal flow to collect a buyer TIN / ID pair, convert the row to B2B, and file an individual type `01`. Staff see a rose “NEEDS BUYER TIN” chip with no button. The sale stays a commercial Official Receipt forever.

### Still present?
**STILL BROKEN**

Pay-time park (lines moved from the audit’s 94–98):

```99:108:apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/GatewayPaymentCompletedHandler.cs
            if (!isB2b)
            {
                var receiptNumber = await _mediator.Send(
                    new GenerateNextSequenceNumberCommand(@event.OrganizationId, DocumentSeries.ReceiptPrefix()), ct);
                entry.AssignB2cReceipt(receiptNumber);
                if (@event.AmountPaid > _b2cIndividualThresholdMyr)
                {
                    entry.MarkConsolidationNotRequired();
                    entry.UpdateLhdnStatus(null, LhdnValidationStatuses.NeedsBuyerTin);
                }
```

Job defense-in-depth:

```234:238:apps/lazuar-api/Modules/Billing/Infrastructure/Workers/B2cConsolidationJob.cs
            if (PaidAmount(entry) > _individualThresholdMyr)
            {
                entry.MarkConsolidationNotRequired();
                entry.UpdateLhdnStatus(null, LhdnValidationStatuses.NeedsBuyerTin);
```

Ops only paints the badge (`TaxInvoiceDetailPanel.tsx:166–168` `case "NEEDS_BUYER_TIN"`). Grep of collect/resolution/TIN-capture flows under `apps/lazuar-ops` for this status is just the badge and a cancel-reason placeholder. Constant: `AccountTypes.cs:48` `NeedsBuyerTin = "NEEDS_BUYER_TIN"`.

### Related files
- `apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/GatewayPaymentCompletedHandler.cs` — pay-time park.
- `apps/lazuar-api/Modules/Billing/Infrastructure/Workers/B2cConsolidationJob.cs` — job park.
- `apps/lazuar-api/Modules/Billing/Domain/AccountTypes.cs` — status constant.
- `apps/lazuar-ops/src/modules/invoicing/components/TaxInvoiceDetailPanel.tsx` — badge, no action.
- `apps/lazuar-ops/src/modules/invoicing/pages/TaxInvoicesPage.tsx` — list badge `REJECTED`/`NEEDS_BUYER_TIN`.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Billing/Workers/B2cConsolidationJobTests.cs` — `OverThreshold_B2c_IsExcludedFromBatch`.
- `plans/007-feats/impl/W2-LP-114-done.md` — named the park; did not ship a collector.
- `issues/094-p1-b06-d06-ops-portal-teach-tax-invoice-valid-on-objects-that-are-not-clear.md` / `issues/141-…` — do not relabel these receipts VALID.

### Tests
- `B2cConsolidationJobTests.OverThreshold_B2c_IsExcludedFromBatch` **locks the park** (`LhdnValidationStatus == NeedsBuyerTin`, excluded from cons event). It would **fail** if we stopped parking; it would **not** fail if no resolution product exists.
- `GatewayPaymentCompletedHandlerTests` (`HandleAsync_WhenB2C_SavesChangesBeforeGeneratingDocument`, `HandleAsync_WhenB2C_PassesSubscriptionIdAsDocumentCorrelation`) use RM 100 and do not assert the threshold branch.
- No ops/API test for “attach TIN → flip to B2B → `B2bTaxInvoiceRequested`.”
- First regression for a future product: after collecting a valid TIN/ID on a `NEEDS_BUYER_TIN` row, assert status leaves `NEEDS_BUYER_TIN`, a type `01` is submitted, and the row is not in the next `B2C-CONS`.

### Reproduction today
Pay a B2C product for RM 10,000.01 (threshold default 10000). Ledger: `RCPT-…`, `NOT_REQUIRED`, `NEEDS_BUYER_TIN`, Official Receipt PDF. Open Sales documents → detail: rose badge, download, no “collect TIN” / “convert to tax invoice.” Run the cons job: row stays out of `B2C-CONS`. Wait forever: same badge.

### Blast radius
Compliance completeness, not a false VALID. Large B2C GMV is legally supposed to become an individual e-invoice once a TIN exists; we never ask. Merchants who think the badge is a work queue will pile rows. Money is already collected. Frequency: every B2C payment above the configured threshold (and any backfilled row the job sees).

### Suggested fix
Smallest honest increment: keep the badge and add an ops action that writes CRM TIN + ID pair, flips customer type to B2B, allocates `INV-` if needed, and publishes `B2bTaxInvoiceRequested` (reuse the live handler). Do **not** silently put the row into consolidation. Do not title the existing Official Receipt “Tax Invoice” before VALID (012 / 094). No TypeSpec unless the new command is public. No Wave 5 / WhatsApp TIN chase unless product asks later.

### Evaluation notes
Honesty of the badge is still correct (do not “fix” it to VALID). W2-LP-114 shipped the park only. 108/026/078 are cons idempotency, not this gap. Still P2 as a missing product; legally sharper if a merchant is already live on large B2C. Not blocked.

## Resolution

Ops Sales document detail can collect TIN/ID and file type 01. Command writes CRM via `ICommerceBuyerIdentity`, converts the row to B2B (stays out of consolidation), and publishes `B2bTaxInvoiceRequested`. Receipt number is kept. Not titled Tax Invoice until VALID.

