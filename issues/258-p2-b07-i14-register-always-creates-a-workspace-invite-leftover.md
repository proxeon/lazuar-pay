---
number: "258"
id: B07-I14
severity: P2
status: open
source: plans/009-bugs/07-one-identity-invites-keys.md
head: "297ba98"
---

# 258 — B07-I14 — Register always creates a workspace (invite leftover)

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/07-one-identity-invites-keys.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B07-I14 — P2 — Register always creates a workspace (invite leftover)

**Where.** `RegisterPublicUserCommand.cs:54–62`; `LoginPage.tsx:208–307` (`inviteReturn` only changes the subtitle).

**What.** New invitee who uses Sign up becomes ADMIN of a stray tenant and MEMBER/VIEWER/ADMIN of the invited one. Accept still works (email match). Extra starter-credit / entitlement events fire for W2. Not an accept-breaker. Do not “fix” this by blocking register-from-invite without a join-without-workspace API.

