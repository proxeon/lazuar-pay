---
number: "122"
id: B07-I19
severity: P1
status: resolved
resolved_branch: fix/122-exception-message-500
source: plans/009-bugs/07-one-identity-invites-keys.md
head: "297ba98"
---

# 122 — B07-I19 — `GlobalExceptionHandler` puts `exception.Message` on 500s

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/07-one-identity-invites-keys.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/122-exception-message-500`

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B07-I19 — P1 — `GlobalExceptionHandler` puts `exception.Message` on 500s

**Where.** `GlobalExceptionHandler.cs:54–62`.

**What.** Unique-index failures (accept, slug, email) leak provider text. Combined with B07-I03 this is how a bookkeeper sees a Postgres constraint in the accept page **if** the SPA did not overwrite it — and how a raw client always sees it.

`InvalidOperationException` → 400 with the domain string is intentional and is how accept “wrong email” reaches the SPA (`:40–50`).

