---
number: "252"
id: B06-D33
severity: P2
status: open
source: plans/009-bugs/06-lhdn-invoices-documents.md
head: "297ba98"
---

# 252 — B06-D33 — Buyer reject is not implemented

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/06-lhdn-invoices-documents.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B06-D33 — Buyer reject is not implemented (P2, honestly labelled)

Ops footer: “Supplier cancel only… Buyer reject is not implemented.” (`TaxInvoiceDetailPanel.tsx:296–298`). True. No portal reject button, no IRBM reject webhook consumer. Domain cancel is 72h from **local** `ValidatedAt` (`CancelWindowMustBeValidRule.cs:12–26`), which is `DateTime.UtcNow` at `MarkAsValid`, not IRBM’s clock. Close enough for a first cut; not proven.

Cancel applies `doc.Cancel()` **before** the gateway call (`CancelTaxDocumentCommand.cs:50–58`). If the gateway succeeds and `SaveChanges` fails, MyInvois is cancelled and Lazuar still shows VALID. Next cancel attempt will 400 at LHDN. Narrow window, real split-brain.

## Evaluation (current tree, 2026-08-18)

### What the bug is
Two related gaps. Product: MyInvois buyer reject (IRBM / buyer-side reject of a VALID e-invoice) is not implemented — no portal button, no inbound reject webhook consumer. Ops already says so. Domain cancel is supplier-only and measures 72 hours from **local** `ValidatedAt` (`DateTime.UtcNow` in `MarkAsValid`), not IRBM’s clock. Persistence: `CancelTaxDocumentCommand` runs `doc.Cancel()` (in-memory VALID → CANCELLED) then the gateway; `SaveChanges` is last. If MyInvois accepts the cancel and `SaveChanges` fails, IRBM is cancelled and Lazuar still shows VALID. The next cancel attempt 400s at LHDN. That split-brain is real and narrow.

### Still present?
**STILL BROKEN**

Footer is still honest (line numbers moved):

```327:328:apps/lazuar-ops/src/modules/invoicing/components/TaxInvoiceDetailPanel.tsx
                Supplier cancel only, within 72 hours of MyInvois VALID. Buyer reject is not implemented.
```

No portal reject control; grep of buyer-reject / IRBM reject webhook consumers in `*.cs` / portal `*.tsx` is empty (ops `REJECTED` is a badge color only). Window rule still uses local `ValidatedAt`:

```12:26:apps/lazuar-api/Modules/Lhdn/Domain/Rules/CancelWindowMustBeValidRule.cs
    public CancelWindowMustBeValidRule(DateTime? validatedAt)
    ...
        return DateTime.UtcNow > _validatedAt.Value.AddHours(AllowedHours);
    public string Message => $"Documents can only be cancelled within {AllowedHours} hours of successful validation.";
```

`MarkAsValid` still stamps `ValidatedAt = DateTime.UtcNow` (`TaxDocument.cs:97`). Cancel-before-persist is unchanged:

```49:62:apps/lazuar-api/Modules/Lhdn/Application/Commands/CancelTaxDocumentCommand.cs
        doc.Cancel();
        ...
        var result = await _gatewayAdapter.CancelDocumentAsync(...);
        if (!result.Success)
        {
            throw new InvalidOperationException($"LHDN Cancellation failed: {result.ErrorMessage}");
        }
        await _eventBus.PublishAsync(new LhdnDocumentCancelledIntegrationEvent(...));
        await _repository.SaveChangesAsync(ct);
```

Gateway success + persist failure: in-memory doc is CANCELLED but the tracked instance never flushes, so the next load is still VALID.

### Related files
- `apps/lazuar-ops/src/modules/invoicing/components/TaxInvoiceDetailPanel.tsx` — honest footer + supplier cancel modal.
- `apps/lazuar-api/Modules/Lhdn/Application/Commands/CancelTaxDocumentCommand.cs` — order of cancel / gateway / save.
- `apps/lazuar-api/Modules/Lhdn/Domain/Aggregates/TaxDocument.cs` — `MarkAsValid` / `Cancel`.
- `apps/lazuar-api/Modules/Lhdn/Domain/Rules/CancelWindowMustBeValidRule.cs` — 72h from local clock.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Lhdn/CancelTaxDocumentCommandTests.cs` — happy path + 72h refuse + unknown id.
- `apps/lazuar-api/Modules/Lhdn/Infrastructure/EventHandlers/GatewayRefundCompletedIntegrationEventHandler.cs` — full refund ≤72h also sends this cancel.
- `issues/107-p1-b06-d22-original-document-resolution-can-walk-the-wrong-key-cancel-refun.md` — cancel+refund double ledger.

### Tests
- `CancelTaxDocumentCommandTests.ValidWithinWindow_CallsGatewayAndCancels` — gateway called, in-memory status CANCELLED. Does **not** assert `SaveChanges` after gateway, and does not simulate SaveChanges failure.
- `CancelTaxDocumentCommandTests.After72Hours_DomainRefuses` — reflection-sets `ValidatedAt` −80h; does not compare to an IRBM timestamp.
- `CancelTaxDocumentCommandTests.UnknownInternalId_Throws`.
- Refund tests send `CancelTaxDocumentCommand` on full refund ≤72h; they do not cover reject.
- No test would fail because buyer reject is missing. No test would fail on the split-brain.
- First regression: (1) no public reject route exists (or it 404s); (2) handler calls gateway **before** mutating persisted status, or uses an outbox so a persist failure retries without a second LHDN cancel; (3) after simulated SaveChanges failure the reloaded row is not left VALID while the adapter reported success.

### Reproduction today
Validate a type `01` (or use a fixture VALID doc). Within 72h, ops Cancel e-Invoice: MyInvois state cancel runs; Lazuar becomes CANCELLED only after SaveChanges. Kill the DB after a successful gateway response to see the split. After 72h the button is “Cancel window closed — issue a credit note.” Portal has no reject. There is no Lhdn webhook for buyer reject.

### Blast radius
Buyer reject: product gap, honestly labelled — do not sell it. Split-brain: rare but ugly (IRBM cancelled, ops still VALID, next cancel 400). Local 72h clock can be minutes off IRBM; first-cut acceptable. Frequency: every supplier cancel; reject never.

### Suggested fix
Do **not** implement buyer reject in this P2 unless product explicitly wants IRBM inbound state (out of wrap-rails / Wave 5). Keep the footer. For the split-brain: persist a `CANCEL_PENDING` (or call gateway first and only `doc.Cancel()` after success, then SaveChanges in the same handler with a compensating note if save fails). Do not invent a reject webhook. No TypeSpec regen for a feature that does not exist.

### Evaluation notes
Honestly labelled, still a gap. Split-brain is the only code defect worth a small PR. 091/103 made **submit deduct** fail-closed; cancel persist is still fail-open in the opposite direction. 107 is the commercial double-row on refund+cancel. Still P2. Do not mark resolved because the footer is true.

