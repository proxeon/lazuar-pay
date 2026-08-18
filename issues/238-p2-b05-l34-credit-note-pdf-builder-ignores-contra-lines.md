---
number: "238"
id: B05-L34
severity: P2
status: open
source: plans/009-bugs/05-billing-ledger-refunds-disputes.md
head: "297ba98"
---

# 238 — B05-L34 — Credit-note PDF builder ignores contra lines

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/05-billing-ledger-refunds-disputes.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B05-L34 — P2 — Credit-note PDF builder ignores contra lines

See §8. Latent until something generates a document for a `GATEWAY_REFUND` row. LHDN validate of a type-02 keyed by the Billing CN number would.

---

## Evaluation (current tree, 2026-08-18)

### What the bug is
At audit time the PDF builder only turned `REVENUE_GROSS` / `REVENUE_RECOGNIZED` into line items. A `GATEWAY_REFUND` journal is almost entirely `CONTRA_REVENUE_REFUNDS` (+ cash + optional tax). Rendering that row as a Credit Note produced subtotal 0, tax = abs(tax line), total = tax only. The refund writer also did not call `GenerateAndStoreDocumentCommand`, so the hole was latent until LHDN VALID regen of a type-02 keyed by the Billing `CN-`.

### Still present?
**ALREADY FIXED**

Likely **106** (`fix/106-credit-note-pdf`): refunds now generate a Credit Note after save, and the builder includes contra lines when `DocumentType == "Credit Note"`.

```84:102:apps/lazuar-api/Modules/Billing/Infrastructure/Commands/GenerateAndStoreDocumentCommandHandler.cs
        var isCreditNote = string.Equals(request.DocumentType, "Credit Note", StringComparison.OrdinalIgnoreCase);
        var sourceLines = entry.Lines.Where(l =>
            l.AccountType == AccountTypes.RevenueGross
            || l.AccountType == AccountTypes.RevenueRecognized
            || (isCreditNote && l.AccountType == AccountTypes.ContraRevenueRefunds)).ToList();
        foreach (var line in sourceLines)
        {
            model.LineItems.Add(new InvoiceLineItemModel
            {
                Description = isCreditNote ? "Refund" : entry.Description ?? "Payment",
                Amount = Math.Abs(line.Amount)
            });
            ...
        }
        model.Subtotal = model.LineItems.Sum(x => x.Amount);
        model.Discount = ...
        model.Tax = entry.Lines.Where(l => l.AccountType == AccountTypes.LiabilityTaxPayable).Sum(l => Math.Abs(l.Amount));
        model.Total = model.Subtotal - model.Discount + model.Tax;
```

Refund writer now always asks for the PDF:

```114:118:apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/GatewayRefundCompletedHandler.cs
        await _mediator.Send(new GenerateAndStoreDocumentCommand(
            @event.OrganizationId,
            booked.Id,
            "Credit Note",
            CorrelationId: @event.PaymentRecordId.ToString()));
```

`LhdnDocumentValidatedIntegrationEventHandler.ResolveDocumentType` still returns `"Credit Note"` for refund / `CN-` rows (`:82-88`), so VALID regen uses the same builder. `DocumentSeries.CustomerFacingNumber` never prints a raw UUID (`DocumentSeries.cs:37-46`).

Residual: `GenerateAndStoreDocumentCommandHandlerTests.CreditNote_UsesContraRevenueLine` only asserts upload + `DocumentType == "Credit Note"`. It does **not** parse QuestPDF totals. A regression that dropped the contra clause would still upload an empty-ish PDF and stay green.

### Related files
- [`apps/lazuar-api/Modules/Billing/Infrastructure/Commands/GenerateAndStoreDocumentCommandHandler.cs`](apps/lazuar-api/Modules/Billing/Infrastructure/Commands/GenerateAndStoreDocumentCommandHandler.cs) — contra included for Credit Notes.
- [`apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/GatewayRefundCompletedHandler.cs`](apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/GatewayRefundCompletedHandler.cs) — now calls generate (106).
- [`apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/LhdnDocumentValidatedIntegrationEventHandler.cs`](apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/LhdnDocumentValidatedIntegrationEventHandler.cs) — VALID regen document type.
- [`apps/lazuar-api/tests/Lazuar.ModuleTests/Billing/Commands/GenerateAndStoreDocumentCommandHandlerTests.cs`](apps/lazuar-api/tests/Lazuar.ModuleTests/Billing/Commands/GenerateAndStoreDocumentCommandHandlerTests.cs) — `CreditNote_UsesContraRevenueLine`.
- [`apps/lazuar-api/tests/Lazuar.ModuleTests/Billing/EventHandlers/GatewayRefundCompletedHandlerTests.cs`](apps/lazuar-api/tests/Lazuar.ModuleTests/Billing/EventHandlers/GatewayRefundCompletedHandlerTests.cs) — `Refund_GeneratesCreditNotePdf`.
- [`issues/106-p1-b06-d21-credit-note-pdf-is-never-generated-on-refund-lhdn-handler-can-mi.md`](issues/106-p1-b06-d21-credit-note-pdf-is-never-generated-on-refund-lhdn-handler-can-mi.md) — resolved sibling that made this path live.

### Tests
- Existing: `GenerateAndStoreDocumentCommandHandlerTests.CreditNote_UsesContraRevenueLine`; `GatewayRefundCompletedHandlerTests.Refund_GeneratesCreditNotePdf`.
- Those tests stay green if someone removes the contra filter (they do not inspect `model.Subtotal` / `model.Total`). They would fail if generate stopped being called on refund (106’s lock).
- First *hardening* test (not required to “fix” this issue): after `Handle(..., "Credit Note")` on a 100 contra + 8 tax refund, assert factory/model `Subtotal == 100`, `Tax == 8`, `Total == 108` (or extract a test hook). Do not mark this issue resolved in YAML from this evaluation.

### Reproduction today
Arrange a `GATEWAY_REFUND` with contra 100 / tax 8 / cash −108 and `AssignCustomerDocumentNumber("CN-2026-00001")`. Act: `GenerateAndStoreDocumentCommand(..., "Credit Note")`. Assert: R2 upload happens; line-item source includes the contra (code path above). Ops `GET /admin/billing/ledger/{id}/document` should no longer be an empty tax-only CN for that row (still a presign; file exists if generate succeeded).

### Blast radius
Was merchant-facing CN PDF + LHDN VALID regen. Live now that 106 generates on every refund. Current code books subtotal from contra, so a 108 refund prints 100 + 8 tax, not “tax only”. Residual risk is the weak test, not the production filter.

### Suggested fix
No product change. If touching this path, tighten `CreditNote_UsesContraRevenueLine` to assert subtotal/tax/total so L34 cannot silently return. Do not TypeSpec-regen. Do not emit `subscription.updated`.

### Evaluation notes
009 §8 is stale. Do **not** flip YAML `status` here. Still filed P2; implementer can treat as already done and only add the assertion. Related: **106** (generate on refund), **111** (email template fallback), **093** (type-02 including-tax math — UBL, not this PDF).

