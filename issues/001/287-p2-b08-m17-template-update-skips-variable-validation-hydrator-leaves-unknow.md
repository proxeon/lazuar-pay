---
number: "287"
id: B08-M17
severity: P2
status: open
source: plans/009-bugs/08-communications-messaging-crm.md
head: "297ba98"
---

# 287 — B08-M17 — Template update skips variable validation; hydrator leaves unknown tags

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/08-communications-messaging-crm.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B08-M17 — P2 — Template update skips variable validation; hydrator leaves unknown tags

**Where:** `UpdateMessageTemplateCommandHandler` 99–108 vs `CreateMessageTemplateCommandHandler` 27, 47–87; `MessageTemplateHydratorTests` 59–63 (locks the leftover).

**What:** Create is strict. Update is a content dump. `{{garbage}}` ships. `{{fulfillment_url}}` and `{{document_link}}` are not in the shared hydrator at all — only in two local replace loops. A dunning step that copies those tags from the wiki’s fulfillment section will send the raw tag.

---

## Evaluation (current tree, 2026-08-18)

### What the bug is
Two claims. (1) Create validated `{{tags}}` against required∪optional; Update dumped subject/bodies with no check, so `{{garbage}}` shipped. (2) Shared `MessageTemplateHydrator.Populate` does not know `{{fulfillment_url}}` or `{{document_link}}` — those are local `.Replace` loops in `OrderCompletedDigitalDeliveryHandler` and `DocumentPublishedIntegrationEventHandler`. A dunning step (or any hydrator caller) that copies those tags from the wiki’s fulfillment section sends the raw tag. **129** (`fix/129-email-html-encode`, commit `5ddd873e`) made Update call the same `Validate` as Create. Claim (1) is fixed on `PUT /admin/communications/templates/{id}`. Claim (2) and the hydrator’s “leave unknown tags” behavior are still locked in by `MessageTemplateHydratorTests.Populate_UnknownTag_IsNotStripped`. Dunning campaign step copy does not go through Update validation.

### Still present?
**PARTIAL**

Update now validates:

```99:115:apps/lazuar-api/Modules/Communications/Application/Commands/MessageTemplateCommandHandlers.cs
    public async Task Handle(UpdateMessageTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await _repository.GetTemplateByIdAsync(request.OrganizationId, request.TemplateId, cancellationToken);

        if (template == null) throw new InvalidOperationException("Template not found.");

        CreateMessageTemplateCommandHandler.Validate(
            template.Channel,
            request.Subject,
            request.EmailBody,
            request.WhatsAppBody,
            template.RequiredVariables,
            template.OptionalVariables);

        template.UpdateContent(request.Subject, request.EmailBody, request.WhatsAppBody);

        await _repository.SaveChangesAsync(cancellationToken);
    }
```

Hydrator still does not map fulfillment/document (only preview special-cases fulfillment):

```78:92:apps/lazuar-api/Modules/Communications/Application/MessageTemplateHydrator.cs
        return text
            .Replace("{{customer_name}}", name, StringComparison.OrdinalIgnoreCase)
            // ... amount / links ...
            .Replace("{{update_payment_link}}", update, StringComparison.OrdinalIgnoreCase);
```

```113:117:apps/lazuar-api/Modules/Communications/Application/MessageTemplateHydrator.cs
    public static string PopulatePreview(string? text)
    {
        var populated = Populate(text, Preview, htmlEncode: true);
        if (string.IsNullOrEmpty(populated)) return populated;
        return populated.Replace("{{fulfillment_url}}", PreviewFulfillmentUrl, StringComparison.OrdinalIgnoreCase);
```

Unknown tags still pass through (`MessageTemplateHydratorTests.cs:80–85`). Local loops: `OrderCompletedDigitalDeliveryHandler.cs:96`, `DocumentPublishedIntegrationEventHandler.cs:67–75`.

