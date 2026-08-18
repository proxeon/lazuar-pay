---
number: "324"
id: B09-U56
severity: P2
status: open
source: plans/009-bugs/09-frontends-ops-portal-admin.md
head: "297ba98"
---

# 324 — B09-U56 — AppOptions default ClientUrl 3020

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/09-frontends-ops-portal-admin.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

#### B09-U56 — AppOptions default ClientUrl 3020 (P2, FE-adjacent)

`AppOptions.cs` 8–10. Comment says portal is “typically port 3020.” Portal is 3004. Sample app is 3020.

## Evaluation (current tree, 2026-08-18)

### What the bug is
At audit HEAD, `AppOptions.ClientUrl` defaulted to `http://localhost:3020` and the XML comment said portal was “typically port 3020.” Port 3020 is `examples/hub-cashier-next`, not the buyer portal (3004). `appsettings.json` already overrode to 3004, so a normal boot was fine. Anyone binding `IOptions<AppOptions>` without JSON, or copying the comment into a new FE env, would mint checkout / magic / recovery URLs at the sample-app port. The live readers mostly used `IConfiguration["App:ClientUrl"]` with their own fallbacks, which disagreed (portal.lazuar.com vs 3004 vs 3020).

### Still present?
**ALREADY FIXED**

Issue **169** (`fix/169-clienturl-fallback`) defaulted the type, the comment, and the shared fallback to 3004. Current tree:

```7:15:apps/lazuar-api/src/Lazuar.Api/Configuration/AppOptions.cs
    /// <summary>
    /// The primary client-facing frontend URL (portal / public checkout surfaces, typically port 3004).
    /// </summary>
    public string ClientUrl { get; init; } = "http://localhost:3004";

    /// <summary>
    /// Staff ops console URL (workspace invite accept, typically port 3003).
    /// </summary>
    public string OpsUrl { get; init; } = "http://localhost:3003";
```

```6:20:apps/lazuar-api/BuildingBlocks/Infrastructure/AppClientUrl.cs
/// Buyer-facing portal / checkout host. Default matches <c>App:ClientUrl</c> in appsettings (port 3004).
public static class AppClientUrl
{
    public const string DevelopmentFallback = "http://localhost:3004";

    public static string Resolve(IConfiguration? configuration)
    {
        var value = configuration?["App:ClientUrl"];
        if (string.IsNullOrWhiteSpace(value))
        {
            return DevelopmentFallback;
        }
        return value.TrimEnd('/');
    }
}
```

`appsettings.json:41` is `http://localhost:3004`. Grep of `apps/` for `typically port 3020` / `ClientUrl … 3020` is empty. 3020 remains only as a CORS origin for the sample app (`appsettings.json:46`) and docs for `hub-cashier-next` — that is correct.

`Configure<AppOptions>` / `IOptions<AppOptions>` is still **unbound** (grep empty). The type is still documentation; live code uses `AppClientUrl.Resolve` / `config["App:ClientUrl"]`. The foot-gun of a *wrong default on the type* is gone.

### Related files
- `apps/lazuar-api/src/Lazuar.Api/Configuration/AppOptions.cs` — default + comment now 3004.
- `apps/lazuar-api/BuildingBlocks/Infrastructure/AppClientUrl.cs` — shared fallback 3004.
- `apps/lazuar-api/src/Lazuar.Api/appsettings.json` — `App:ClientUrl` 3004.
- `apps/lazuar-api/Modules/One/Infrastructure/Services/OneLinkService.cs` — `GetClientBaseUrl()` → `AppClientUrl.Resolve`.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/BuildingBlocks/AppClientUrlTests.cs` — `Missing_Config_Uses_Portal_Port_3004`.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/One/OneLinkServiceTests.cs` — invite links are OpsUrl, not ClientUrl 3004.
- `issues/169-p1-b10-x13-appoptions-clienturl-default-3020-is-unbound-three-other-fallbac.md` — the P1 that shipped this.
- `issues/276-p2-b07-i36-appoptions-clienturl-default-3020-vs-live-3004.md` — still `status: open`; same residue.

### Tests
- Existing: `AppClientUrlTests.Missing_Config_Uses_Portal_Port_3004` would fail if the fallback returned 3020. `Configured_Value_Is_Trimmed` covers override.
- No test instantiates `new AppOptions().ClientUrl`, so a future edit of the property initializer back to 3020 would not fail until someone binds the type. Low risk because nothing binds it.
- First extra lock if you touch this: `Assert.That(new AppOptions().ClientUrl, Is.EqualTo("http://localhost:3004"))`.

### Reproduction today
Arrange: boot API with repo `appsettings.json` (no extra `App__ClientUrl`). Act: trigger any ClientUrl reader (magic link, arrears success URL, `AppClientUrl.Resolve`). Assert: host is `:3004`, not `:3020`. Act: `new AppOptions().ClientUrl` in a scratch test / immediate window — `http://localhost:3004`. Sample cashier on `:3020` still works as an origin, not as ClientUrl.

### Blast radius
Was: mis-minted buyer links to a process that is not the portal (404 / wrong app). After 169: none from this default. Residual: `AppOptions` still unused in DI; new code that binds it without JSON now gets 3004, which matches portal. Production must still set `App__ClientUrl` (see `deploy/prod/env.example`). Not P2 anymore.

### Suggested fix
Do not change ports again. Close 324 (and 276) as duplicates of 169. Optional one-line: bind `Configure<AppOptions>` so the type is not dead documentation — out of scope unless you are already in composition. Do not point ClientUrl at 3020. No TypeSpec.

### Evaluation notes
**169 already defaulted App:ClientUrl to 3004.** 324 and 276 are residual docs from the same 17 Aug audit, filed before that fix landed on main. 276 YAML is still `open`; do not edit it here. FE bake `NEXT_PUBLIC_OPS_URL` (U06 / 135) is a different bug — portal accept-invite 302 — not this default.

