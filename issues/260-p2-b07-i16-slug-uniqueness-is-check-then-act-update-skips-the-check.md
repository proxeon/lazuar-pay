---
number: "260"
id: B07-I16
severity: P2
status: open
source: plans/009-bugs/07-one-identity-invites-keys.md
head: "297ba98"
---

# 260 — B07-I16 — Slug uniqueness is check-then-act; update skips the check

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/07-one-identity-invites-keys.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B07-I16 — P2 — Slug uniqueness is check-then-act; update skips the check

**Where.** `RegisterPublicUserCommand.cs:45–49`; `CreateWorkspaceCommand.cs:42–47`; `UpdateWorkspaceCommand.cs:43` (no `IsSlugUniqueAsync`); `OneDbContext.cs:49`.

**What.** Concurrent create → 500 + leaked unique-violation (B07-I19). Update collision → same. Not an IDOR.

