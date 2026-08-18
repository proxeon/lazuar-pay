---
number: "274"
id: B07-I34
severity: P2
status: open
source: plans/009-bugs/07-one-identity-invites-keys.md
head: "297ba98"
---

# 274 — B07-I34 — IApiCredentialService + LHDN façade comments still promise implicit LHDN defaults

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/07-one-identity-invites-keys.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B07-I34 — P2 — IApiCredentialService + LHDN façade comments still promise implicit LHDN defaults

**Where.** `IApiCredentialService.cs:32–34`; `AdminApiKeyEndpoints.cs:51`; `Lhdn/Domain/ApiKeyScopes.cs:14–17`.

**What.** Command rejects omit (`GenerateApiCredentialCommand.cs:57`; tests). Comments are a lying interface. High odds of a “compat” “fix” that re-opens the default.

## Evaluation (current tree, 2026-08-18)

### What the bug is
Machine-key mint **rejects** null, empty, and unknown scopes (`PlatformApiScopes.NormalizeAndValidate`). That is locked by tests and by the live One endpoint comment (`ApiCredentialEndpoints.cs:39`). Three comment sites still tell the next agent the opposite: omit means “LHDN document defaults.” The Lhdn HTTP façade comment says the same and then passes `req.Scopes` through as null. The obsolete `GenerateApiKeyCommandHandler` always calls `GenerateAsync(..., scopes: null)`, so `POST /lhdn/api-keys` with no scopes is a 400 today, not a defaulted LHDN key. TypeSpec `GenerateApiKeyRequestDto.scopes` still documents “Omit for LHDN document defaults.” A well-meaning “compat” change that makes omit succeed would re-open implicit `lhdn.documents:write` + `read` on every mint.

### Still present?
**DOCS / HONESTY ONLY** (runtime reject is correct and tested)

```32:34:apps/lazuar-api/Modules/One/Contracts/IApiCredentialService.cs
    /// <param name="scopes">
    /// Optional scope list from the closed platform catalog.
    /// Null/omitted uses LHDN document defaults; empty or unknown values are rejected.
```

```51:52:apps/lazuar-api/Modules/Lhdn/Infrastructure/Endpoints/AdminApiKeyEndpoints.cs
                // Null/omitted scopes → LHDN document defaults (product façade compat).
                IReadOnlyList<string>? scopes = req.Scopes is null ? null : req.Scopes.ToList();
```

```14:17:apps/lazuar-api/Modules/Lhdn/Domain/ApiKeyScopes.cs
    /// <summary>
    /// Default scopes granted to newly minted keys (v1 matrix).
    /// </summary>
    public const string DefaultDocumentScopes = LhdnDocumentsWrite + " " + LhdnDocumentsRead;
```

Command still: `var scopes = PlatformApiScopes.NormalizeAndValidate(request.Scopes);` (`GenerateApiCredentialCommand.cs:56–57`). One HTTP comment is honest (`ApiCredentialEndpoints.cs:39`). Lhdn command façade passes null (`GenerateApiKeyCommand.cs:39–45`). TypeSpec lie: `packages/api-spec/modules/one/models/api-keys.tsp:7–10` (generated into `Lazuar.ApiContracts.cs` / lhdn-sdk — do not regen just to chase this).

### Related files
- `apps/lazuar-api/Modules/One/Contracts/IApiCredentialService.cs` — XML to fix.
- `apps/lazuar-api/Modules/Lhdn/Infrastructure/Endpoints/AdminApiKeyEndpoints.cs` — comment vs pass-through.
- `apps/lazuar-api/Modules/Lhdn/Domain/ApiKeyScopes.cs` — `DefaultDocumentScopes` name is fine as a constant; the summary is the lie.
- `apps/lazuar-api/Modules/Lhdn/Application/Commands/GenerateApiKeyCommand.cs` — always omits scopes (400 at runtime).
- `apps/lazuar-api/Modules/One/Application/Commands/GenerateApiCredentialCommand.cs` — source of truth.
- `apps/lazuar-api/Modules/One/Infrastructure/Endpoints/ApiCredentialEndpoints.cs` — honest comment to copy.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/One/GenerateAndListApiCredentialsTests.cs` — keep `GenerateApiCredential_Omit_Scopes_Throws`.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Lhdn/GenerateAndListApiKeysTests.cs` — `GenerateApiKey_Delegates_To_Platform_Service` stubs `scopes: null` as a **success** returning `DefaultDocumentScopes` — that test documents the lie.
- `packages/api-spec/modules/one/models/api-keys.tsp` — residual copy; wrap-rail: no TypeSpec regen unless a dedicated honesty PR.

### Tests
- Existing: `GenerateApiCredential_Omit_Scopes_Throws`, `GenerateApiCredential_Empty_Scopes_Array_Throws`, `NormalizeAndValidate_Rejects_Unknown_And_Accepts_Catalog` — these **would fail** if someone “fixed” omit to default. `GenerateApiKey_Delegates_To_Platform_Service` would **not** catch a real 400 because it substitutes the façade.
- Comment-only drift has no test.
- First honesty test (optional): Lhdn `POST /lhdn/api-keys` with omitted scopes is 400 `At least one scope` (or the façade passes `ApiKeyScopes.Split(DefaultDocumentScopes)` **explicitly** if product wants LHDN defaults — that is a behavior change, not a comment tweak).

### Reproduction today
Arrange: OrgAdmin cookie + tenant. Act: `POST /api/v1/one/api-keys` with `{ "name": "x", "is_test_mode": true }` and no `scopes`. Assert: 400, no row. Act: `POST /api/v1/lhdn/api-keys` same body. Assert: 400 (despite the comment). Read the three comment sites; they still promise defaults.

### Blast radius
No live privilege today. The blast is the next agent who implements the comments and mints LHDN write keys for every “forgot scopes” caller. That is a least-privilege regression, not a crash. High odds because the Lhdn façade test already pretends omit succeeds.

### Suggested fix
Rewrite the three comments to match the command: omit/empty/unknown → 400; callers must send an explicit catalog list. If the Lhdn product façade should mint document scopes, pass `ApiKeyScopes.Split(DefaultDocumentScopes)` **explicitly** in `AdminApiKeyEndpoints` / `GenerateApiKeyCommandHandler` — do not teach `null` as default. Leave `GenerateApiCredential_Omit_Scopes_Throws` green. Do **not** TypeSpec-regen in the same PR unless you are already in an honesty contract change; the tsp sentence can wait. Do not restore implicit defaults.

### Evaluation notes
Honesty-only P2; keep severity. Sibling of 008 H and B07-I21 (human ADMIN bypass of Integration* policies) — different hole. Residual after 161–200. The Lhdn unit test that stubs omit→defaults is the landmine, not the command.

