---
number: "170"
id: B10-X14
severity: P1
status: open
source: plans/009-bugs/10-tenancy-workers-contracts-tests.md
head: "297ba98"
---

# 170 — B10-X14 — JWT secret is the HMAC key for documents, unsubscribe, magic links, and (fallback) vault

- **Severity:** P1
- **Status:** open
- **Source:** `plans/009-bugs/10-tenancy-workers-contracts-tests.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B10-X14 — P1 — JWT secret is the HMAC key for documents, unsubscribe, magic links, and (fallback) vault

`DocumentLinkSigner.ResolveSecret` uses `Jwt:Secret` or `"secure_development_key_minimum_32_characters_long"`.

Same secret: unsubscribe query HMAC, broadcast unsubscribe URLs, magic-link tokens (`MagicLinkTokenService` fallback `"fallback_dev_secret_key"` — a **fourth** string), `AesSecretVault` if `Kms:MasterKey` empty, LHDN certificate vault same fallback.

`appsettings.json` has `"Jwt:Secret": ""`. Non-Production therefore signs JWTs and document links with the well-known 32-char dev string (`AuthAndCorsExtensions` 31). Production throws if empty or default. Staging-shaped environments that are not `IsProduction()` ship forgeable document URLs and unsubscribe tokens.

`DocumentLinkSigner.TryValidate` has **no** clock-skew window. A 1s skew past `exp` fails closed (good for security, bad for “link emailed at T-1s”).

