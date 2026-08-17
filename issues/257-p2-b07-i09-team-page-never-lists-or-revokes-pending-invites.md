---
number: "257"
id: B07-I09
severity: P2
status: open
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

