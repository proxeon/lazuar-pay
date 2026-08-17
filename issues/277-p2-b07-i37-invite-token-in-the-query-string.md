---
number: "277"
id: B07-I37
severity: P2
status: open
source: plans/009-bugs/07-one-identity-invites-keys.md
head: "297ba98"
---

# 277 — B07-I37 — Invite token in the query string

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/07-one-identity-invites-keys.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B07-I37 — P2 — Invite token in the query string

**Where.** Mail URL; `AcceptInvitePage` reads `searchParams`.

**What.** Server access logs, browser history, Referer if the success page ever loads a third party. Accept is first-party today. Prefer POST-only after a fragment, or a one-time exchange. P2.

