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

