---
number: "293"
id: B08-M23
severity: P2
status: open
source: plans/009-bugs/08-communications-messaging-crm.md
head: "297ba98"
---

# 293 — B08-M23 — Parser misses string `to`; webhook 200 on suppress failure

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/08-communications-messaging-crm.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B08-M23 — P2 — Parser misses string `to`; webhook 200 on suppress failure

**Where:** `ResendWebhookParser.ReadRecipient` 47–66; endpoint 160–165.

**What:** If Resend ever sends `"to": "user@example.com"` instead of an array, recipient is null, event acknowledged, no suppress. DB exceptions inside the try are acknowledged too.

---

## Evaluation (current tree, 2026-08-18)

### What the bug is
The Resend bounce/complaint webhook (`POST /public/communications/webhooks/resend`) must turn `email.bounced` / `email.complained` into a suppression row. `ResendWebhookParser.ReadRecipient` only accepts `data.to` as a JSON array, or `data.email.to` as an array, or `data.recipient` as a string. If Resend ever posts `"to": "user@example.com"` (a string), recipient is null. The endpoint then 200s without writing BOUNCE/COMPLAINT. The same handler wraps parse+suppress in try/catch and still returns 200 when `SuppressAsync` throws, so Resend will not retry a transient DB failure. Issue 019 fixed Svix HMAC key decoding; this ticket is the remaining parse shape and ack-on-failure behavior.

### Still present?
**STILL BROKEN**

Recipient parse is still array-or-`recipient` only:

```45:66:apps/lazuar-api/Modules/Communications/Infrastructure/ResendWebhookParser.cs
    private static string? ReadRecipient(JsonElement data)
    {
        if (data.TryGetProperty("to", out var toEl) && toEl.ValueKind == JsonValueKind.Array && toEl.GetArrayLength() > 0)
        {
            return toEl[0].GetString();
        }
        // … nested data.email.to array, then data.recipient string …
        return null;
    }
```

There is no `JsonValueKind.String` branch on `to`. `TryParseSuppression` still returns `true` after a successful JSON parse even when recipient and org are null (`ResendWebhookParser.cs:12–30`). The endpoint then acknowledges:

```142:169:apps/lazuar-api/Modules/Communications/Infrastructure/Endpoints/PublicComplianceEndpoints.cs
            try
            {
                if (!ResendWebhookParser.TryParseSuppression(rawBody, out var type, out var recipient, out var orgId))
                {
                    logger.LogWarning("Failed to parse Resend webhook payload.");
                    return Results.Ok();
                }

                var reason = ResendWebhookParser.MapReason(type);
                if (reason == null) return Results.Ok();
                if (string.IsNullOrWhiteSpace(recipient)) return Results.Ok();
                // … SuppressAsync only when org tag present …
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to parse Resend webhook payload.");
            }

            return Results.Ok();
```

HMAC verify is now `SvixWebhookSignature.IsValid` (019 closed). That does not fix string-`to` or 200-on-exception.

### Related files
- `apps/lazuar-api/Modules/Communications/Infrastructure/ResendWebhookParser.cs` — recipient extraction.
- `apps/lazuar-api/Modules/Communications/Infrastructure/Endpoints/PublicComplianceEndpoints.cs` — always-200 after the verify gate.
- `apps/lazuar-api/Modules/Communications/Infrastructure/Services/SuppressionService.cs` — `SuppressAsync` that can throw after parse.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Communications/ResendWebhookParserTests.cs` — array `to` and nested `email.to` only.
- `issues/019-p0-b08-m01-resend-bounce-complaint-webhook-never-verifies-a-real-whsec-secr.md` — HMAC sibling, already resolved.

### Tests
- Existing: `ResendWebhookParserTests.Parses_Data_To_And_Object_Tags`, `Parses_Array_Tags`, `Missing_Org_Tag_Leaves_Org_Null`. No test feeds `"to": "user@example.com"`. No endpoint test that throws inside `SuppressAsync` and asserts status.
- Would any test fail if the bug is still there? No. The parser tests only lock the array shapes.
- First regression: `TryParseSuppression` with `"to":"user@example.com"` must return that address. Second: when `SuppressAsync` throws, the webhook must not 200 (503/500) so Resend retries.

### Reproduction today
Arrange a signed (or Development unsigned) POST to `/api/v1/public/communications/webhooks/resend` with `{"type":"email.bounced","data":{"to":"user@example.com","tags":{"org":"<tenant-guid>"}}}`. Act: handle. Assert: no `SuppressionEntries` row, HTTP 200. Arrange `SuppressAsync` to throw (DB down). Act: valid array-`to` bounce. Assert: still 200, no retry.

### Blast radius
Hard-bounced / complained mailboxes stay on the transactional lane (receipts, dunning). Tenant Resend domain reputation burns. Frequency is “whenever Resend’s payload uses a string `to`” plus “any transient suppress write failure.” PII is not leaked; deliverability and complaint compliance are.

### Suggested fix
In `ReadRecipient`, if `to` is a string, use it; if it is an array, keep `[0]`. After a recognized bounce/complaint with an org tag, do not swallow `SuppressAsync` — return 5xx so Svix retries. Leave non-suppression event types as 200. Do not regen TypeSpec. Do not touch WhatsApp.

### Evaluation notes
Still P2 on its own (019 was the P0 verify-key bug). Sibling of 127 (first-reason-wins unsub vs bounce) and 019 (HMAC). Not blocked. Parser 200-on-unparseable-JSON (`TryParseSuppression` false → `Results.Ok()`) is the same ack-too-early family.

