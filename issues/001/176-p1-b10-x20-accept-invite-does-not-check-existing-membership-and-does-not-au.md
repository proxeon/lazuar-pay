---
number: "176"
id: B10-X20
severity: P1
status: resolved
resolved_branch: fix/176-accept-invite-membership
source: plans/009-bugs/10-tenancy-workers-contracts-tests.md
head: "297ba98"
---

# 176 — B10-X20 — Accept-invite does not check existing membership and does not audit

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/10-tenancy-workers-contracts-tests.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/176-accept-invite-membership`

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B10-X20 — P1 — Accept-invite does not check existing membership and does not audit

```36:41:apps/lazuar-api/Modules/One/Application/Commands/AcceptWorkspaceInvitationCommand.cs
        invitation.Accept();

        var membership = new TenantMembership(user.Id, invitation.OrganizationId, invitation.Role);
        _repository.AddTenantMembership(membership);

        await _repository.SaveChangesAsync(ct);
```

`AcceptWorkspaceInvitationCommandHandlerTests` covers happy / expired / wrong email. It does **not** cover: already a member, second accept of a still-PENDING row (status check should stop this), inactive user, bad token, duplicate unique (org, user) if one exists.

Invite create writes `AuditEvent`. Accept does not (`IAuditRecorder` is not even a constructor parameter).

`297ba98` added the ops `/accept-invite` page and `OneLinkService.GetOpsBaseUrl`. The handler hole remains.

