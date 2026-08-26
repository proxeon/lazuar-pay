---
number: "294"
id: B08-M24
severity: P2
status: open
source: plans/009-bugs/08-communications-messaging-crm.md
head: "297ba98"
---

# 294 — B08-M24 — Test reminder always mails `admin@lazuars.io` via tenant BYOK

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/08-communications-messaging-crm.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B08-M24 — P2 — Test reminder always mails `admin@lazuars.io` via tenant BYOK

**Where:** `SendTestReminderCommandHandler.cs` 168–176; `TemplateEndpoints.cs` 114–120.

**What:** Tenant’s domain sends to Lazuar staff. Preview mocks (Ahmad / Founders Mastermind / portal.lazuar.com). WhatsApp test would console-log `+60123456789` if the flag were on.

---

## Evaluation (current tree, 2026-08-18)

### What the bug is
Ops “send test reminder” (`POST /admin/communications/reminders/test`) hydrates the chosen template with `MessageTemplateHydrator.PopulatePreview` and publishes a `DispatchMessageIntegrationEvent` whose `ToEmail` is hard-coded `admin@lazuars.io` and `ToPhone` is `+60123456789`. Dispatch then uses the **tenant** Resend BYOK (`GetEmailConfigCredentialsAsync`) so the merchant’s verified domain is the From, and Lazuar staff’s inbox is the To. That is a surprise send from a customer domain, not a sandbox. The API response even advertises `Sent_to = "admin@lazuars.io"`. Preview copy is still Ahmad / Founders Mastermind; links are now `localhost:3004` rather than `portal.lazuar.com`. WhatsApp remains gated off (`Messaging:WhatsAppEnabled` default false), so the phone is only a console log if someone flips the flag.

### Still present?
**STILL BROKEN**

```176:184:apps/lazuar-api/Modules/Communications/Application/Commands/MessageTemplateCommandHandlers.cs
        var dispatchEvent = new DispatchMessageIntegrationEvent(
            OrganizationId: request.OrganizationId,
            ToEmail: "admin@lazuars.io",
            ToPhone: "+60123456789",
            Subject: subject,
            HtmlEmailBody: emailBody,
            PlainTextPhoneBody: whatsappBody,
            Channel: request.Channel ?? template.Channel
        );
```

```114:120:apps/lazuar-api/Modules/Communications/Infrastructure/Endpoints/TemplateEndpoints.cs
        group.MapPost("/reminders/test", async Task<Ok<TestReminderResponse>> (
            TestReminderRequestDto req,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            await mediator.Send(new SendTestReminderCommand(ctx.TenantId, req.Template_name, req.Channel));
            return TypedResults.Ok(new TestReminderResponse { Success = true, Sent_to = "admin@lazuars.io" });
```

Preview mocks (`MessageTemplateHydrator.cs:50–63`) still use `Ahmad Firdaus` / `Founders Mastermind` / `+60123456789`. Renewal/portal URLs are `http://localhost:3004/acme/...` now, not `portal.lazuar.com` — a docs-only drift from the audit sentence, not a fix of the staff mailbox.

### Related files
- `apps/lazuar-api/Modules/Communications/Application/Commands/MessageTemplateCommandHandlers.cs` — `SendTestReminderCommandHandler` hard-codes To.
- `apps/lazuar-api/Modules/Communications/Infrastructure/Endpoints/TemplateEndpoints.cs` — HTTP surface and `Sent_to` echo.
- `apps/lazuar-api/Modules/Communications/Application/MessageTemplateHydrator.cs` — preview persona.
- `apps/lazuar-api/Modules/Messaging/Infrastructure/EventHandlers/DispatchMessageIntegrationEventHandler.cs` — tenant BYOK send path (112–137).
- `apps/lazuar-ops/src/modules/commerce/components/MessageTemplateEditor.tsx` / Templates page — UI that can trigger preview; test-send is the admin API above.

### Tests
- Existing: no `SendTestReminder` / `reminders/test` test under `apps/lazuar-api/tests/`. Hydrator tests cover `PopulatePreview` (`MessageTemplateHydratorTests.PopulatePreview_IncludesUpdatePaymentLink`, `PopulatePreview_CheckoutUrl_MatchesRenewalLink`) and do not assert destination.
- Would any test fail if the bug is still there? No.
- First regression: handler/endpoint must send to the signed-in operator (or a request `to` that is the operator’s email), never `admin@lazuars.io`, and the response `sent_to` must match that address.

### Reproduction today
Arrange a tenant with a live Resend BYOK row. Sign in as OrgAdmin. `POST /api/v1/admin/communications/reminders/test` with `{ "template_name": "Payment Failed" }`. Assert: JSON `sent_to` is `admin@lazuars.io`; Resend dashboard for the tenant domain shows a delivery to that mailbox; From is the tenant sender.

### Blast radius
Lazuar staff inbox + the merchant’s sending domain. Low frequency (manual test click). Not buyer PII. Ops confusion (“why is Lazuar in my Resend logs?”) and a deliverability foot-gun if staff mark it spam.

### Suggested fix
Send the test to `IExecutionContextAccessor` user’s email (or require `to` and allow-list it to the current user). Keep preview hydration. Do not send WhatsApp. Do not use a platform fallback key (tenant BYOK for From is fine if To is the operator). No TypeSpec regen required if `sent_to` already exists.

### Evaluation notes
Still P2. Not a duplicate of 019/293 (inbound webhook). Preview host string is slightly less wrong than the audit (`localhost:3004` vs `portal.lazuar.com`) — do not call that a product fix. WhatsApp half is frozen; do not “fix” it by enabling the flag.

