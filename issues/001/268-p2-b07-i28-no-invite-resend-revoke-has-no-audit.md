---
number: "268"
id: B07-I28
severity: P2
status: open
source: plans/009-bugs/07-one-identity-invites-keys.md
head: "297ba98"
---

# 268 — B07-I28 — No invite resend; revoke has no audit

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/07-one-identity-invites-keys.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B07-I28 — P2 — No invite resend; revoke has no audit

**Where.** No command. `RevokeWorkspaceInvitationCommand.cs:43–45` sets REVOKED, no recorder.

**What.** Completes the “mail failed, now what?” dead-end with B07-I01.

## Evaluation (current tree, 2026-08-18)

### What the bug is
An admin who invites staff gets one shot at delivery. There is still no `ResendWorkspaceInvitation` command, no rotate-token helper, and Team never lists pending rows, so a bounced, filtered, or expired mail leaves a `PENDING` invite whose plaintext token exists only in the original message. `InviteUserToWorkspaceCommandHandler` now refuses a second pending invite for the same email (`"A pending invitation already exists for this email."`) instead of re-sending, so “invite again” is a 400, not a new mail. `RevokeWorkspaceInvitationCommandHandler` flips the row to `REVOKED` and saves; it never calls `IAuditRecorder`. Invite records `member.invited` and accept (issue 115) now records `member.accepted`, so revoke is the remaining identity write that is invisible on `GET /one/workspaces/{id}/audit`. Combined with B07-I01 this was the “mail failed, now what?” dead-end; 018 later moved invite dispatch onto the system tenant, so mail usually arrives, but there is still no product recovery if it does not.

### Still present?
**STILL BROKEN**

Revoke still has no recorder — it only calls `invitation.Revoke()` then `SaveChangesAsync`:

```43:45:apps/lazuar-api/Modules/One/Application/Commands/RevokeWorkspaceInvitationCommand.cs
        invitation.Revoke();

        await _repository.SaveChangesAsync(ct);
```

The handler constructor takes only `IOneRepository` (`RevokeWorkspaceInvitationCommand.cs:16–21`). Grep of `ResendWorkspaceInvitation` / `ResendInvite` is empty. TypeSpec `packages/api-spec/modules/one/routes.tsp:125–143` has POST/GET invites and DELETE revoke, no resend. Team still only invalidates `workspace-members` and never GETs `/invites` (`apps/lazuar-ops/src/modules/workspace/pages/TeamPage.tsx:19–42`). Invite now short-circuits a second pending row (`InviteUserToWorkspaceCommand.cs:41–43`). Invite mail itself now publishes with `_systemTenantId` (`NotificationDispatchDomainEventHandlers.cs:19, 78–79`); that is 018 (`fix/018-invite-mail-platform-resend`), not a resend/audit fix.

### Related files
- `apps/lazuar-api/Modules/One/Application/Commands/RevokeWorkspaceInvitationCommand.cs` — revoke path that must grow `IAuditRecorder`.
- `apps/lazuar-api/Modules/One/Application/Commands/InviteUserToWorkspaceCommand.cs` — existing pending check is the hook a resend should reuse, not a second insert.
- `apps/lazuar-api/Modules/One/Infrastructure/Repositories/OneRepository.cs` (`GetPendingInvitationAsync` at 112–119) — already loads the row a resend would rotate.
- `apps/lazuar-api/Modules/One/Domain/WorkspaceInvitation.cs` — `Revoke()` at 47–51; no rotate-token method.
- `apps/lazuar-api/Modules/One/Infrastructure/Endpoints/WorkspaceEndpoints.cs` — DELETE `/workspaces/{id}/invites/{inviteId}` at 116–123; no resend map.
- `apps/lazuar-api/Modules/One/Application/EventHandlers/NotificationDispatchDomainEventHandlers.cs` — re-raising `WorkspaceInvitationCreatedDomainEvent` (or a sibling) is how a new mail would go out.
- `apps/lazuar-ops/src/modules/workspace/pages/TeamPage.tsx` — product UX (also issue 257 / B07-I09).
- `apps/lazuar-api/tests/Lazuar.ModuleTests/One/InviteUserToWorkspaceCommandHandlerTests.cs` — invite audit + pending-exists; no revoke/resend cases.

### Tests
- Existing: `InviteUserToWorkspaceCommandHandlerTests.Invite_RecordsAuditWithoutSecrets` (`member.invited` without the token); `Invite_PendingAlreadyExists_Throws`; `AcceptWorkspaceInvitationCommandHandlerTests.Accept_RecordsAuditWithoutToken` (`member.accepted`, 115).
- None would fail today: no `RevokeWorkspaceInvitationCommandHandlerTests`, no assertion that revoke writes `invitation.revoked`, no resend command to miss.
- First regression: revoke a PENDING invite with a real `IAuditRecorder` and assert one `invitation.revoked` (or `member.invite_revoked`) row whose metadata has email/role and not the token; add `Resend_PendingInvite_RotatesHash_AndReDispatches` that keeps a single PENDING row, changes `TokenHash`, and publishes a new accept URL.

### Reproduction today
Arrange: ADMIN cookie, workspace with Email Provider optional (018 uses platform Resend). Act: `POST /api/v1/one/workspaces/{id}/invites` `{ email, role: "VIEWER" }`, discard the mail, then POST the same email again. Assert: second POST is 400 “pending invitation already exists”; `GET .../invites` shows PENDING with no token; Team UI still only shows members. Act: `DELETE /api/v1/one/workspaces/{id}/invites/{inviteId}`. Assert: 200 `revoked`; `GET .../audit` has `member.invited` but no revoke action.

### Blast radius
Staff onboarding only (bookkeepers, viewers). No money movement. PII is the invitee email already on the invite row. Frequency: every failed or expired invite after 018; worse if Resend is down or the address is wrong. Audit gap is compliance (who killed an invite) rather than a steal.

### Suggested fix
Smallest correct change: inject optional `IAuditRecorder` into `RevokeWorkspaceInvitationCommandHandler` and record `invitation.revoked` with `{ email, role }` (mirror invite, no token). Add `ResendWorkspaceInvitationCommand` that loads the PENDING row via `GetPendingInvitationAsync`, mints a new `GenerateSecureToken()`, updates hash/expiry on the same row (new domain method; do not insert a second PENDING), raises the existing created event (or a dedicated resent event) so `NotificationDispatchDomainEventHandlers` mails the new URL on the system tenant, and audits `invitation.resent`. Keep GET/DELETE as-is; do not TypeSpec-regen unless you add a route (a command reused from DELETE+POST is enough). Team listing/resend button is 257, not required to close the command hole. Do not re-open a second pending token (114 already closed that).

### Evaluation notes
Still P2 after 018: mail usually lands, so this is recovery + audit, not the P0 loop. Blocked-by: 257 (Team never lists/revokes) for UX; 018 is done. Sibling of 115 (accept audit, resolved). Do not treat `Invite_PendingAlreadyExists_Throws` as a resend. Residual after 161–200: 018/114/115 landed; this P2 was left.