Likely fix for claim (1): **129** / `5ddd873e` (“Update still validates template variables the same way create does.”).

### Related files
- `apps/lazuar-api/Modules/Communications/Application/Commands/MessageTemplateCommandHandlers.cs` — Create + Update Validate.
- `apps/lazuar-api/Modules/Communications/Application/MessageTemplateHydrator.cs` — shared populate; no document/fulfillment.
- `apps/lazuar-api/Modules/Communications/Infrastructure/Endpoints/TemplateEndpoints.cs` — `PUT /templates/{id}` (line 75).
- `apps/lazuar-api/Modules/Communications/Infrastructure/EventHandlers/OrderCompletedDigitalDeliveryHandler.cs` — local `{{fulfillment_url}}`.
- `apps/lazuar-api/Modules/Communications/Infrastructure/EventHandlers/DocumentPublishedIntegrationEventHandler.cs` — local `{{document_link}}`.
- `apps/lazuar-api/Modules/Communications/Infrastructure/EventHandlers/FulfillmentRequestedIntegrationEventHandler.cs` — dunning uses hydrator only.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Communications/MessageTemplateHydratorTests.cs` — leftover lock.

### Tests
- Existing: `MessageTemplateHydratorTests.Populate_UnknownTag_IsNotStripped` (asserts `{{garbage}}` survives); `Populate_EveryContextField_RoundTrips` (no fulfillment/document); `PopulateHtml_Encodes_Untrusted_Name_And_Drops_Javascript_Url` (129). **No** `UpdateMessageTemplateCommandHandler` test that `{{garbage}}` now throws `BusinessRuleValidationException`.
- `Populate_UnknownTag_IsNotStripped` would **fail** if you stripped unknown tags — it locks the remaining half. Update validation has no test: a revert of the 129 Validate call would stay green.
- First remaining regression: Update of Payment Failed with `{{garbage}}` → 400 / business rule (same message as Create). Second: dunning step body containing `{{document_link}}` or `{{fulfillment_url}}` either hydrates or is rejected at campaign-save, not shipped raw. Add an Update handler test; do not “fix” leftover by deleting `Populate_UnknownTag_IsNotStripped` without a product decision.

### Reproduction today
Arrange: catalog Payment Failed for tenant T. Act A: `PUT /api/v1/admin/communications/templates/{id}` with body containing `{{garbage}}`. Assert: **now** `BusinessRuleValidationException` “Unsupported variables…” (129). Act B: dunning campaign step email_body `Download {{document_link}}` (wiki fulfillment section). Assert: buyer mail still contains the literal `{{document_link}}` because `FulfillmentRequestedIntegrationEventHandler` uses `MessageTemplateHydrator.Populate` only. Act C: `MessageTemplateHydrator.Populate("See {{garbage}} please", sample)` still returns the leftover.

### Blast radius
Act A (shipping garbage via Templates UI) is closed. Act B hurts buyers on a merchant who copied fulfillment tags into dunning/cancel — raw `{{document_link}}` in a pay-please email. Frequency: whenever someone treats the wiki as one bag of tags. Still **P2** for the leftover/hydrator half. HTML injection is 129 (resolved).

### Suggested fix
Do not undo 129’s Validate. Add the missing Update test. Either add `document_link` / `fulfillment_url` to `MessageTemplateContext` + Populate (empty unless the producer supplied them) so leftover tags disappear, **or** reject those tags on dunning step save. Do not strip all unknown tags in Populate without updating `Populate_UnknownTag_IsNotStripped`. Wave 5 / WhatsApp out of scope. No TypeSpec.

### Evaluation notes
**129 / B08-M06** already did Update validation as part of the HTML-encode commit. This ticket’s remaining work is hydrator coverage + dunning copy. **131 / B08-M08** is the digital-delivery fulfillment_url lie (different producer). **297 / B08-M27** is dual CMS (Payment Failed vs dunning steps). Still P2 for the unfixed half. Not blocked.

