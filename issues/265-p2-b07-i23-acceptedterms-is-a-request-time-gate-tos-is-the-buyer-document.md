---
number: "265"
id: B07-I23
severity: P2
status: open
source: plans/009-bugs/07-one-identity-invites-keys.md
head: "297ba98"
---

# 265 — B07-I23 — `accepted_terms` is a request-time gate; TOS is the buyer document

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/07-one-identity-invites-keys.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B07-I23 — P2 — `accepted_terms` is a request-time gate; TOS is the buyer document

**Where.** `AuthEndpoints.cs:47–48`; `LoginPage.tsx:9–10, 289–298` links `/portal/legal/terms` and `/privacy`.

**What.** 008 §2.3 still holds. No merchant MSA, no stored version, 99.9% sentence still on the buyer terms. Legal, not a crash.

