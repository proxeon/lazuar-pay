---
number: "269"
id: B07-I29
severity: P2
status: open
source: plans/009-bugs/07-one-identity-invites-keys.md
head: "297ba98"
---

# 269 — B07-I29 — `HasTenantAccess` ignores archive and role

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/07-one-identity-invites-keys.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B07-I29 — P2 — `HasTenantAccess` ignores archive and role

**Where.** `OneQueryService.cs:72–78`.

**What.** Any historical membership reads members/invites/audit. VIEWER included (intended). Archived included (not intended if archive means leave).

## Evaluation (current tree, 2026-08-18)

### What the bug is
`HasTenantAccessAsync` is the membership-only gate on every id-scoped One workspace GET (workspace, members, invites, audit) and on webhook *read*. It is `AnyAsync` on `(GlobalUserId, OrganizationId)` with `IgnoreQueryFilters`. It does not join `Organizations.IsActive` and does not look at `TenantMembership.Role`. VIEWER passing that predicate is the product intent for “staff can read the roster.” Archived-org access is not intent if archive means leave: a leftover membership on an `IsActive = false` org still unlocks members, pending invite emails, and audit metadata. Issue 119 (`fix/119-archive-revoke`) changed the *command* to drop memberships, revoke keys, and revoke pending invites when an ADMIN archives, so the original “I archived, I can still list members” walk is blocked on the happy path. The predicate itself is unchanged, so any membership that survives (failed archive, raw SQL, future `IsActive = false` without `RemoveTenantMembership`) still reads the tenant.

### Still present?
**PARTIAL**

The method is still membership-only:

```72:78:apps/lazuar-api/Modules/One/Infrastructure/Services/OneQueryService.cs
    public async Task<bool> HasTenantAccessAsync(Guid globalUserId, Guid tenantId)
    {
        return await _context.TenantMemberships
            .AsNoTracking()
            .IgnoreQueryFilters()
            .AnyAsync(m => m.GlobalUserId == globalUserId && m.OrganizationId == tenantId);
    }
```

Contrast public branding, which *does* filter `o.IsActive` (`OneQueryService.cs:46–49`). `GetWorkspaceByIdAsync` (`:26–30`) does not. Archive now removes every membership before save (`ArchiveWorkspaceCommand.cs:56–57`) and is locked by `ArchiveWorkspaceCommandHandlerTests.Archive_RevokesKeys_DropsMemberships_AndPublishesTenantInactive`. After that command, `HasTenantAccessAsync` returns false because the row is gone, not because it consulted `IsActive`. Role is still ignored; VIEWER is still intended (`WorkspaceStaffRoles.cs:9`, Team copy). Webhook *manage* uses `GetTenantRoleAsync` and requires ADMIN/SUPER_ADMIN (`WebhookEndpoints.cs:299–302`); webhook *read* still uses `HasTenantAccessAsync` (`:305–306`).

### Related files
- `apps/lazuar-api/Modules/One/Infrastructure/Services/OneQueryService.cs` — predicate to change.
- `apps/lazuar-api/Modules/One/Infrastructure/Endpoints/WorkspaceEndpoints.cs` — every `{id}` GET/PUT/DELETE after `:32` trusts this bool.
- `apps/lazuar-api/Modules/One/Infrastructure/Endpoints/WebhookEndpoints.cs` — read vs manage split.
- `apps/lazuar-api/Modules/One/Application/Commands/ArchiveWorkspaceCommand.cs` — 119 lifecycle; do not undo membership drop.
- `apps/lazuar-api/Modules/One/Contracts/IOneQueryService.cs` — interface contract.
- `apps/lazuar-api/tests/Lazuar.ArchitectureTests/TenantIsolationArchitectureTests.cs` — `WorkspaceEndpoints_Id_Scoped_Maps_Check_HasTenantAccess` only scrapes that the call exists.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/One/ArchiveWorkspaceCommandHandlerTests.cs` — archive side effects, not `HasTenantAccess`.
- `apps/lazuar-api/Modules/One/Infrastructure/Endpoints/WorkspaceEndpoints.cs` `GET /me/entitlements` (`:175–176`) — still joins memberships with no `o.IsActive` filter (harmless after 119 if rows are deleted).

### Tests
- Existing: `TenantIsolationArchitectureTests.WorkspaceEndpoints_Id_Scoped_Maps_Check_HasTenantAccess` (string scrape); `AuditRecorderTests.ForeignOrg_GetAudit_Forbidden` (stubs `HasTenantAccessAsync` false); `ArchiveWorkspaceCommandHandlerTests.Archive_RevokesKeys_DropsMemberships_AndPublishesTenantInactive`.
- None fail if `HasTenantAccessAsync` still ignores `IsActive`. The architecture test would still pass if the body stayed `return true`.
- First regression: seed membership + `Organization.IsActive = false` without going through archive; `HasTenantAccessAsync` must be false. Second: VIEWER on an *active* org must remain true (do not “fix” VIEWER). Third: after `ArchiveWorkspaceCommand`, access is false (already implied by membership delete).

### Reproduction today
Arrange: two users, org A active with VIEWER membership. Act: `GET /api/v1/one/workspaces/{A}/members` as VIEWER. Assert: 200 (intended). Arrange: flip `one.Organizations.IsActive` to false **without** deleting `TenantMemberships`. Act: same GET. Assert today: 200 members/invites; `GET .../audit` 200. After a real `DELETE /one/workspaces/{A}` (archive command): membership gone, GET members is 401 (issue 270’s status), not a data leak. That second walk is what 119 closed; the first walk is still open.

### Blast radius
Historical staff on a workspace someone thought was gone: roster emails, pending invite addresses, API-key hints on audit. Not a cross-tenant IDOR (you still need a membership row). VIEWER reads are by design. Frequency: rare after 119 unless ops bypasses the command or archive fails mid-transaction.

### Suggested fix
Tighten `HasTenantAccessAsync` to `AnyAsync` membership **join** `Organizations` where `o.IsActive` (still `IgnoreQueryFilters` so empty ambient tenant works). Leave role out of this helper; keep VIEWER on GET members/invites/audit. Do not change `CanManageMembers` / `OrgAdmin` write gates. Do not re-introduce membership-on-archive; 119 stays. No TypeSpec, no Stripe, no Wave 5.

### Evaluation notes
Still P2. Residual of 119 (resolved): predicate honesty, not the product archive loop. Do not merge into 119 or 270 (401 vs 403). VIEWER-can-read is **not** a bug; a “fix” that requires ADMIN for GET members would break Team. After 161–200 this is leftover hygiene.

