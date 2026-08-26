---
number: "318"
id: B09-U50
severity: P2
status: open
source: plans/009-bugs/09-frontends-ops-portal-admin.md
head: "297ba98"
---

# 318 — B09-U50 — No pending invites UI

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/09-frontends-ops-portal-admin.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

#### B09-U50 — No pending invites UI (P2)

Team page invalidates members after invite, not invites.

## Evaluation (current tree, 2026-08-18)

### What the bug is
Workspace Team (`/workspace/team`) can send an invite (`POST /one/workspaces/{id}/invites`) and then only refreshes the **members** list. Pending invitations never appear. There is no query for `GET /one/workspaces/{id}/invites`, no revoke button for `DELETE /one/workspaces/{id}/invites/{inviteId}`, and no “waiting / expired” row. After a successful invite the new person is not a member yet, so the table looks unchanged except for a toast. An Admin who fat-fingers a role or email cannot see or cancel the outstanding token from the UI. The API already returns invitation rows (`Id`, `Email`, `Role`, `Status`, `Expires_at`).

### Still present?
**STILL BROKEN**

`TeamPage` still has a single members query and invalidates only that key:

```19:42:apps/lazuar-ops/src/modules/workspace/pages/TeamPage.tsx
  const { data: members, isLoading } = useQuery({
    queryKey: ["workspace-members", activeWorkspaceId],
    queryFn: async () => {
      const { data, error } = await client.GET("/one/workspaces/{id}/members", {
        params: { path: { id: activeWorkspaceId } },
      });
      if (error) throw new Error(error.detail);
      return data as WorkspaceMemberDto[];
    },
    enabled: !!activeWorkspaceId,
  });

  const inviteMutation = useMutation({
    ...
    onSuccess: () => {
      toast.success("Invitation sent");
      setEmail("");
      queryClient.invalidateQueries({ queryKey: ["workspace-members", activeWorkspaceId] });
    },
```

There is no `client.GET("/one/workspaces/{id}/invites")` in any ops/admin/portal TSX. Grep of the three apps for pending-invite UI is empty. The API list + revoke endpoints are live:

```106:123:apps/lazuar-api/Modules/One/Infrastructure/Endpoints/WorkspaceEndpoints.cs
        group.MapGet("/workspaces/{id:guid}/invites", async Task<Results<Ok<ICollection<WorkspaceInvitationDto>>, UnauthorizedHttpResult>> (Guid id, IExecutionContextAccessor ctx, IOneQueryService queryService) =>
        {
            ...
            var invites = await queryService.GetWorkspaceInvitationsAsync(id);
            var dtos = invites.Select(i => new WorkspaceInvitationDto { Id = i.Id.ToString(), Email = i.Email, Role = i.Role, Status = i.Status, Expires_at = new DateTimeOffset(i.ExpiresAt) }).ToList();
            return TypedResults.Ok((ICollection<WorkspaceInvitationDto>)dtos);
        }).RequireAuthorization();

        group.MapDelete("/workspaces/{id:guid}/invites/{inviteId:guid}", ...
        }).RequireAuthorization("OrgAdmin");
```

`OneQueryService.GetWorkspaceInvitationsAsync` (`OneQueryService.cs:125–133`) returns all invitation rows for the org, ordered by `CreatedAt` desc. Accept still works via `AcceptInvitePage` + `POST /one/workspaces/invites/accept`. Invite form is now hidden for non-admins (`TeamPage.tsx:14,68` — later than the audit; U25-style), but that does not add a pending list.

### Related files
- `apps/lazuar-ops/src/modules/workspace/pages/TeamPage.tsx` — invite + members only.
- `apps/lazuar-ops/src/modules/workspace/pages/AcceptInvitePage.tsx` — accept path; not a list.
- `apps/lazuar-api/Modules/One/Infrastructure/Endpoints/WorkspaceEndpoints.cs` — GET list + DELETE revoke + POST invite.
- `apps/lazuar-api/Modules/One/Infrastructure/Services/OneQueryService.cs` — `GetWorkspaceInvitationsAsync`.
- `apps/lazuar-api/Modules/One/Application/Commands/InviteUserToWorkspaceCommand.cs` (handler tests below) — pending-already-exists, role allow-list.
- `packages/api-types-ts/src/index.ts` — `GET /one/workspaces/{id}/invites` and `One.WorkspaceInvitationDto` already exist; no TypeSpec regen needed to paint the table.
- `issues/114-p1-b07-i04-pending-invite-index-is-not-unique.md` — unique pending index (resolved); not a UI.

### Tests
- Existing API: `InviteUserToWorkspaceCommandHandlerTests.Invite_PendingAlreadyExists_Throws`, `Invite_Member_StoresUppercaseMember`, `Member_CannotInvite`, `Invite_RecordsAuditWithoutSecrets`. `TenantIsolationArchitectureTests` only asserts the POST invites map exists.
- No test calls `GetWorkspaceInvitationsAsync`. No ops test that the Team page shows a pending row after invite. A green CI today does **not** fail this bug.
- First regression test: after `POST .../invites`, `GET .../invites` includes `{ email, role, status: pending }`; after revoke it is gone. UI: Team page, invite `staff@example.com` as VIEWER, assert a “Pending” row with that email appears without waiting for accept (invalidate `["workspace-invites", id]`).

### Reproduction today
Arrange: OrgAdmin on a workspace, Team page. Act: invite a never-seen email as MEMBER. Assert: toast “Invitation sent”; the members table does **not** gain a row; DevTools Network has no `GET /one/workspaces/{id}/invites`; `invalidateQueries` only hits `workspace-members`. Then `curl`/DevTools `GET /api/v1/one/workspaces/{id}/invites` (same cookie + `X-Tenant-Id`) — the invitation is there. There is still no revoke control in the UI. Invited user can only accept via email/ops `/accept-invite?token=`.

### Blast radius
Admins cannot audit or retract an outstanding invite. Wrong email / wrong role sits until expiry (or unique-pending conflict on retry — 114). Not money. PII: invitee email is stored but not shown back to the Admin who just typed it. Frequency: every staff invite. Viewers no longer see Invite (good); they also cannot see pending (API GET is any member with tenant access). Still P2.

### Suggested fix
On `TeamPage`, add `useQuery` `["workspace-invites", activeWorkspaceId]` → `GET /one/workspaces/{id}/invites`. Render a second list (or mixed table) for non-accepted rows: email, role, status, expiry. Invalidate that key in `inviteMutation.onSuccess`. OrgAdmin-only revoke button → `DELETE .../invites/{inviteId}`. Filter to `Status === "PENDING"` if the query returns historical rows. Do not invent a new endpoint. Do not remount WhatsApp. No TypeSpec regen.

### Evaluation notes
Not a duplicate of 114 (index) or 113/115/176 (accept semantics). Related honesty: Team description still says “Viewers can only read” (`TeamPage.tsx:64`) while 317 still lets a Viewer create another workspace. Severity still P2 — missing chrome, API already complete.

