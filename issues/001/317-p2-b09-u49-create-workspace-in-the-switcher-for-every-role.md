---
number: "317"
id: B09-U49
severity: P2
status: open
source: plans/009-bugs/09-frontends-ops-portal-admin.md
head: "297ba98"
---

# 317 — B09-U49 — Create workspace in the switcher for every role

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/09-frontends-ops-portal-admin.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

#### B09-U49 — Create workspace in the switcher for every role (P2)

`PageLayout.tsx` 101–107. Viewer can try. Outcome depends on `POST /one/workspaces`.

## Evaluation (current tree, 2026-08-18)

### What the bug is
Every Hub Ops page that uses `PageLayout` paints a workspace switcher. At the bottom of that menu, **Create New Workspace** is always shown — there is no check of the current entitlement role. A VIEWER (or MEMBER) of workspace A can open the modal and `POST /one/workspaces`. The API does not require ADMIN on the current tenant; any authenticated user with a non-empty `UserId` may create a *new* organization and is written as ADMIN of it. The audit called this wrong because the switcher implies a workspace-scoped action, but the write is global account-level provisioning. A Viewer who is supposed to “only read” can mint a sibling tenant (slug, entitlements OPS/BILLING/PAYMENTS/CRM/LHDN) from any page.

### Still present?
**STILL BROKEN**

The switcher still has an unguarded create button. Audit cited `PageLayout.tsx` 101–107; after the mobile hamburger landed, the same control is at 115–120:

```115:120:apps/lazuar-ops/src/modules/core/components/PageLayout.tsx
                  <button 
                    onClick={() => { setIsCreateModalOpen(true); setIsWorkspaceMenuOpen(false); }} 
                    className="w-full flex items-center gap-2 px-3 py-2 text-left text-[12px] font-medium text-[#09090b] hover:bg-[#f4f4f5] transition-colors focus:outline-none"
                  >
                    <Plus size={14} className="text-[#a1a1aa]" /> Create New Workspace
                  </button>
```

`role` is already on the outlet context (`PageLayout.tsx:22`) and is used only to badge the current workspace (`PageLayout.tsx:87–90`). It is not used to hide create. The modal always POSTs:

```32:38:apps/lazuar-ops/src/modules/workspace/components/CreateWorkspaceModal.tsx
      const { data, error } = await client.POST("/one/workspaces", {
        body: {
          name: name.trim(),
          slug: normalized,
          provision_apps: ["OPS", "BILLING", "PAYMENTS", "CRM", "LHDN"]
        }
      });
```

The endpoint only checks that someone is logged in:

```19:26:apps/lazuar-api/Modules/One/Infrastructure/Endpoints/WorkspaceEndpoints.cs
        group.MapPost("/workspaces", async Task<Results<Ok<IdResponse>, UnauthorizedHttpResult>> (
            CreateWorkspaceRequestDto req, IExecutionContextAccessor ctx, IMediator mediator) =>
        {
            if (ctx.UserId == Guid.Empty) return TypedResults.Unauthorized();

            var id = await mediator.Send(new CreateWorkspaceCommand(ctx.UserId, req.Name, req.Slug, req.Provision_apps?.ToList() ?? new List<string>()));
            return TypedResults.Ok(new IdResponse { Id = id.ToString() });
        }).RequireAuthorization();
```

`CreateWorkspaceCommandHandler` (`CreateWorkspaceCommand.cs:36–66`) does not inspect the caller’s role on any existing tenant. It creates the org, adds `TenantMembership(..., "ADMIN")`, and grants the requested apps. So a Viewer who tries **succeeds** and is immediately switched into the new workspace as ADMIN (`PageLayout.tsx:161–164` `onWorkspaceSelect(id)`).

The empty-state create (`EmptyWorkspaceState.tsx:23–28`) is a different, legitimate path (signed in, zero entitlements). Pricing / login “Create workspace” is public signup, not this switcher.

