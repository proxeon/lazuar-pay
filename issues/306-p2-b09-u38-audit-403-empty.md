---
number: "306"
id: B09-U38
severity: P2
status: open
source: plans/009-bugs/09-frontends-ops-portal-admin.md
head: "297ba98"
---

# 306 — B09-U38 — Audit 403 → empty

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/09-frontends-ops-portal-admin.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

#### B09-U38 — Audit 403 → empty (P2)

Latent. Today Viewer can read. If policy tightens, Admins will think nothing happened.

## Evaluation (current tree, 2026-08-18)

### What the bug is
`AuditLogPage` treats HTTP 403 as a successful empty list. The table then prints “No audit events yet.” The audit was explicit that this is *latent*: today’s API allows any workspace member (Viewer included) to `GET /one/workspaces/{id}/audit` after `HasTenantAccessAsync`. If that policy is later raised to OrgAdmin (or a Member/Viewer hits a 403 for any other reason), an Admin-looking empty table will be indistinguishable from “this workspace has never logged anything.” `metadata_json` is still fetched and never rendered. Role chrome from 143 exists on the outlet context; this page does not use it.

### Still present?
**STILL BROKEN**

The swallow is unchanged:

```22:31:apps/lazuar-ops/src/modules/workspace/pages/AuditLogPage.tsx
  const { data, isLoading } = useQuery({
    queryKey: ["workspace-audit", activeWorkspaceId, page],
    queryFn: async () => {
      const res = await fetch(
        `${API_URL}/one/workspaces/${activeWorkspaceId}/audit?page=${page}&limit=50`,
        { credentials: "include", headers: { "X-Tenant-Id": activeWorkspaceId } },
      );
      if (res.status === 403) return { data: [] as AuditEvent[], total_count: 0, total_pages: 1 };
      if (!res.ok) throw new Error("Failed to load audit log");
      return (await res.json()) as { data: AuditEvent[]; total_count: number; total_pages: number };
```

```72:76:apps/lazuar-ops/src/modules/workspace/pages/AuditLogPage.tsx
            {!isLoading && (data?.data.length ?? 0) === 0 && (
              <tr>
                <td colSpan={4} className="px-4 py-8 text-center text-[12px] text-[#71717a]">
                  No audit events yet.
```

API is still membership-any, not OrgAdmin:

```180:190:apps/lazuar-api/Modules/One/Infrastructure/Endpoints/WorkspaceEndpoints.cs
        group.MapGet("/workspaces/{id:guid}/audit", async Task<Results<Ok<PaginatedResponse<AuditEventDto>>, UnauthorizedHttpResult, ForbidHttpResult>> (
            Guid id,
            [FromQuery] int page,
            [FromQuery] int limit,
            IExecutionContextAccessor ctx,
            IOneQueryService queryService,
            OneDbContext db) =>
        {
            if (ctx.UserId == Guid.Empty) return TypedResults.Unauthorized();
            var hasAccess = await queryService.HasTenantAccessAsync(ctx.UserId, id);
            if (!hasAccess && !ctx.IsSystemAdmin) return TypedResults.Forbid();
```

`HasTenantAccessAsync` is “any `TenantMembership` row” (`OneQueryService.cs` 72–78). TypeSpec `getWorkspaceAudit` is `@useAuth(BearerAuth)` only (`packages/api-spec/modules/one/routes.tsp` 152–159).

### Related files
- `apps/lazuar-ops/src/modules/workspace/pages/AuditLogPage.tsx` — 403 → empty + unused `metadata_json`.
- `apps/lazuar-api/Modules/One/Infrastructure/Endpoints/WorkspaceEndpoints.cs` — audit GET policy.
- `apps/lazuar-api/Modules/One/Infrastructure/Services/OneQueryService.cs` — membership check, no role.
- `packages/api-spec/modules/one/routes.tsp` — contract has no OrgAdmin.
- `apps/lazuar-ops/src/App.tsx` `OpsOutletContext.role` — available, unused here (143).
- Contrast: `apps/lazuar-ops/src/modules/commerce/pages/DashboardPage.tsx` 31, 50, 61 — later 403 handling returns `{ forbidden: true }` instead of a fake zero list.

### Tests
- Existing tests that touch this path: none. Grep of `apps/lazuar-api/tests` for workspace audit GET / `getWorkspaceAudit` returned no handler tests. Ops has no page test.
- Whether any test would fail if the bug is still there: **No.**
- What a first regression test should assert: a mocked 403 does not produce `data: []`; the page shows “You do not have access to the audit log” (or equivalent) and does not show “No audit events yet.” A 200 with `[]` still shows the empty-history sentence. Do not change TypeSpec unless you also change the real policy.

### Reproduction today
Arrange a Viewer (or Admin) in a workspace that has invite/remove events. Open Workspace → Audit log. Assert: rows load (Viewer can read today). To see the latent lie, intercept `GET /one/workspaces/{id}/audit` and return 403. Assert: table says “No audit events yet.” with no forbidden banner.

### Blast radius
Honesty / ops, not money. Today almost nobody hits the 403 path from a real membership. The danger is a future policy tighten (or a Superadmin/workspace mismatch) silently erasing the log. Audit rows include actor email and entity ids (PII-adjacent). Frequency: every load if policy ever returns 403.

### Suggested fix
Stop mapping 403 to an empty payload. Return a `{ forbidden: true }` sentinel (copy Dashboard 144) or let the query throw and render `isError`. Keep Viewer-readable if that remains product policy; if product wants Admin-only, change the *API* to `RequireAuthorization("OrgAdmin")` and paint a role-gated empty state — do not leave the swallow in place. Showing `metadata_json` is a separate nicety, not required to close U38. No TypeSpec regen unless the authorization contract changes.

### Evaluation notes
008 and 009 both marked this OPEN (latent). 143 added role chrome elsewhere; this page still ignores `role`. Severity still P2 — latent, not a current Admin outage. Not blocked. Do not “fix” it by hiding the nav from Viewer while still swallowing 403.

