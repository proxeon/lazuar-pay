---
number: "303"
id: B09-U35
severity: P2
status: open
source: plans/009-bugs/09-frontends-ops-portal-admin.md
head: "297ba98"
---

# 303 — B09-U35 — WhatsApp Body * required on template create; dunning editor says not connected

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/09-frontends-ops-portal-admin.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

#### B09-U35 — WhatsApp Body * required on template create; dunning editor says not connected (P2)

`TemplatesPage.tsx` 249; `DunningStepEditor.tsx` 152–168; `MessageTemplateEditor.tsx` WhatsApp tab; `ProductForm.tsx` 227; `SubscribersPage.tsx` 470–472.

## Evaluation (current tree, 2026-08-18)

### What the bug is
Ops surfaces disagree about whether WhatsApp is a live channel. Create Template marks **WhatsApp Body \*** `required` and POSTs `channel: "ALL"` plus `required_variables: ["{{customer_name}}"]`, so an empty WhatsApp box fails HTML5 validation and, if bypassed, fails `CreateMessageTemplateCommandHandler.Validate` on the WhatsApp half. The template editor still has a first-class WhatsApp tab with no “not connected” banner. Dunning step editor is honest: action option “Send WhatsApp (not connected)” plus an amber “Email only until WhatsApp connected” panel. Product form still labels a phone checkbox “Require WhatsApp Number.” Subscriber drawer still offers a `wa.me` link named “WhatsApp.” Billing Settings correctly says WhatsApp is not connected. Wave 5 / Meta Cloud must not be implemented to “fix” this.

### Still present?
**STILL BROKEN**

Create modal (HTML required + ALL channel):

```51:55:apps/lazuar-ops/src/modules/commerce/pages/TemplatesPage.tsx
          whatsapp_body: newWhatsappBody,
          channel: "ALL",
          required_variables: ["{{customer_name}}"],
```

```249:250:apps/lazuar-ops/src/modules/commerce/pages/TemplatesPage.tsx
                  <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">WhatsApp Body *</label>
                  <textarea required value={newWhatsappBody} …
```

Backend agrees for channel ALL (`MessageTemplateCommandHandlers.cs:55–63`). Dunning is the honest screen:

```151:168:apps/lazuar-ops/src/modules/commerce/components/dunning/DunningStepEditor.tsx
                  <option value="EMAIL">Send Email</option>
                  <option value="WHATSAPP">Send WhatsApp (not connected)</option>
                  …
                  <p className="font-bold">Email only until WhatsApp connected</p>
```

Other liars:
- `MessageTemplateEditor.tsx:134–139` — “WhatsApp Version” tab, no freeze copy.
- `ProductForm.tsx:226–228` — “Require WhatsApp Number” (collects phone only).
- `SubscribersPage.tsx:512–513` — `wa.me` labeled “WhatsApp” (audit line numbers moved; was 470–472, now pagination).

`Messaging:WhatsAppEnabled` still defaults false; dispatch skips WhatsApp (`DispatchMessageIntegrationEventHandler.cs:60–76`).

### Related files
- `apps/lazuar-ops/src/modules/commerce/pages/TemplatesPage.tsx` — required WhatsApp on create.
- `apps/lazuar-ops/src/modules/commerce/components/MessageTemplateEditor.tsx` — WhatsApp tab.
- `apps/lazuar-ops/src/modules/commerce/components/dunning/DunningStepEditor.tsx` — honest copy to copy from.
- `apps/lazuar-ops/src/modules/commerce/components/ProductForm.tsx` — phone labeled WhatsApp.
- `apps/lazuar-ops/src/modules/commerce/pages/SubscribersPage.tsx` — `wa.me` affordance.
- `apps/lazuar-ops/src/modules/workspace/pages/BillingSettingsPage.tsx:149` — “WhatsApp is not connected and is not billed.”
- `apps/lazuar-api/Modules/Communications/Application/Commands/MessageTemplateCommandHandlers.cs` — ALL-channel WhatsApp variable check.

### Tests
- Existing: no ops component tests. API: `DefaultMessageTemplatesTests` seeds ALL-channel catalog with WhatsApp bodies. `MessagingEndpointsAuthorizationTests` / console stub tests lock the freeze, not the ops labels.
- Would any test fail if the bug is still there? No.
- First regression: create-template form must not `required` WhatsApp; channel EMAIL (or ALL with optional WA) must succeed with empty `whatsapp_body`. Optional: ProductForm label is “Phone” / “Buyer phone.”

### Reproduction today
Ops → Notification Templates → Create Template. Leave WhatsApp Body empty, fill title/subject/email. Assert: browser blocks submit (`required`). Fill dummy WhatsApp text; save. Open Dunning Campaigns → add/edit a step. Assert: “Send WhatsApp (not connected)” and the amber email-only banner. Open a product: checkbox “Require WhatsApp Number.” Open a subscriber with a phone: green “WhatsApp” `wa.me` link.

### Blast radius
Merchant UX / honesty. Blocks creating an email-only custom template. Does not send Meta traffic (flag off). `wa.me` is a deep-link, not Communications. Frequency: every template create. Do not treat this as a reason to ship WhatsApp.

### Suggested fix
Create modal: drop `required` on WhatsApp, default `channel` to `"EMAIL"`, or keep ALL but make WA optional and skip `CheckVariables` when WA body is blank. Copy Dunning’s “not connected” sentence onto the MessageTemplateEditor WhatsApp tab. Rename ProductForm checkbox to “Require phone number.” Leave `wa.me` or relabel “Open in WhatsApp (personal)” so it is not the product channel. **Do not** set `Messaging:WhatsAppEnabled`, do not add Meta Cloud, do not regen TypeSpec for a new channel.

### Evaluation notes
Still P2. Same family as U20 (legal/landing still sell WhatsApp). Audit cited `SubscribersPage.tsx` 470–472; those lines are now Prev/Next — the `wa.me` control is 512–513. Not blocked. Wrap-rail: no Wave 5 / WhatsApp implementation.
