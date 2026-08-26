---
number: "056"
id: B03-C11
severity: P1
status: resolved
source: plans/009-bugs/03-commerce-dunning-arrears-portal.md
head: "297ba98"
resolved_branch: fix/056-magic-token-constant-time
---

# 056 — B03-C11 — HMAC compare is not constant-time; missing `Jwt:Secret` is a shared mint key

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/03-commerce-dunning-arrears-portal.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/056-magic-token-constant-time`

Missing `Jwt:Secret` fails closed. HMAC compare uses `FixedTimeEquals`. Expired tokens return null.

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B03-C11 — P1 — HMAC compare is not constant-time; missing `Jwt:Secret` is a shared mint key

**Evidence.** `parts[2] != expectedHash` (`MagicLinkTokenService.cs` 48). Constructor: `_secret = configuration["Jwt:Secret"] ?? "fallback_dev_secret_key"`. Test **requires** the fallback to validate across two service instances.

If production ever boots without `Jwt:Secret`, anyone who knows the string and a subscription GUID (emails, webhooks, ops screens, v7 time leak) can mint a 24h portal token and pass `ArrearsAccess`.

Timing leak on a 64-char hex compare is the smaller half; still real against a hot validate endpoint.

**Tests.** `GenerateToken_UsesFallbackSecret_WhenJwtSecretMissing` would go **red** if the fallback were removed. That test is a landmine.

**Fix direction.** Fail closed without `Jwt:Secret`. `CryptographicOperations.FixedTimeEquals` on the hex UTF-8 bytes (or compare raw MAC). Switch to Base64url while versioning tokens. Add `ValidateToken_Expired_ReturnsNull`.

---

