---
number: "276"
id: B07-I36
severity: P2
status: open
source: plans/009-bugs/07-one-identity-invites-keys.md
head: "297ba98"
---

# 276 — B07-I36 — AppOptions ClientUrl default 3020 vs live 3004

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/07-one-identity-invites-keys.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B07-I36 — P2 — AppOptions ClientUrl default 3020 vs live 3004

**Where.** `AppOptions.cs:10` vs `appsettings.json:41` vs `OneLinkService.cs:17`.

**What.** Dead default today. Future bind-to-options foot-gun for reset/verify (already 404 on 3004).

## Evaluation (current tree, 2026-08-18)

### What the bug is
At audit HEAD `AppOptions.ClientUrl` defaulted to `http://localhost:3020` and the XML said “typically port 3020.” Port 3020 is `examples/hub-cashier-next`, not the buyer portal (`lazuar-portal` on 3004) and not ops (3003). Live readers never bound `IOptions<AppOptions>`; they read `App:ClientUrl` from configuration (`appsettings` already 3004) or had their own fallbacks. The 3020 default was a future foot-gun: the first caller to `Configure<AppOptions>` without config would send reset/verify/checkout links to the sample cashier. Issue 169 (`fix/169-clienturl-fallback`) aligned the default and introduced `AppClientUrl.Resolve` with fallback `http://localhost:3004`. This issue’s specific claim (3020 vs 3004) is gone. Reset/verify *pages* were a separate 404 (112); those links now go to ops (`OneLinkServiceTests.ResetAndVerifyEmails_UseOpsUrl_NotClientUrl`).

### Still present?
**ALREADY FIXED** (likely issue **169** / `fix/169-clienturl-fallback`)

```7:10:apps/lazuar-api/src/Lazuar.Api/Configuration/AppOptions.cs
    /// The primary client-facing frontend URL (portal / public checkout surfaces, typically port 3004).
    /// </summary>
    public string ClientUrl { get; init; } = "http://localhost:3004";
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
            return DevelopmentFallback;
        return value.TrimEnd('/');
    }
}
```

`appsettings.json:41` and `appsettings.Development.json:30` are `http://localhost:3004`. `OneLinkService.GetClientBaseUrl()` delegates to `AppClientUrl.Resolve` (`OneLinkService.cs:15–18`). Grep of `Configure<AppOptions>` / `IOptions<AppOptions>` is still **empty** — the type remains unbound documentation. That leftover is 169’s “unbound” half, not a 3020 default.

### Related files
- `apps/lazuar-api/src/Lazuar.Api/Configuration/AppOptions.cs` — default now 3004.
- `apps/lazuar-api/BuildingBlocks/Infrastructure/AppClientUrl.cs` — shared fallback.
- `apps/lazuar-api/Modules/One/Infrastructure/Services/OneLinkService.cs` — invite still uses `OpsUrl` (3003); client URL is portal.
- `apps/lazuar-api/src/Lazuar.Api/appsettings.json` / `appsettings.Development.json` — live config 3004.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/BuildingBlocks/AppClientUrlTests.cs` — `Missing_Config_Uses_Portal_Port_3004`.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/One/OneLinkServiceTests.cs` — invite URL must not contain 3004.
- `examples/hub-cashier-next/` — the process that actually listens on 3020.
- Issues **169** (resolved P1, same default + unbound + disagreeing fallbacks) and **324** (open P2, B09-U56, same 3020 sentence).

### Tests
- Existing: `AppClientUrlTests.Missing_Config_Uses_Portal_Port_3004`; `AppClientUrlTests.Configured_Value_Is_Trimmed`; `OneLinkServiceTests.GetOpsBaseUrl_UsesOpsUrl_AndInviteUrlDoesNotContainClientUrl`.
- Those tests **would fail** if someone put 3020 back in `AppClientUrl.DevelopmentFallback`. They would **not** fail if `AppOptions.ClientUrl` default flipped to 3020 again, because nothing binds `AppOptions`.
- First extra guard if you touch this: assert `new AppOptions().ClientUrl` contains `3004` not `3020` (cheap) or bind `IOptions<AppOptions>` once and delete the class default.

### Reproduction today
Grep `localhost:3020` in `AppOptions.cs` — no hit. Construct `new AppOptions()` in a scratch test — `ClientUrl` is 3004. Unset `App:ClientUrl` and call `OneLinkService.GetClientBaseUrl()` — 3004 via `AppClientUrl`. `pnpm example:cashier` is still 3020; that is the sample, not the portal.

### Blast radius
None for the 3020 default anymore. Residual: `AppOptions` still unused; a future bind is now safe if they keep the 3004 default. Buyer-link 404s belong to 112 (pages), not this default.

### Suggested fix
Do not re-fix. Close this as a duplicate of 169. When someone next evaluates 324, mark that the same way. Optional leftover from 169: either bind `AppOptions` to `IConfiguration` section `App` and delete scattered reads, or delete `AppOptions` if it stays unbound. Do not point `ClientUrl` at 3020. Do not TypeSpec. Invite URLs stay `App:OpsUrl`.

### Evaluation notes
169 / 276 / 324 are the same 3020-vs-3004 sentence in three slices (tenancy, One, frontends). 169 was P1 because fallbacks also disagreed (`portal.lazuar.com`); that helper is now `AppClientUrl`. 276/324 can stay open in YAML (instruction: do not change status) but an implementer should not spend a third PR on the default. Residual after 161–200: default fixed; type still unbound.

