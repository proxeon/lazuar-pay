---
number: "279"
id: B07-I39
severity: P2
status: open
source: plans/009-bugs/07-one-identity-invites-keys.md
head: "297ba98"
---

# 279 — B07-I39 — No MFA, SSO, lockout, session list, password complexity

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/07-one-identity-invites-keys.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B07-I39 — P2 — No MFA, SSO, lockout, session list, password complexity

**Where.** `PasswordService` is BCrypt work factor 11 (`PasswordService.cs:15–16`; `appsettings.json:32–34`). `GlobalUser` has no lockout/MFA/last-login.

**What.** Procurement-questionnaire fail. Not a crash. Do not put “SSO” on a pricing page.

## Evaluation (current tree, 2026-08-18)

### What the bug is
One identity is still password + cookie. `PasswordService` is BCrypt at work factor 11 (`Security:PasswordWorkFactor`). `GlobalUser` has no lockout counter, no MFA secret, no last-login, no session table. Register / change-password / reset hash whatever string they are given — no minimum length, class, or breach check. There is no SSO (OIDC/SAML) and no “sessions” UI. Login is no longer unlimited (issue 121 added `PublicAuthRateLimiter` on login/forgot/resend), which is adjacent hygiene, not MFA/lockout/SSO. Procurement questionnaires that ask for SSO/MFA/lockout/session revocation will fail. The audit’s instruction still holds: do not put “SSO” on a pricing page.

### Still present?
**STILL BROKEN** (login rate-limit half is **already** 121)

```11:16:apps/lazuar-api/BuildingBlocks/Infrastructure/PasswordService.cs
    public PasswordService(IConfiguration configuration)
    {
        _workFactor = configuration.GetValue<int>("Security:PasswordWorkFactor", 11);
    }

    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, workFactor: _workFactor);
    public bool Verify(string password, string hash) => BCrypt.Net.BCrypt.Verify(password, hash);
```

```14:25:apps/lazuar-api/Modules/One/Domain/GlobalUser.cs
    public bool IsSystemAdmin { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsEmailVerified { get; private set; }
    ...
    public string? PasswordResetTokenHash { get; private set; }
```

No `AccessFailedCount` / `TwoFactor` / `LastLoginAt`. `RegisterPublicUserCommand` hashes `request.Password` with no policy (`:56`). `ChangePasswordCommand` only checks the current password (`:26–30`). `appsettings.json:32–34` is only `PasswordWorkFactor: 11`. Grep of `Mfa`/`SSO`/`lockout`/`TwoFactor` in `*.cs`/`*.tsx` is empty. Login limiter: `AuthEndpoints.cs:83–93` + `PublicAuthRateLimiterTests`. Dev genesis password is still `Password123!` (`appsettings.Development.json:18`).

### Related files
- `apps/lazuar-api/BuildingBlocks/Infrastructure/PasswordService.cs` — hash only.
- `apps/lazuar-api/Modules/One/Domain/GlobalUser.cs` — no lockout/MFA fields.
- `apps/lazuar-api/Modules/One/Application/Commands/RegisterPublicUserCommand.cs` / `ChangePasswordCommand.cs` / `ResetPasswordCommand.cs`.
- `apps/lazuar-api/Modules/One/Infrastructure/Endpoints/AuthEndpoints.cs` — login + 121 limiter.
- `apps/lazuar-api/src/Lazuar.Api/appsettings.json` — work factor only.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/One/PublicAuthRateLimiterTests.cs` — `Blocks_After_Budget`, `Empty_Key_Is_Denied`.
- `apps/lazuar-api/Modules/One/README.md` — “BCrypt” and nothing about MFA.
- Issues **121** (login unlimited, resolved), **117** (stamp only on `/auth/me` — session kill is not a session list), **272** (genesis rotates to `Password123!`).

### Tests
- Existing: `PublicAuthRateLimiterTests.Blocks_After_Budget`; register/login tests do not assert password shape; no MFA/SSO tests (correct — those features are absent).
- No test fails because MFA/lockout/complexity are missing. A complexity rule would need new tests.
- First small regression if you add a policy: register/`ChangePassword`/`ResetPassword` reject length < 10 (or whatever you pick); login increments a failure counter and 423/429s after N; existing `Password123!` genesis must be updated or exempted in Development only.

### Reproduction today
Act: `POST /one/public/register` with password `a`. Assert: 200 (or domain slug errors), hash stored. Act: `POST /one/auth/login` with a wrong password until you exceed `PublicAuthRateLimiter.Limit` — 429 (121), then wait/reset process memory and continue; no per-user lockout in the DB. Act: look for `/one/me/sessions` or MFA enroll — no route. Ops login has no SSO button.

### Blast radius
Every merchant and the platform admin. Online guessing is now rate-limited in-process (121), not locked out across instances or after password-change. Shared/simple passwords (`Password123!` in the repo) are valid. No money path by itself; a guessed ADMIN cookie is keys + refunds + archive. Frequency: constant background risk; procurement fails every enterprise questionnaire.

### Suggested fix
Do **not** build SSO/MFA in this issue. Smallest honest increment: a single password policy (min length + reject the committed `Password123!` in Production) on register/change/reset, and a `GlobalUser` lockout (failed count + `LockoutEnd`) that login honors **in addition to** the in-process limiter. Session list/MFA/SSO are product epics; they need a session table (117’s stamp is not one) and an IdP. Do not advertise SSO on pricing/docs. Do not TypeSpec-regen a fake `/sso` route. Keep BCrypt; do not roll a homemade KDF.

### Evaluation notes
Still P2 procurement, not a crash. 121 reduced the “unlimited login” P1; this issue remains. 272 makes the committed superadmin password worse. 117 is the stolen-cookie half of “no session list.” Do not close 279 because rate-limit exists. Residual after 161–200: limiter only.

