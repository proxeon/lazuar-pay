---
number: "272"
id: B07-I32
severity: P2
status: open
source: plans/009-bugs/07-one-identity-invites-keys.md
head: "297ba98"
---

# 272 — B07-I32 — Genesis rotates superadmin password from env every boot

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/07-one-identity-invites-keys.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B07-I32 — P2 — Genesis rotates superadmin password from env every boot

**Where.** `SystemGenesisBootstrapperJob.cs:75–79`.

**What.** Convenient. A leaked `PLATFORM_ADMIN_PASSWORD` in the runtime env is a standing password reset. Dev `appsettings.Development.json:17–18` has `PLATFORM_ADMIN_EMAILS` / `PLATFORM_ADMIN_PASSWORD` in the repo. Dev-only.

## Evaluation (current tree, 2026-08-18)

### What the bug is
`SystemGenesisBootstrapperJob` still upserts platform admins from `PLATFORM_ADMIN_EMAILS` + `PLATFORM_ADMIN_PASSWORD` on **every process start**. If the user exists and `IPasswordService.Verify(envPassword, user.PasswordHash)` is false, it calls `user.ChangePassword(targetHash)`, which also rotates `SecurityStamp`. Anyone who can write the runtime env (or who already leaked it) can force the superadmin password back to that value on the next deploy/restart, wiping a password the human set in the product. Dev `appsettings.Development.json` commits `PLATFORM_ADMIN_EMAILS=admin@lazuar.com` and `PLATFORM_ADMIN_PASSWORD=Password123!`. Production `deploy/prod/env.example` documents the pair as optional first-boot seed. Convenient for local genesis; it is a standing reset channel wherever the env is set.

### Still present?
**STILL BROKEN**

```73:80:apps/lazuar-api/Modules/One/Infrastructure/Workers/SystemGenesisBootstrapperJob.cs
                else
                {
                    // Rotate password if the .env hash doesn't match the database hash
                    if (!passwordService.Verify(_settings.Password, user.PasswordHash))
                    {
                        user.ChangePassword(targetHash);
                        _logger.LogInformation("Rotated credentials for Superadmin: {Email}", normalizedEmail);
                    }
```

`ChangePassword` always mints a new stamp (`GlobalUser.cs:55–59`). Settings are bound from env on every boot (`Program.cs:66–70`). Dev secrets are in-repo (`appsettings.Development.json:17–18`). Missing env only skips the block (`SystemGenesisBootstrapperJob.cs:105–107`). Demo-tenant seed is Development-only and does **not** rotate (`:118–138`); the superadmin path is not gated on `IsDevelopment()`.

### Related files
- `apps/lazuar-api/Modules/One/Infrastructure/Workers/SystemGenesisBootstrapperJob.cs` — rotate-on-mismatch.
- `apps/lazuar-api/Modules/One/Infrastructure/Configuration/PlatformAdminSettings.cs` — options bag.
- `apps/lazuar-api/src/Lazuar.Api/Program.cs` — binds `PLATFORM_ADMIN_*`.
- `apps/lazuar-api/src/Lazuar.Api/appsettings.Development.json` — committed password.
- `deploy/prod/env.example` — documents the standing env pair.
- `apps/lazuar-api/Modules/One/Domain/GlobalUser.cs` — stamp rotate on `ChangePassword`.
- `apps/lazuar-api/Modules/One/README.md` §6 — still describes genesis as “securely upserts root Superadmin credentials from environment variables.”
- No `SystemGenesisBootstrapperJob` tests under `apps/lazuar-api/tests/`.

### Tests
- Existing: none for genesis rotate vs seed-only. `PlatformAdminAuthQueryTests` only reads an active admin. `SecurityStampMiddlewareTests.MismatchedStamp_Returns401` proves a rotate would 401 `/auth/me` but does not run genesis.
- No test fails if boot keeps overwriting the hash.
- First regression: existing superadmin whose DB hash does **not** match env must be left unchanged on `StartAsync`; a missing user is still created. Optional: `PLATFORM_ADMIN_ROTATE=true` is the only path that calls `ChangePassword`.

### Reproduction today
Arrange: Development API with `appsettings.Development.json` as shipped; log in as `admin@lazuar.com` / `Password123!`; `PUT /one/me/security/password` to something else. Act: restart the API. Assert: login with the new password fails; `Password123!` works again; `/one/auth/me` with the old cookie 401s (stamp). In Production, set `PLATFORM_ADMIN_PASSWORD` in the pod env and restart to take over the listed emails.

### Blast radius
Platform superadmin only (support / genesis emails), not tenant books. A leaked env or a shared `Password123!` in dev is a standing privileged reset. Every restart after an intentional password change is an outage for that human until they notice. Stamp rotate logs them out of `/auth/me` (117: other routes still accept the old cookie until expiry). Frequency: every boot when env is set and the DB hash drifted.

### Suggested fix
Seed only: `if (user == null) create; else` elevate `IsSystemAdmin` / system membership as today, **do not** `ChangePassword` unless an explicit rotate flag is set (and log it as a security event). Remove `PLATFORM_ADMIN_PASSWORD` from committed `appsettings.Development.json` (user-secrets / `.env` gitignored). Keep BCrypt work factor. Do not add MFA here (279). Do not TypeSpec.

### Evaluation notes
Still P2; audit’s “Dev-only” understates Production if the env pair is set (env.example invites that). Not a duplicate of 117 (stamp scope) or 279 (no lockout). Residual after 161–200: untouched. Do not “fix” by hashing the env password into git.

