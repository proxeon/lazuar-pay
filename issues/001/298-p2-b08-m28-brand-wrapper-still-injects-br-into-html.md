---
number: "298"
id: B08-M28
severity: P2
status: open
source: plans/009-bugs/08-communications-messaging-crm.md
head: "297ba98"
---

# 298 — B08-M28 — Brand wrapper still injects `<br/>` into HTML

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/08-communications-messaging-crm.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B08-M28 — P2 — Brand wrapper still injects `<br/>` into HTML

**Where:** `EmailTemplateBuilder.cs` 16; tests assert it.

**What:** Markdown already emitted `<p>`. Extra `<br/>` is ugly, not a security issue. Locked in by tests — a future cleanup will fail them on purpose.

---

## Evaluation (current tree, 2026-08-18)

### What the bug is
Communications hydrates markdown then `MarkdownParser.ToHtml`, which already emits block `<p>` tags. Messaging’s brand shell then runs `rawBody.Replace("\n", "<br/>")` on that HTML before wrapping “Powered by Lazuar.” Newlines that survive between tags become extra `<br/>` inside or between paragraphs. The result is ugly spacing in every tenant email that goes through `DispatchMessageIntegrationEventHandler`. It is not XSS (that is B08-M06 / issue 129). Tests **assert** the `<br/>`, so a cleanup that only removes the replace will fail CI until those tests change.

### Still present?
**STILL BROKEN**

```16:16:apps/lazuar-api/Modules/Messaging/Infrastructure/Email/EmailTemplateBuilder.cs
        var formattedBody = rawBody.Replace("\n", "<br/>");
```

Called from dispatch on the already-HTML body:

```129:133:apps/lazuar-api/Modules/Messaging/Infrastructure/EventHandlers/DispatchMessageIntegrationEventHandler.cs
                var htmlPayload = EmailTemplateBuilder.WrapWithBrandHtml(@event.HtmlEmailBody!, @event.UnsubscribeUrl);
                var providerId = await _emailService.SendEmailAsync(
                    @event.ToEmail,
                    @event.Subject,
                    htmlPayload,
```

Locked in:

```18:22:apps/lazuar-api/tests/Lazuar.ModuleTests/Messaging/EmailTemplateBuilderTests.cs
    public void WrapWithBrandHtml_IncludesBodyAndBrandFooter()
    {
        var html = EmailTemplateBuilder.WrapWithBrandHtml("Hello\nWorld");
        html.Should().Contain("Hello<br/>World");
```

```129:133:apps/lazuar-api/tests/Lazuar.ModuleTests/Messaging/DispatchMessageIntegrationEventHandlerTests.cs
            Arg.Is<string>(html =>
                html.Contains("Line1<br/>Line2")
                && html.Contains("Powered by")
```

### Related files
- `apps/lazuar-api/Modules/Messaging/Infrastructure/Email/EmailTemplateBuilder.cs` — the replace.
- `apps/lazuar-api/Modules/Messaging/Infrastructure/EventHandlers/DispatchMessageIntegrationEventHandler.cs` — applies the wrapper to HTML.
- `apps/lazuar-api/BuildingBlocks/Application/MarkdownParser.cs` — already emits HTML (soft-break-as-hard-break pipeline).
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Messaging/EmailTemplateBuilderTests.cs`
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Messaging/DispatchMessageIntegrationEventHandlerTests.cs`

### Tests
- Existing: `EmailTemplateBuilderTests.WrapWithBrandHtml_IncludesBodyAndBrandFooter`, `WrapWithBrandHtml_Empty_ReturnsEmpty`, `WrapWithBrandHtml_WithUnsubscribe_AddsFooterLink`. `DispatchMessageIntegrationEventHandlerTests.HandleAsync_EmailChannel_WrapsBrandAndSendsViaIEmailService`.
- Would any test fail if the bug is still there? No — they fail only if you **remove** the `<br/>`.
- First regression after a real fix: wrap already-HTML (`<p>Hi</p>\n<p>There</p>`) must not insert `<br/>` between/inside tags; a plain-text `Hello\nWorld` path may still become `<br/>` if you keep a text-only helper.

### Reproduction today
Arrange dispatch of `HtmlEmailBody` = markdown-parsed “Hi {{name}}” (contains `<p>…</p>\n`). Act: send via `WrapWithBrandHtml`. Assert: payload contains `<br/>` adjacent to `<p>`. Preview in an email client shows extra blank lines.

### Blast radius
Every transactional email (dunning, cancel, receipts, test reminder). Cosmetic only. High frequency, zero money/PII.

### Suggested fix
If `rawBody` contains `<` / looks like HTML, do not newline-replace; wrap as-is. Keep `\n` → `<br/>` only for true plain text. Update the two tests in the same change so CI does not lock the ugly path. Do not touch Markdig / XSS here (separate ticket).

### Evaluation notes
Still P2. Tests are lying-as-spec: they document the defect. Cleanup must edit tests on purpose. Not a security issue. Not blocked.

