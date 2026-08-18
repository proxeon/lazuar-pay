---
number: "270"
id: B07-I30
severity: P2
status: open
source: plans/009-bugs/07-one-identity-invites-keys.md
head: "297ba98"
---

# 270 — B07-I30 — GET members/workspace use 401 for IDOR; audit uses 403

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/07-one-identity-invites-keys.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B07-I30 — P2 — GET members/workspace use 401 for IDOR; audit uses 403

**Where.** `WorkspaceEndpoints.cs:33, 87, 103` vs `:177`.

**What.** Same predicate, two statuses. Clients that treat 401 as “login again” will bounce a VIEWER who typed the wrong GUID into a logout loop.

## Evaluation (current tree, 2026-08-18)

### What the bug is
Every id-scoped One workspace route uses the same `HasTenantAccessAsync || IsSystemAdmin` check, then splits the miss status. `GET /workspaces/{id}`, `GET .../members`, `GET .../invites` (and PUT/DELETE workspace, invite, remove) return `TypedResults.Unauthorized()` (HTTP 401). `GET /workspaces/{id}/audit` returns `TypedResults.Forbid()` (HTTP 403). 401 means “this session is not authenticated”; 403 means “this session is authenticated and not allowed.” A client that maps any 401 to “cookie dead, go to login” will treat a typo’d GUID or a VIEWER hitting the wrong workspace as a logout. Ops itself only session-probes `/one/auth/me` (`App.tsx:83–86`); Team’s members GET throws the problem `detail` into react-query rather than logging out. Audit UI already special-cases 403 as an empty table (`AuditLogPage.tsx:29`). The API contract is still two codes for one predicate.

### Still present?
**STILL BROKEN**

Workspace GET still 401s a member-miss (`WorkspaceEndpoints.cs:31–33`):

```31:33:apps/lazuar-api/Modules/One/Infrastructure/Endpoints/WorkspaceEndpoints.cs
            if (ctx.UserId == Guid.Empty) return TypedResults.Unauthorized();
            var hasAccess = await queryService.HasTenantAccessAsync(ctx.UserId, id);
            if (!hasAccess && !ctx.IsSystemAdmin) return TypedResults.Unauthorized();
```

Members (`:89–91`), invites (`:108–110`), invite POST (`:99–101`), revoke (`:118–120`) are the same 401. Audit is the outlier (`:188–190`):

```188:190:apps/lazuar-api/Modules/One/Infrastructure/Endpoints/WorkspaceEndpoints.cs
            if (ctx.UserId == Guid.Empty) return TypedResults.Unauthorized();
            var hasAccess = await queryService.HasTenantAccessAsync(ctx.UserId, id);
            if (!hasAccess && !ctx.IsSystemAdmin) return TypedResults.Forbid();
```

Empty `UserId` → 401 is correct on all of them. Authenticated miss should match audit. `AuditRecorderTests.ForeignOrg_GetAudit_Forbidden` re-implements the 403 branch in a local lambda (`:99–101`) and never hits members/workspace.

### Related files
- `apps/lazuar-api/Modules/One/Infrastructure/Endpoints/WorkspaceEndpoints.cs` — every `Unauthorized()` after a failed `HasTenantAccessAsync`.
- `apps/lazuar-ops/src/modules/workspace/pages/AuditLogPage.tsx` — already treats 403 as empty; keep that if audit stays 403.
- `apps/lazuar-ops/src/modules/workspace/pages/TeamPage.tsx` — members GET; today surfaces `error.detail`, does not logout.
- `apps/lazuar-ops/src/App.tsx` — only `/one/auth/me` failure navigates to `/login`.
- `apps/lazuar-ops/src/lib/api-client.ts` — no global 401 interceptor (so the SPA is safer than a generic client).
- `apps/lazuar-api/tests/Lazuar.ModuleTests/One/AuditRecorderTests.cs` — `ForeignOrg_GetAudit_Forbidden`.
- `apps/lazuar-api/tests/Lazuar.ArchitectureTests/TenantIsolationArchitectureTests.cs` — does not assert status codes.

### Tests
- Existing: `AuditRecorderTests.ForeignOrg_GetAudit_Forbidden` (403 on the inlined audit helper); `WorkspaceCreateAuthorizationTests.Post_Workspaces_Requires_Authorization` (source scrape of `Unauthorized` on empty `UserId`).
- No test would fail if members kept returning 401 for an authenticated non-member. No HTTP test hits both routes with the same principal.
- First regression: authenticated user B, org A they do not belong to: `GET /one/workspaces/{A}`, `GET .../members`, `GET .../invites`, `GET .../audit` are all **403**; unauthenticated is still **401**. Do not weaken the membership check.

### Reproduction today
Arrange: two workspaces, cookie for a member of A only. Act: `GET /api/v1/one/workspaces/{B}/members` and `GET /api/v1/one/workspaces/{B}/audit` with the same cookie and `X-Tenant-Id: A` (path B). Assert: members 401, audit 403. Act: omit cookie. Assert: both 401. In ops, Audit log on a forbidden id shows an empty table; Team on a crafted id shows a query error, not login — unless a future interceptor treats 401 as session death.

### Blast radius
DX and session UX, not a data steal (the miss is fail-closed). Integrators and Scalar “try it” with a guessed GUID look logged-out. Frequency: every wrong id; low in the shipped SPA because the switcher only lists entitlements. Confusion with 269 (access predicate) and 117 (stamp 401 on `/auth/me` only).

### Suggested fix
Change every authenticated-but-no-membership branch on `WorkspaceEndpoints` from `TypedResults.Unauthorized()` to `TypedResults.Forbid()`, keeping `UserId == Guid.Empty` as 401. Widen the audit test (or add a small endpoint test) so members/workspace/invites share that 403. Leave TypeSpec alone (those routes already union `ProblemDetailsResponse`). Do not add a global ops 401→logout interceptor while this split exists.

### Evaluation notes
Still P2. Same predicate as 269; do not fold them — 269 is *who* passes, 270 is the status. Not fixed in 161–200. `AuditLogPage` 403 empty-list is intentional UX, not this bug.

