---
number: "257"
id: B07-I09
severity: P2
status: resolved
resolved_branch: fix/257-team-pending-invites
source: plans/009-bugs/07-one-identity-invites-keys.md
head: "297ba98"
---

# 257 — B07-I09 — Team page never lists or revokes pending invites

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/07-one-identity-invites-keys.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B07-I09 — P2 — Team page never lists or revokes pending invites

**Where.** `TeamPage.tsx:17–43` invalidates `workspace-members` only. GET `/one/workspaces/{id}/invites` and DELETE `.../invites/{inviteId}` exist (`WorkspaceEndpoints.cs:99–113`).

**What.** Admin cannot see that an invite is PENDING, expired, or doubled. They cannot revoke from the UI. LP-166’s “Team page is the only staff UX” is still a roster widget plus a form that 403s for VIEWER (who still see the form).

## Evaluation (current tree, 2026-08-18)

### What the bug is
Staff invitations are a first-class One API (create, list, revoke, accept) but the only merchant UX is `TeamPage`, which loads `GET /one/workspaces/{id}/members` and invalidates only `workspace-members`. After a successful invite the page toasts “Invitation sent” and refreshes the member roster — which does not include PENDING rows — so the admin cannot see that the invite exists, whether it expired, or that a second send would 400 on the pending unique pair. There is no revoke control. LP-166 still treats this page as the only staff surface, so pending invites are operationally invisible until the person accepts (or the token ages out).

### Still present?
**STILL BROKEN**

The page still has no invites query and no DELETE mutation. Invite success still invalidates members only:

```31:43:apps/lazuar-ops/src/modules/workspace/pages/TeamPage.tsx
  const inviteMutation = useMutation({
    mutationFn: async () => {
      const { error } = await client.POST("/one/workspaces/{id}/invites", {
        params: { path: { id: activeWorkspaceId } },
        body: { email: email.trim().toLowerCase(), role },
      });
      if (error) throw new Error(error.detail);
    },
    onSuccess: () => {
      toast.success("Invitation sent");
      setEmail("");
      queryClient.invalidateQueries({ queryKey: ["workspace-members", activeWorkspaceId] });
    },
```

The APIs the audit cited still exist and return status/expiry:

```106:123:apps/lazuar-api/Modules/One/Infrastructure/Endpoints/WorkspaceEndpoints.cs
        group.MapGet("/workspaces/{id:guid}/invites", async Task<Results<Ok<ICollection<WorkspaceInvitationDto>>, UnauthorizedHttpResult>> (Guid id, IExecutionContextAccessor ctx, IOneQueryService queryService) =>
        {
            // ...
            var invites = await queryService.GetWorkspaceInvitationsAsync(id);
            var dtos = invites.Select(i => new WorkspaceInvitationDto { Id = i.Id.ToString(), Email = i.Email, Role = i.Role, Status = i.Status, Expires_at = new DateTimeOffset(i.ExpiresAt) }).ToList();
```

`GetWorkspaceInvitationsAsync` returns all statuses (`OneQueryService.cs:125–133`). `RevokeWorkspaceInvitationCommand` revokes PENDING only (`RevokeWorkspaceInvitationCommand.cs:38–43`). Secondary audit complaint (VIEWER still sees the form, which 403s) was fixed by **154** (`fix/154-role-gated-buttons`): `canInvite` hides the form (`TeamPage.tsx:14, 68–102`). That does not list or revoke invites.

### Related files
- `apps/lazuar-ops/src/modules/workspace/pages/TeamPage.tsx` — the only staff invite UX; members query only.
- `apps/lazuar-api/Modules/One/Infrastructure/Endpoints/WorkspaceEndpoints.cs:97–123` — POST/GET/DELETE invites.
- `apps/lazuar-api/Modules/One/Infrastructure/Services/OneQueryService.cs:125–133` — list payload (id, email, role, status, expires).
- `apps/lazuar-api/Modules/One/Application/Commands/InviteUserToWorkspaceCommand.cs` — create; rejects a second PENDING for the same email.
- `apps/lazuar-api/Modules/One/Application/Commands/RevokeWorkspaceInvitationCommand.cs` — revoke without audit (B07-I28 / issue 268).
- `apps/lazuar-api/tests/Lazuar.ModuleTests/One/InviteUserToWorkspaceCommandHandlerTests.cs` — create/role/pending-exists; no list UI.
- `issues/154-p1-b09-u25-anonymize-invite-save-vault-painted-for-roles-that-403.md` — form gating only.

### Tests
- Existing: `Invite_Member_StoresUppercaseMember`, `Invite_DisallowedRole_Throws` (`CLIENT` included), `Member_CannotInvite`, `Invite_PendingAlreadyExists_Throws`, `Invite_RecordsAuditWithoutSecrets`.
- None would fail if Team page never called GET/DELETE invites. No ops/RTL test (issue **325**). No API test named for `GetWorkspaceInvitationsAsync`.
- First regression: Team page (or a thin hook test) asserts that after invite success it fetches `/one/workspaces/{id}/invites` and renders the email + `PENDING`; clicking Revoke calls `DELETE .../invites/{inviteId}` and the row disappears. VIEWER still must not see Invite/Revoke.

### Reproduction today
Arrange: ADMIN in a workspace, invite `book@example.com` as MEMBER. Act: stay on `/workspace/team`. Assert: toast “Invitation sent”; the new email is **not** in the member list; there is no PENDING row and no revoke button. `GET /api/v1/one/workspaces/{id}/invites` with the same cookie + `X-Tenant-Id` does return the PENDING row. Send the same email again → API 400 pending invitation; UI only shows the toast error. Expire or wait: UI still cannot tell.

### Blast radius
Workspace admins and invitees. Not PII theft (emails are already on the form). Ops cost: cannot cancel a mis-typed email before accept; cannot see doubled/expired invites; “Team is the only staff UX” is still a roster. Happens on every invite until someone accepts.

### Suggested fix
On Team page, `useQuery` `GET /one/workspaces/{id}/invites` keyed with the workspace id; render PENDING/expired/revoked with expiry; ADMIN-only Revoke calling the existing DELETE. Invalidate that query (not only `workspace-members`) on invite/revoke. Keep `canInvite`. Do not invent resend here (that is **268** / B07-I28). No TypeSpec regen — the list DTO already exists.

### Evaluation notes
Still P2: API is complete, UI is not. **154** closed the VIEWER-sees-form slice. **114** pending unique index makes a second send fail closed — which is worse UX without a list. **268** (no resend, revoke has no audit) is adjacent; do not block 257 on 268, but add audit if you touch revoke. Not a 161–200 fail-closed leftover.

## Resolution

Team page lists invitations (email, role, status, expiry). ADMIN/SUPER_ADMIN can revoke PENDING. Invite success invalidates the invites query. No resend.

