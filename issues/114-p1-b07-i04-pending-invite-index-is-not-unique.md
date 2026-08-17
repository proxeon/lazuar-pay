---
number: "114"
id: B07-I04
severity: P1
status: open
source: plans/009-bugs/07-one-identity-invites-keys.md
head: "297ba98"
---

# 114 — B07-I04 — Pending invite index is not unique

- **Severity:** P1
- **Status:** open
- **Source:** `plans/009-bugs/07-one-identity-invites-keys.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B07-I04 — P1 — Pending invite index is not unique

**Where.** `OneDbContext.cs:88`.

**What.** Team double-submit, or invite → fail to notice → invite again. Two tokens in two emails. First accept works. Second is B07-I03. Also two in-flight “you’re invited” mails with different roles: last writer of membership wins only if they were not already a member; otherwise 500. Role is write-once; there is no “upgrade this invite.”

