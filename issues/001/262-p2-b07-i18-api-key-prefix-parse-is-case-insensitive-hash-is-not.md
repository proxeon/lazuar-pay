---
number: "262"
id: B07-I18
severity: P2
status: open
source: plans/009-bugs/07-one-identity-invites-keys.md
head: "297ba98"
---

# 262 — B07-I18 — API key prefix parse is case-insensitive; hash is not

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/07-one-identity-invites-keys.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B07-I18 — P2 — API key prefix parse is case-insensitive; hash is not

**Where.** `ApiKeyAuthenticationMiddleware.cs:35, 158–162` vs `TokenGeneratorService.cs:23–27`.

**What.** `SK_TEST_…` is recognized as a key and as test mode, then 401s. Confusing, not a bypass.

## Evaluation (current tree, 2026-08-18)

### What the bug is
Minted keys are `sk_test_` / `sk_live_` plus a base64url secret; the DB stores `SHA256(UTF8(exact plaintext))`. Auth accepts a request as “this is an API key” when `Authorization` starts with `sk_live_` or `sk_test_` **case-insensitively**, and sets `IsTestMode` the same way. The hash is the exact bytes. `Authorization: Bearer SK_TEST_…` (or a user who uppercased a pasted key) therefore enters the API-key pipeline, is marked test, misses `one.ApiCredentials`, and returns 401 “Invalid or revoked API Key” instead of falling through to JWT. It is not a bypass: the uppercased string cannot match a stored hash.

### Still present?
**STILL BROKEN**

Parse + test-mode still ignore case:

```33:36:apps/lazuar-api/src/Lazuar.Api/Middleware/ApiKeyAuthenticationMiddleware.cs
        if (TryGetApiKey(context.Request, out var token))
        {
            var isTestMode = token.StartsWith("sk_test_", StringComparison.OrdinalIgnoreCase);
            var keyHash = _tokenGenerator.HashToken(token);
```

```161:166:apps/lazuar-api/src/Lazuar.Api/Middleware/ApiKeyAuthenticationMiddleware.cs
        if (value.StartsWith("sk_live_", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("sk_test_", StringComparison.OrdinalIgnoreCase))
        {
            apiKey = value;
            return true;
        }
```

Hash is still exact UTF-8 SHA-256, hex lowercased **after** hash (does not normalize the input):

```23:28:apps/lazuar-api/BuildingBlocks/Infrastructure/TokenGeneratorService.cs
    public string HashToken(string plainToken)
    {
        var bytes = Encoding.UTF8.GetBytes(plainToken);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
```

Mint still concatenates lowercase prefix + random (`GenerateApiCredentialCommand.cs:49–52`). Tests only send lowercase `sk_test_` / `sk_live_` (`GenerateAndListApiCredentialsTests.TryGetApiKey_Accepts_Bearer_And_Raw_Prefix`, `TryGetApiKey_Rejects_NonSk_Prefix`).

### Related files
- `apps/lazuar-api/src/Lazuar.Api/Middleware/ApiKeyAuthenticationMiddleware.cs` — parse + test flag + 401 on miss.
- `apps/lazuar-api/BuildingBlocks/Infrastructure/TokenGeneratorService.cs` — case-sensitive hash.
- `apps/lazuar-api/Modules/One/Application/Commands/GenerateApiCredentialCommand.cs` — canonical `sk_test_` / `sk_live_`.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/One/GenerateAndListApiCredentialsTests.cs` — prefix accept/reject.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/One/ApiKeyAuthenticationTests.cs` — happy-path lowercase keys.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Lhdn/GenerateAndListApiKeysTests.cs` — duplicate TryGetApiKey cases.

### Tests
- Existing: `TryGetApiKey_Accepts_Bearer_And_Raw_Prefix`, `TryGetApiKey_Rejects_NonSk_Prefix`; middleware tests with `sk_test_validkey…`.
- None would fail on `SK_TEST_…` today: parse would return true, hash would miss, 401. That is the bug, and it is unasserted.
- First regression: `TryGetApiKey` is **false** for `Bearer SK_TEST_abc` and `SK_LIVE_abc` (Ordinal, not IgnoreCase), **true** only for exact `sk_test_` / `sk_live_`. A middleware test that a minted key uppercased returns the same 401 as a non-key (or, if you choose to canonicalize, that only the prefix case is folded and the secret suffix is not). Do not hash `ToLower(token)` — that would invalidate every issued key if any secret letter is upper.

### Reproduction today
Arrange: mint a test key `sk_test_<secret>` and call any authenticated route with `Authorization: Bearer <exact key>` → 200 + `IsTestMode=true`. Act: same request with `SK_TEST_<SECRET>` or `Sk_Test_<secret>`. Assert: 401 `Invalid or revoked API Key.` (not a JWT challenge). Confirm `TryGetApiKey` is true for the uppercased string in a scratch test.

### Blast radius
Integrators who transform Authorization headers or paste keys into tools that uppercase. Confusing 401, not a privilege gain, not cross-tenant. Test-mode flag on a miss never reaches a principal because the middleware returns 401 first (`:46–52`). Frequency: support tickets, not attackers.

### Suggested fix
Parse prefixes with `StringComparison.Ordinal` (canonical lowercase only). Keep `HashToken` exact. Leave minted keys as they are. Do not lowercase the whole token. No TypeSpec regen.

### Evaluation notes
Still P2, still “confusing, not a bypass.” **125** (5-minute cache / revoke) is adjacent but different. R05 One-only lookup does not change case behavior. Stay P2.

