---
number: "258"
id: B07-I14
severity: P2
status: resolved
resolved_branch: fix/258-register-invite-copy
source: plans/009-bugs/07-one-identity-invites-keys.md
head: "297ba98"
---

# 258 — B07-I14 — Register always creates a workspace (invite leftover)

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/07-one-identity-invites-keys.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B07-I14 — P2 — Register always creates a workspace (invite leftover)

**Where.** `RegisterPublicUserCommand.cs:54–62`; `LoginPage.tsx:208–307` (`inviteReturn` only changes the subtitle).

**What.** New invitee who uses Sign up becomes ADMIN of a stray tenant and MEMBER/VIEWER/ADMIN of the invited one. Accept still works (email match). Extra starter-credit / entitlement events fire for W2. Not an accept-breaker. Do not “fix” this by blocking register-from-invite without a join-without-workspace API.

## Evaluation (current tree, 2026-08-18)

### What the bug is
Public register used to always mint a workspace + ADMIN membership + core entitlement events, even when the human was only signing up to accept an invite. The invitee then owned a leftover tenant (starter credits / W2 entitlement noise) **and** joined the invited workspace. Accept still worked on email match. The honest fix the audit asked for is a join-without-workspace register path, not “block signup from invite.”

### Still present?
**ALREADY FIXED**

Likely **146** (`B09-U17`, `fix/146-invite-signup-no-dummy`) plus the handler change covered by `Empty_Workspace_Creates_User_Only`. Register now creates a workspace only when both name and slug are non-empty:

```37:64:apps/lazuar-api/Modules/One/Application/Commands/RegisterPublicUserCommand.cs
        var createWorkspace = !string.IsNullOrWhiteSpace(request.WorkspaceName)
            && !string.IsNullOrWhiteSpace(request.TenantSlug);
        // ...
        if (createWorkspace)
        {
            // Validate slug before tracking a user so reserved/malformed slugs write nothing.
            organization = new Organization(request.WorkspaceName, slug);
        }
```

The endpoint requires the pair together (`AuthEndpoints.cs:44–47`). Ops signup on an invite return URL now **hides** workspace fields and submits empty strings:

```91:96:apps/lazuar-ops/src/components/LoginPage.tsx
    let workspace_name = workspaceName.trim();
    let tenant_slug = slugify(tenantSlug);
    if (inviteReturn) {
      workspace_name = "";
      tenant_slug = "";
    }
```

`inviteReturn` is `returnUrl?.startsWith("/accept-invite")` (`LoginPage.tsx:37`). Residual copy only: submit button still says “Create workspace” (`:320`) and invite signup subtitle reuses “Sign in with the invited email.” (`:219–221`). That is not a stray tenant.

### Related files
- `apps/lazuar-api/Modules/One/Application/Commands/RegisterPublicUserCommand.cs` — user-only vs user+workspace.
- `apps/lazuar-api/Modules/One/Infrastructure/Endpoints/AuthEndpoints.cs:32–75` — pair check, cookie issue, register `Role = "ADMIN"` in the JSON body (see **259**).
- `apps/lazuar-ops/src/components/LoginPage.tsx` — invite return strips workspace fields.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/One/RegisterPublicUserCommandHandlerTests.cs` — `Empty_Workspace_Creates_User_Only`.
- `issues/146-p1-b09-u17-invite-signup-still-creates-a-dummy-workspace.md` — P1 twin, resolved.

### Tests
- Existing: `Empty_Workspace_Creates_User_Only` asserts no org, no membership, no entitlements, no `IsSlugUniqueAsync`, one `SaveChanges`. `HappyPath_Creates_User_Workspace_Admin_And_Core_Entitlements` still covers the non-invite path.
- Those handler tests **would fail** if register always created a workspace again. There is no ops test that `inviteReturn` submits empty name/slug (issue **325**).
- First regression if this regresses: keep `Empty_Workspace_Creates_User_Only`; add an RTL/assert on LoginPage that `/signup?returnUrl=/accept-invite?token=…` does not render workspace fields and POSTs `workspace_name: ""`, `tenant_slug: ""`.

### Reproduction today
Arrange: pending invite for `invitee@example.com`. Act: open `/signup?returnUrl=/accept-invite%3Ftoken%3D…`, register that email without seeing workspace fields. Assert: `one.Organizations` does not gain a row for this user; `POST /one/workspaces/invites/accept` then creates only the invited membership. A normal `/signup` without returnUrl still requires name+slug and still creates ADMIN + core apps.

### Blast radius
Was: extra tenants, extra `AppEntitlementGrantedIntegrationEvent`s, confused EmptyWorkspaceState. Now: invitees get a user row only; after accept they have one workspace. Residual button label can confuse but does not mint a tenant. No money.

### Suggested fix
None for the product hole. Optional copy: invite signup button “Create account” / subtitle “Create an account with the invited email.” Do not block register-from-invite. Do not TypeSpec-regen.

### Evaluation notes
Duplicate of resolved **146**. Leave YAML `open` as instructed. **259** still lies that register’s JSON `Role` is `ADMIN` even on the user-only path (cookie JWT is still `CLIENT`). Not blocked. Not a 161–200 fail-closed item.

## Resolution

Product hole already closed by 146. Invite signup subtitle/button now say create account, not create workspace.

