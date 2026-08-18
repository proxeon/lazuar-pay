---
number: "261"
id: B07-I17
severity: P2
status: open
source: plans/009-bugs/07-one-identity-invites-keys.md
head: "297ba98"
---

# 261 — B07-I17 — Reset-password is an email oracle

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/07-one-identity-invites-keys.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B07-I17 — P2 — Reset-password is an email oracle

**Where.** `ResetPasswordCommand.cs:25–33`. Missing user: `"Invalid request."` Bad token on a real user: `"Token is invalid or expired."`

**What.** Forgot is silent. Reset is not. Pair with B07-I02 (the link 404s) and you have an API that enumerates emails and a product that cannot complete the flow.

## Evaluation (current tree, 2026-08-18)

### What the bug is
`POST /one/auth/forgot-password` is intentionally silent when the email is missing or inactive. `POST /one/auth/reset-password` is not: unknown/inactive email throws `"Invalid request."`; a real user with a bad/expired/mismatched token throws `"Token is invalid or expired."` Both become HTTP 400 with `detail` = that string (`InvalidOperationException` → GlobalExceptionHandler). An unauthenticated caller who posts `{ email, token: "x", new_password: "…" }` can tell whether the address is a live GlobalUser. At audit time the mail link also 404’d (B07-I02), so the product could not finish reset **and** the API enumerated emails.

### Still present?
**STILL BROKEN**

Handler messages are unchanged:

```25:33:apps/lazuar-api/Modules/One/Application/Commands/ResetPasswordCommand.cs
        var user = await _repository.GetUserByEmailAsync(request.Email, ct);
        if (user == null || !user.IsActive) throw new InvalidOperationException("Invalid request.");

        if (string.IsNullOrEmpty(user.PasswordResetTokenHash) || user.PasswordResetExpiresAt < DateTime.UtcNow)
            throw new InvalidOperationException("Token is invalid or expired.");

        var inputHash = _tokenGenerator.HashToken(request.Token);
        if (user.PasswordResetTokenHash != inputHash)
            throw new InvalidOperationException("Token is invalid or expired.");
```

Forgot remains silent (`ForgotPasswordCommand.cs:24`). The 404 half is **gone**: **112** (`fix/112-reset-verify-404`) routed reset mail to ops; `NotificationDispatchDomainEventHandlers.cs:31` builds `/reset-password?email=…&token=…` on `GetOpsBaseUrl()`; `ResetPasswordPage.tsx` POSTs those query params and **renders `apiError.detail`**. The oracle is now reachable from a browser, not only curl. I found **no** `ResetPasswordCommand` tests under `apps/lazuar-api/tests/`.

### Related files
- `apps/lazuar-api/Modules/One/Application/Commands/ResetPasswordCommand.cs` — the oracle.
- `apps/lazuar-api/Modules/One/Application/Commands/ForgotPasswordCommand.cs` — silent counterpart.
- `apps/lazuar-api/Modules/One/Infrastructure/Endpoints/AuthEndpoints.cs:125–146` — forgot vs reset routes; reset is unauthenticated and **not** on `PublicAuthRateLimiter`.
- `apps/lazuar-api/Modules/One/Application/EventHandlers/NotificationDispatchDomainEventHandlers.cs:31` — mail includes email+token.
- `apps/lazuar-ops/src/pages/ResetPasswordPage.tsx` — surfaces `detail`.
- `apps/lazuar-ops/src/pages/ForgotPasswordPage.tsx` — honest “If that email exists…” copy.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/One/OneLinkServiceTests.cs` — `ResetAndVerifyEmails_UseOpsUrl_NotClientUrl` (URL host only).
- `issues/112-p1-b07-i02-password-reset-and-verify-email-links-still-404.md` — 404 closed; makes this worse.

### Tests
- Existing: `ResetAndVerifyEmails_UseOpsUrl_NotClientUrl`; `InvalidOperation_Is_400_With_Domain_Message` (generic handler). No `ResetPassword*` / `ForgotPassword*` handler tests.
- Nothing would fail if the two reset strings stayed different. `OneLinkServiceTests` would still pass.
- First regression: `ResetPassword` with garbage token returns the **same** status+detail for (a) unknown email, (b) known email with no reset hash, (c) known email with wrong hash. Prefer one string, e.g. `"Token is invalid or expired."`, and do not branch timing in a way that re-opens the oracle. Optionally put reset on `PublicAuthRateLimiter` like forgot (**121**).

### Reproduction today
Arrange: one real user `ada@example.com`, one unused address. Act: `POST /api/v1/one/auth/reset-password` `{ "email": "nobody@example.com", "token": "x", "new_password": "n3w-n3w-n3w" }` → 400 `Invalid request.` Repeat with `ada@example.com` → 400 `Token is invalid or expired.` Same split appears on `/reset-password?email=…&token=x` after submit. Forgot both addresses: always 200 `requested` and the same UI sentence.

### Blast radius
Account enumeration of merchant emails (staff GlobalUsers, not buyer CRM). No password change without the token. Reset is unthrottled (login/forgot now are, after **121**). Frequency: any unauthenticated client; now also anyone who opens a reset URL and edits the email query param. PII-adjacent, not money.

### Suggested fix
Use a single InvalidOperation message for every failed reset (missing user, inactive, missing/expired/mismatch token). Do not add a “user exists” code. Keep forgot silent. Add the handler test above. Optional: `PublicAuthRateLimiter` on reset using `ResolveRegisterClientKey`. No TypeSpec regen.

### Evaluation notes
Still P2 as an oracle; **higher value than at audit** because **112** made the flow completable and the SPA prints `detail`. Pair with **121** (login/forgot limited; reset not). Not 161–200 fail-closed. Do not “fix” by hiding the reset page.

