---
number: "115"
id: B07-I05
severity: P1
status: resolved
resolved_branch: fix/115-accept-audit
source: plans/009-bugs/07-one-identity-invites-keys.md
head: "297ba98"
---

# 115 — B07-I05 — Accept does not pre-check membership and writes no audit

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/07-one-identity-invites-keys.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/115-accept-audit`

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B07-I05 — P1 — Accept does not pre-check membership and writes no audit

**Where.** `AcceptWorkspaceInvitationCommand.cs` has no `HasMembershipAsync` and no `IAuditRecorder`. Invite and remove do.

**What.** The unique index is the only guard (500). LP-167’s identity story cannot answer “when did this email join.” Viewer reading `/audit` never sees `member.accepted`.

