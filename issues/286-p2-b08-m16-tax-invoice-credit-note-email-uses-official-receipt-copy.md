---
number: "286"
id: B08-M16
severity: P2
status: open
source: plans/009-bugs/08-communications-messaging-crm.md
head: "297ba98"
---

# 286 — B08-M16 — Tax Invoice / Credit Note email uses Official Receipt copy

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/08-communications-messaging-crm.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B08-M16 — P2 — Tax Invoice / Credit Note email uses Official Receipt copy

**Where:** `DocumentPublishedIntegrationEventHandler.cs` 38–59; catalog has neither name (`DefaultMessageTemplates.cs` 23–87).

**What:** Fallback is intentional in code. Subject is still “Your official receipt from {business}.” W4-LP-100 fixed the **PDF** disclaimer. The email still says receipt. Event has no amount (`DocumentPublishedIntegrationEvent.cs` 10–18).

---

## Evaluation (current tree, 2026-08-18)

### What the bug is
At audit HEAD, Tax Invoice / Credit Note document-published mail fell back to the Official Receipt template, so the subject was “Your official receipt from {business}” while the PDF (W4-LP-100) said Tax Invoice. **111** (`fix/111-tax-invoice-email-fallback`, commit `24da2941`) removed that fallback and added catalog definitions **Tax Invoice** and **Credit Note** with matching subjects/bodies. The handler now maps document type → exact template name and **returns without sending** if that template is missing. Entitlement seeding (`DefaultMessageTemplates.CreateAllForTenant`) includes both names. Residual honesty: `DocumentPublishedIntegrationEvent` still has no amount/currency, so a custom template cannot print money (PDF holds the figures). That is not the filed “uses Official Receipt copy” bug.

### Still present?
**ALREADY FIXED**

No Official Receipt fallback remains:

```39:54:apps/lazuar-api/Modules/Communications/Infrastructure/EventHandlers/DocumentPublishedIntegrationEventHandler.cs
        var preferredTemplate = @event.DocumentType switch
        {
            "Official Receipt" => "Official Receipt",
            "Draft Quotation" => "Quotation Ready",
            "Proforma Invoice" => "Quotation Ready",
            "Tax Invoice" => "Tax Invoice",
            "Credit Note" => "Credit Note",
            _ => null
        };
        if (preferredTemplate == null) return;

        var template = await _dbContext.MessageTemplates
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.OrganizationId == @event.OrganizationId && t.Name == preferredTemplate);

        if (template == null) return;
```

Catalog now has both names (`DefaultMessageTemplates.cs:70–86`) — subjects “Your tax invoice from {{business_name}}” / “Your credit note from {{business_name}}”. Tests: `DocumentPublished_TaxInvoice_DoesNotFallBackToOfficialReceipt`, `DocumentPublished_TaxInvoice_UsesTaxInvoiceTemplate`. Event still has no money (`DocumentPublishedIntegrationEvent.cs:10–18`).

Likely issue/commit: **111** / `fix/111-tax-invoice-email-fallback` / `24da2941`.

### Related files
- `apps/lazuar-api/Modules/Communications/Infrastructure/EventHandlers/DocumentPublishedIntegrationEventHandler.cs` — exact-name match, no fallback.
- `apps/lazuar-api/Modules/Communications/Domain/DefaultMessageTemplates.cs` — Tax Invoice / Credit Note copy.
- `apps/lazuar-api/Modules/Billing/Contracts/Events/DocumentPublishedIntegrationEvent.cs` — still no amount (residual).
- `apps/lazuar-api/Modules/Billing/Infrastructure/Commands/GenerateAndStoreDocumentCommandHandler.cs` — publisher.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Communications/DocumentPublishedIntegrationEventHandlerTests.cs` — locks the 111 behavior.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Communications/DefaultMessageTemplatesTests.cs` — catalog contains both names.

### Tests
- Existing: `DocumentPublished_TaxInvoice_DoesNotFallBackToOfficialReceipt` (Official Receipt seeded, Tax Invoice event → no dispatch); `DocumentPublished_TaxInvoice_UsesTaxInvoiceTemplate` (subject/body contain “tax invoice”); `HandleAsync_EnrichedEvent_DispatchesWithSubstitutedTemplateAndDocumentLink`; `DocumentPublished_ProformaInvoice_UsesQuotationReadyTemplate`; `DefaultMessageTemplatesTests.Catalog_IncludesLifecycleAndDocumentTemplatesOnly`.
- Those **would fail** if the Official Receipt fallback returned. They would **not** fail if the event still lacked amount (no test asks for `{{amount}}` on this path).
- Residual test if someone extends this ticket: Tax Invoice custom body `Total {{amount}}` should either substitute a real figure or reject the tag at save (287) — today the tag would survive as `{{amount}}` because this handler only replaces customer/business/document_link.

### Reproduction today
Arrange: tenant with seeded catalog (or only Official Receipt). Act: publish `DocumentPublishedIntegrationEvent` `DocumentType: "Tax Invoice"`. Assert: with Tax Invoice template present, subject contains “tax invoice” not “official receipt”; with only Official Receipt present, **no** email (111). Credit Note same. Do not expect an amount in the mail.

### Blast radius
Was: B2B buyers getting a “receipt” email for a Tax Invoice PDF (teachability / LHDN honesty). Now: that lie is gone for seeded tenants. Residual: tenants who deleted the Tax Invoice template get silence instead of a wrong-copy mail (better). Event-without-amount is ops/custom-template only. No money path. Frequency: every B2B tax invoice / CN publish.

### Suggested fix
Do not re-introduce fallback. If product wants amount in the mail, denormalize it onto `DocumentPublishedIntegrationEvent` at the Billing publisher and replace it in the handler — that is a small follow-on, not a reopen of 111. Do not regenerate TypeSpec unless you add a public DTO. Leave PDF disclaimer (W4-LP-100) alone.

### Evaluation notes
Duplicate of **111 / B06-D29** (resolved). Keep this file open in YAML (do not flip `status`) but implementers should treat the titled bug as done. Residual no-amount is honesty-only and overlaps 287’s “unknown tags left in place.” **292** is unrelated. No longer P2 for the receipt-copy claim.

