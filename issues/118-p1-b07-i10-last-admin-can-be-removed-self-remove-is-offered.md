---
number: "118"
id: B07-I10
severity: P1
status: open
source: plans/009-bugs/07-one-identity-invites-keys.md
head: "297ba98"
---

# 118 — B07-I10 — Last admin can be removed; self-remove is offered

- **Severity:** P1
- **Status:** open
- **Source:** `plans/009-bugs/07-one-identity-invites-keys.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B07-I10 — P1 — Last admin can be removed; self-remove is offered

**Where.** `RemoveWorkspaceMemberCommand.cs:33–43`; `TeamPage.tsx:114–122`.

**What.** Orphaned workspace. Keys keep working. No owner transfer. EmptyWorkspaceState offers create-new, not recover.

