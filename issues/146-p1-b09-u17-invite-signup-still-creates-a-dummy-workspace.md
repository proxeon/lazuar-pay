---
number: "146"
id: B09-U17
severity: P1
status: resolved
resolved_branch: fix/146-invite-signup-no-dummy
source: plans/009-bugs/09-frontends-ops-portal-admin.md
head: "297ba98"
---

# 146 — B09-U17 — Invite signup still creates a dummy workspace

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/09-frontends-ops-portal-admin.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/146-invite-signup-no-dummy`

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

#### B09-U17 — Invite signup still creates a dummy workspace (P1)

**Where:** `LoginPage.tsx` 112–126, 208–210; `EmptyWorkspaceState.tsx` 15–18.  
ReturnUrl is preserved (closed). The register contract still requires a workspace. Invitee becomes ADMIN of a junk tenant plus MEMBER/VIEWER of the real one. Empty state has no invite-token field if they land without returnUrl (auth throw path).