### Related files
- `apps/lazuar-ops/src/modules/core/components/PageLayout.tsx` — unguarded switcher button + modal mount.
- `apps/lazuar-ops/src/modules/workspace/components/CreateWorkspaceModal.tsx` — `POST /one/workspaces`.
- `apps/lazuar-ops/src/App.tsx` — `workspaceRoleOf` / `OpsOutletContext.role` already computed for every page.
- `apps/lazuar-ops/src/components/EmptyWorkspaceState.tsx` — create when the user has no workspace (keep).
- `apps/lazuar-api/Modules/One/Infrastructure/Endpoints/WorkspaceEndpoints.cs` — create is auth-only.
- `apps/lazuar-api/Modules/One/Application/Commands/CreateWorkspaceCommand.cs` — any existing user becomes ADMIN of the new org.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/One/CreateWorkspaceCommandHandlerTests.cs` — slug / missing-user tests; no “Viewer of A cannot create B” case.
- `issues/177-p1-b10-x21-one-workspaces-exemption-empty-ambient-is-a-loaded-gun.md` — `/one/workspaces` prefix is tenant-middleware exempt (resolved 177); create remains intentionally account-scoped.

### Tests
- Existing: `CreateWorkspaceCommandHandlerTests.Authenticated_User_With_Zero_Memberships_Creates_Admin_Workspace`, `Duplicate_Slug_Throws_And_Writes_Nothing`, `Reserved_Slug_Throws_Business_Rule`, `Missing_User_Throws`. Those prove create works for a logged-in user; they do **not** fail if the switcher is painted for VIEWER.
- No ops component/Playwright test exists (325). No API test asserts “must be ADMIN of *some* workspace” or “must have zero workspaces.”
- First regression test: render `PageLayout` with `role: "VIEWER"` and assert the create button is absent (or opens a “ask an Admin” copy). If product instead wants any user to provision a new tenant, assert that and close this as product, not a hide-the-button bug. Do not add a Stripe Billing hook.

### Reproduction today
Arrange: user who is VIEWER on workspace A (invite as VIEWER, accept). Sign in to ops on `:3003`. Act: open any page (Team, Sales Insights, Products) → workspace name in the header → **Create New Workspace** → name + unused slug → submit. Assert: `POST /api/v1/one/workspaces` is 200; toast “Workspace created successfully”; entitlements refresh; the new workspace is selected and the badge reads ADMIN. Repeat as MEMBER — same success. Contrast with Team invite, which is already hidden unless `ADMIN` / `SUPER_ADMIN` (`TeamPage.tsx:14,68`).

### Blast radius
Who: any VIEWER or MEMBER who can log into Hub Ops. What: they can mint a new billable-looking tenant (apps provisioned include BILLING/PAYMENTS/LHDN) without an existing Admin’s consent. Money: not a drain of workspace A’s ledger, but a new org they fully control (invite others, attach a vault, take checkout). Frequency: every authenticated session; the button is on every `PageLayout` page. Ops/PII: slug squatting if they pick a reserved-looking public slug (reserved list is enforced). This is still P2, not P0 — they do not write workspace A.

### Suggested fix
Smallest correct UI change: hide **Create New Workspace** unless `role` is `ADMIN` or `SUPER_ADMIN`, *or* unless product explicitly wants “any login may own a second workspace,” in which case leave the API as-is and document it on the Team page (“Viewers can only read *this* workspace”). Do not put a role check on `POST /one/workspaces` that would also break `EmptyWorkspaceState` (zero memberships). Keep slug validation. No TypeSpec regen. No Wave 5 / WhatsApp.

### Evaluation notes
Still P2. Not a duplicate of 177 (that was IDOR / empty ambient on `/one/workspaces/*`). Team invite chrome was later gated (`canInvite` on `TeamPage.tsx`) but the switcher was not. If product confirms “anyone may create another workspace,” downgrade this to honesty/docs and close; the audit’s complaint is the *painted* control, not a failed POST.

