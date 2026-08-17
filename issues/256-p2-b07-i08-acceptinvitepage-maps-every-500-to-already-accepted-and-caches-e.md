---
number: "256"
id: B07-I08
severity: P2
status: open
source: plans/009-bugs/07-one-identity-invites-keys.md
head: "297ba98"
---

# 256 — B07-I08 — AcceptInvitePage maps every 500 to “already accepted” and caches errors

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/07-one-identity-invites-keys.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B07-I08 — P2 — AcceptInvitePage maps every 500 to “already accepted” and caches errors

**Where.** `AcceptInvitePage.tsx:17, 40–46, 64, 175–177`.

**What.** Honest for the unique-index 500. Dishonest for everything else. Module-level `Map` is the right Strict-Mode fix for **in-flight** accepts. Leaving a rejected Promise in the Map means a later visit with the same token in the same JS heap does not retry. Wrong-email Sign out deletes (`:120`). The generic “Sign in” link does not.

Out of scope for 09’s *pixels*; this is control-flow that lies about One’s API.

