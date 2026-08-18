---
number: "297"
id: B08-M27
severity: P2
status: open
source: plans/009-bugs/08-communications-messaging-crm.md
head: "297ba98"
---

# 297 — B08-M27 — Dual CMS and leftover `reminder.due`

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/08-communications-messaging-crm.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B08-M27 — P2 — Dual CMS and leftover `reminder.due`

**Where:** `DunningStepDispatcher` sends step copy, not catalog “Payment Failed”; hydrate still implements `reminder.due` + `template_id` (FulfillmentRequested 51, 161–172); no job publishes it.

**What:** Editing Templates → Payment Failed does not change day-0 dunning. 008 called this Chargebee-shaped debt. Still true. Not a functional defect unless someone sells “one template.” `reminder.due` is dead code path.

---

## Evaluation (current tree, 2026-08-18)

### What the bug is
There are two content systems for “your payment failed / is due.” Day-0 (and other) dunning mail is the **campaign step** subject/body that `DunningStepDispatcher` publishes as `reminder.dunning`. The catalog template named “Payment Failed” is a different row, edited under Notification Templates, and is consumed by the **immediate** `GatewayPaymentFailed` mail handler — not by the dunning job. A merchant who edits Templates → Payment Failed will not change Standard Recovery Strategy day-0 copy (`{{plan_name}} is due — pay this cycle`). Separately, `FulfillmentRequestedIntegrationEventHandler` still implements `reminder.due` + `template_id` lookup. No production job publishes `reminder.due`. That branch is dead Chargebee-shaped debt. Plans already say “do not revive it.”

### Still present?
**STILL BROKEN**

Live dunning is inline step copy + `reminder.dunning`:

```85:108:apps/lazuar-api/Modules/Commerce/Infrastructure/Dunning/DunningStepDispatcher.cs
        var payloadObj = new
        {
            …
            subject = step.Subject,
            email_body = step.EmailBody,
            whatsapp_body = effectiveActionType == "EMAIL" ? string.Empty : step.WhatsAppBody,
            …
        };
        await eventBus.PublishAsync(new FulfillmentRequestedIntegrationEvent(
            sub.OrganizationId, "COMMUNICATIONS", "reminder.dunning", payloadElement));
```

Default day-0 is campaign copy, not the catalog:

```150:153:apps/lazuar-api/Modules/Commerce/Application/Commands/DunningCampaignCommandHandlers.cs
        campaign.AddStep(0, "EMAIL",
            "{{plan_name}} is due — pay this cycle",
            "{{plan_name}} is due today ({{amount}} {{currency}}). [Pay now]({{renewal_link}})",
            null);
```

Hydrator still accepts the unpublished type and the template-id else-branch:

```50:53:apps/lazuar-api/Modules/Communications/Infrastructure/EventHandlers/FulfillmentRequestedIntegrationEventHandler.cs
        if (@event.InternalTargetApp != "COMMUNICATIONS"
            || (@event.EventType != "reminder.due"
                && @event.EventType != "reminder.dunning"
                && @event.EventType != "invoice.reminder"))
```

```161:171:apps/lazuar-api/Modules/Communications/Infrastructure/EventHandlers/FulfillmentRequestedIntegrationEventHandler.cs
        else
        {
            if (!root.TryGetProperty("template_id", out var templateIdProp) || !Guid.TryParse(templateIdProp.GetString(), out var templateId)) return;
            var template = await _repository.GetTemplateByIdAsync(@event.OrganizationId, templateId);
            …
        }
```

Grep of `*.cs` tests: no `EventType: "reminder.due"` publisher. All dunning hydrate tests use `reminder.dunning`.

### Related files
- `apps/lazuar-api/Modules/Commerce/Infrastructure/Dunning/DunningStepDispatcher.cs` — live producer.
- `apps/lazuar-api/Modules/Commerce/Application/Commands/DunningCampaignCommandHandlers.cs` — default campaign seed.
- `apps/lazuar-api/Modules/Communications/Infrastructure/EventHandlers/FulfillmentRequestedIntegrationEventHandler.cs` — dual event types + dead `template_id` branch.
- `apps/lazuar-api/Modules/Communications/Domain/DefaultMessageTemplates.cs` — “Payment Failed” catalog (immediate-fail mail).
- `apps/lazuar-api/Modules/Communications/Infrastructure/EventHandlers/GatewayPaymentFailedIntegrationEventHandler.cs` — the handler that **does** use “Payment Failed”.
- `apps/lazuar-ops/src/modules/commerce/pages/TemplatesPage.tsx` — merchant-facing catalog editor.
- `apps/lazuar-ops/src/modules/commerce/components/dunning/DunningStepEditor.tsx` — the copy that actually sends on dunning days.

### Tests
- Existing: `DunningTemplateVariableSubstitutionTests.*` (all `reminder.dunning`). `DunningEngineJobTests` asserts `e.EventType == "reminder.dunning"`. `DefaultMessageTemplatesTests` seeds “Payment Failed”. `GatewayPaymentFailedEmailHandlerTests` uses the catalog name. `DunningCampaignCommandHandlerTests` locks default day-0 body (`{{renewal_link}}`, not Payment Failed subject).
- Would any test fail if the dual CMS is still there? No. Tests lock the split.
- First regression if you delete `reminder.due`: existing dunning tests must still pass; add a grep/test that no job publishes `reminder.due`. If you add ops honesty copy, a UI test is optional.

### Reproduction today
Arrange a tenant with the default campaign. Edit Notification Templates → Payment Failed subject to “CHANGED CATALOG”. Trigger day-0 dunning on a PAST_DUE sub. Assert: mail subject is still `{{plan_name}} is due — pay this cycle` (hydrated), not “CHANGED CATALOG”. Trigger an immediate vault decline: that mail **does** use Payment Failed. Grep the repo: nothing publishes `"reminder.due"`.

### Blast radius
Merchant confusion, not money. Day-0 recovery copy and the catalog diverge. Frequency: every merchant who treats Templates as the single CMS. `reminder.due` is latent — reviving it would double-send if someone also left campaign steps in place.

### Suggested fix
Do not publish `reminder.due`. Smallest honesty: Templates page description that “Payment Failed” is immediate-decline mail; dunning copy lives under Dunning Campaigns. Optional cleanup: delete the `reminder.due` / `template_id` else-branch after a grep that no publisher exists. Do not wire day-0 to the catalog (that would be a product merge, not a one-line fix). No TypeSpec, no WhatsApp.

### Evaluation notes
Still P2. Honesty / CMS-shape more than a runtime fail. 008 called this Chargebee debt; still true. Not blocked. Immediate-fail amount emptiness is 285 (M15), not this ticket.

