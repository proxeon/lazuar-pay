---
number: "259"
id: B07-I15
severity: P2
status: resolved
resolved_branch: fix/259-register-role-honesty
source: plans/009-bugs/07-one-identity-invites-keys.md
head: "297ba98"
---

# 259 — B07-I15 — Dual role model + register body `ADMIN` vs cookie `CLIENT`

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/07-one-identity-invites-keys.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B07-I15 — P2 — Dual role model + register body `ADMIN` vs cookie `CLIENT`

**Where.** `AuthEndpoints.cs:71, 93, 197`; `TenantSecurityMiddleware.cs:83–88`; `TenantMembership.cs:10` comment; `Modules/One/README.md:22, 33–34`.

**What.** Teachability hole. Scalar without `X-Tenant-Id` fails `OrgAdmin`. README still says membership roles `ADMIN` / `CLIENT` and that a paid subscription “may grant a `CLIENT` membership.” No such handler exists. Next agent who “aligns invite with the README” will try to re-introduce `CLIENT` as staff; invite tests currently reject that string — **keep those tests**.

## Evaluation (current tree, 2026-08-18)

### What the bug is
One has two role vocabularies that share string names. The cookie JWT always carries `ClaimTypes.Role = CLIENT` (or `SUPER_ADMIN` for genesis). Staff power is a **membership** role (`ADMIN` / `MEMBER` / `VIEWER`) injected onto the principal only after `TenantSecurityMiddleware` sees `X-Tenant-Id` / slug. Register’s JSON body still reports `Role = "ADMIN"` even though the cookie it just set is `CLIENT`. Scalar or any client that omits the tenant header therefore fails `OrgAdmin` despite the register response. The module README still documents membership roles as `ADMIN` / `CLIENT` and claims a paid subscription “may grant a `CLIENT` membership.” No production handler inserts `TenantMembership(..., "CLIENT")`. An agent who “aligns invite with the README” will try to mint `CLIENT` as staff.

### Still present?
**STILL BROKEN**

Register response vs cookie vs login:

```69:74:apps/lazuar-api/Modules/One/Infrastructure/Endpoints/AuthEndpoints.cs
            return TypedResults.Ok(new LoginResponse
            {
                User = new AuthUser { Email = user!.Email, Name = user.Name, Role = "ADMIN", Is_email_verified = user.IsEmailVerified }
            });
```

```109:110:apps/lazuar-api/Modules/One/Infrastructure/Endpoints/AuthEndpoints.cs
            var role = user.IsSystemAdmin ? "SUPER_ADMIN" : "CLIENT";
```

```273:277:apps/lazuar-api/Modules/One/Infrastructure/Endpoints/AuthEndpoints.cs
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.IsSystemAdmin ? "SUPER_ADMIN" : "CLIENT"),
```

Injection still happens only with a resolved tenant (`TenantSecurityMiddleware.cs:83–94`). README still teaches the wrong staff enum and a fictional CLIENT grant (`apps/lazuar-api/Modules/One/README.md:22, 33–34`). Domain comment still says `// e.g. "ADMIN", "CLIENT"` (`TenantMembership.cs:10`). Invite still **rejects** `CLIENT` (`InviteUserToWorkspaceCommandHandlerTests.Invite_DisallowedRole_Throws`). I grepped `Modules/` for `new TenantMembership(` — production inserts are `ADMIN`, `SUPER_ADMIN` (genesis), provision `ownerRole`, or the invitation’s staff role. No `CLIENT` membership writer.

### Related files
- `apps/lazuar-api/Modules/One/Infrastructure/Endpoints/AuthEndpoints.cs` — register `ADMIN` JSON, login/cookie `CLIENT`.
- `apps/lazuar-api/src/Lazuar.Api/Middleware/TenantSecurityMiddleware.cs` — injects membership role when tenant is present.
- `apps/lazuar-api/Modules/One/Domain/TenantMembership.cs` — comment names `CLIENT`.
- `apps/lazuar-api/Modules/One/README.md` — staff roles + “may grant CLIENT”.
- `apps/lazuar-api/Modules/One/Application/Commands/InviteUserToWorkspaceCommand.cs` — closed staff set.
- `apps/lazuar-api/Modules/Ops/Infrastructure/Endpoints.cs:10` — `/ops` `RequireRole("CLIENT", "ADMIN")` (JWT vs injected).
- `apps/lazuar-api/tests/Lazuar.ModuleTests/One/InviteUserToWorkspaceCommandHandlerTests.cs` — keep `CLIENT` reject.
- `issues/123-p1-b07-i20-integratorprovisionauth-treats-injected-membership-superadmin-as.md` — adjacent claim-injection hole.

### Tests
- Existing: `Invite_DisallowedRole_Throws` (`CLIENT`), `HappyPath_Creates_User_Workspace_Admin_And_Core_Entitlements` (membership `ADMIN`), `OrgAdmin_Policy_Allows_Human_Admin` (policy role `ADMIN`).
- No test would fail today because register JSON says `ADMIN` while the cookie is `CLIENT`, or because the README is wrong. Those tests would **fail** if someone “fixed” invite by allowing `CLIENT` — **keep them**.
- First regression: (1) register (with or without workspace) response `user.role` equals the JWT role (`CLIENT`, unless system admin); (2) `GET /one/auth/me` without `X-Tenant-Id` is `CLIENT`; (3) with `X-Tenant-Id` of a workspace the user administers, `OrgAdmin` succeeds; (4) invite `CLIENT` still 400. Snapshot or lint the README membership bullet so it cannot reintroduce CLIENT as staff.

### Reproduction today
Arrange: `POST /one/public/register` with a unique email + workspace. Act: read `user.role` in the JSON (`ADMIN`) and decode `lazuar_auth` (`role`/`CLIENT`). Call `POST /one/api-keys` (or any `OrgAdmin` route) **without** `X-Tenant-Id` → 400 missing tenant or 403, not admin. Repeat with `X-Tenant-Id` of the new org → 200. Read `Modules/One/README.md` §4–5: still `ADMIN`/`CLIENT` and “may grant a CLIENT membership.”

### Blast radius
Integrators, Scalar explorers, and the next implementer. Not a cross-tenant steal. Register-from-invite (**258** fixed) now returns `Role=ADMIN` for a user who has **zero** memberships — worse teachability than at audit time. Frequency: every register and every docs-driven invite change.

### Suggested fix
Smallest honest change: register/login/`/auth/me` JSON `Role` = JWT role (`CLIENT` / `SUPER_ADMIN`). Do not put membership role in the unauthenticated-tenant body. Rewrite README §4–5 to staff `{ADMIN, MEMBER, VIEWER}` and JWT `{CLIENT, SUPER_ADMIN}`; delete the “paid subscription may grant CLIENT membership” sentence (no handler). Leave `TenantMembership` comment or change it to staff examples only. **Keep** `Invite_DisallowedRole_Throws("CLIENT")`. No TypeSpec regen if `AuthUser.role` stays a string.

### Evaluation notes
Docs/honesty more than a crash; still a live API lie, so not `DOCS / HONESTY ONLY` alone. Dual-realm is load-bearing (`/ops` allows `CLIENT`). Do not “unify” by putting `ADMIN` on the JWT — that bypasses tenant injection. Adjacent: **123** (injected `SUPER_ADMIN`), **263** (human ADMIN on Integration*), **267** (cookie vs Bearer). Stay P2.

## Resolution

Register JSON `user.role` matches the cookie JWT (`CLIENT` / `SUPER_ADMIN`). README staff roles are ADMIN/MEMBER/VIEWER. Invite still rejects CLIENT.

