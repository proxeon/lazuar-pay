---
number: "290"
id: B08-M20
severity: P2
status: open
source: plans/009-bugs/08-communications-messaging-crm.md
head: "297ba98"
---

# 290 — B08-M20 — `FixedTimeEquals` on hex/base64 of unequal length throws 500

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/08-communications-messaging-crm.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B08-M20 — P2 — `FixedTimeEquals` on hex/base64 of unequal length throws 500

**Where:** unsubscribe 51–52; webhook 131–133.

**What:** `CryptographicOperations.FixedTimeEquals` requires equal lengths. A 1-character `sig` or a truncated `v1=` is an unhandled exception, not `400 Invalid unsubscribe link`.

---

## Evaluation (current tree, 2026-08-18)

### What the bug is
`CryptographicOperations.FixedTimeEquals` throws if the two spans differ in length. Unsubscribe GET/POST compare hex HMAC (`expected` is 64 ASCII hex chars) to `sig` as ASCII bytes with **no** length check. A 1-character `sig` (or any non-64-char string) is an unhandled exception → global 500, not `400 Invalid unsubscribe link`. The Resend/Svix path was the other cited site (`webhook 131–133` in the audit). **019** moved Svix compare into `SvixWebhookSignature.IsValid`, which now checks `expectedBytes.Length == receivedBytes.Length` before `FixedTimeEquals`. **132** made empty `Jwt:Secret` fail closed (503) instead of HMAC-with-empty. Unsubscribe length mismatch is still live.

### Still present?
**PARTIAL**

Unsubscribe still unguarded (GET and POST):

```53:58:apps/lazuar-api/Modules/Communications/Infrastructure/Endpoints/PublicComplianceEndpoints.cs
            var expected = ComputeSig(secret, $"{orgId}:{email}");
            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(sig.ToLowerInvariant())))
            {
                return Results.BadRequest("Invalid unsubscribe link.");
            }
```

(same at lines 84–88). Webhook half is length-safe:

```44:47:apps/lazuar-api/Modules/Communications/Infrastructure/Security/SvixWebhookSignature.cs
        var expectedBytes = Encoding.ASCII.GetBytes(expected);
        var receivedBytes = Encoding.ASCII.GetBytes(received);
        return expectedBytes.Length == receivedBytes.Length
            && CryptographicOperations.FixedTimeEquals(expectedBytes, receivedBytes);
```

`TryJwtHmacSecret` fail-closed (`PublicComplianceEndpoints.cs:178–182`) is **132**. Svix decode/verify is **019** / `fix/019-resend-svix-whsec-hmac` / `fc504194`. No test sends `sig=x`.

### Related files
- `apps/lazuar-api/Modules/Communications/Infrastructure/Endpoints/PublicComplianceEndpoints.cs` — GET/POST unsubscribe compare.
- `apps/lazuar-api/Modules/Communications/Infrastructure/Security/SvixWebhookSignature.cs` — copy this length guard.
- `apps/lazuar-api/Modules/One/Infrastructure/Workers/OutboundWebhookSignature.cs` / `BillplzGatewayAdapter.FixedTimeEqualsHex` — existing `left.Length == right.Length && FixedTimeEquals` pattern in-repo.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Communications/UnsubscribeJwtSecretTests.cs` — empty secret only.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Communications/SvixWebhookSignatureTests.cs` — valid/wrong signature, not short `v1=`.

### Tests
- Existing: `UnsubscribeJwtSecretTests.Empty_Or_Missing_Secret_Fails_Closed`, `Configured_Secret_Is_Usable`; `SvixWebhookSignatureTests.IsValid_AcceptsSignatureForSampleWhsecSecret`, `IsValid_RejectsUtf8OfWholeSecret`.
- None fail on a 1-char `sig`. Svix tests would fail if `IsValid` started throwing on length mismatch (they use full-length wrong base64). Unsubscribe has no HTTP test at all.
- First remaining regression: `GET /public/communications/unsubscribe?org=<guid>&email=a@b.com&sig=x` → 400 `"Invalid unsubscribe link."` and no 500. Same for POST and for a truncated `v1=` header on `/public/communications/webhooks/resend` (already false, lock it). Compare hex of equal length that is wrong → still 400.

### Reproduction today
Arrange: any configured `Jwt:Secret`. Act: `GET /api/v1/public/communications/unsubscribe?org=00000000-0000-0000-0000-000000000001&email=buyer@example.com&sig=x`. Assert: today an unhandled `ArgumentException` from `FixedTimeEquals` (500 via `GlobalExceptionHandler`). A correct-length wrong hex returns 400. Empty secret returns 503 (132).

### Blast radius
Anyone who clicks a broken/truncated List-Unsubscribe URL or tampers `sig`. Marketing unsub only (receipts survive). 500s in logs/APM; attacker can probe for the throw vs 400. No PII dump in the 400 path. Frequency: every malformed unsub link (mail clients sometimes truncate query strings). Remaining half is still **P2**. Webhook 500 is fixed.

### Suggested fix
Copy `FixedTimeEqualsHex` (length check then `FixedTimeEquals`) used by Billplz/outbound webhooks. Return 400 on mismatch. Apply to both GET and POST unsubscribe. Do not change HMAC algorithm or URL shape. Do not reopen 019/132 unless those regress. No TypeSpec (public unsub is honesty-allowlisted).

### Evaluation notes
**019 / B08-M01** fixed Svix `whsec_` + the webhook length throw. **132 / B08-M09** fixed empty JWT. This ticket’s unsubscribe 500 remains. **056** is magic-link compare (Commerce), different file. Still P2 for the open half. Not blocked.

