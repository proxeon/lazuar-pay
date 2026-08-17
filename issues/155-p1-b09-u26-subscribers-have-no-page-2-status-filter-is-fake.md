---
number: "155"
id: B09-U26
severity: P1
status: open
source: plans/009-bugs/09-frontends-ops-portal-admin.md
head: "297ba98"
---

# 155 — B09-U26 — Subscribers have no page 2; status filter is fake

- **Severity:** P1
- **Status:** open
- **Source:** `plans/009-bugs/09-frontends-ops-portal-admin.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

#### B09-U26 — Subscribers have no page 2; status filter is fake (P1)

**Where:** `SubscribersPage.tsx` 22, 53–60, 299, 337–348. Transactions and quotes have Prev/Next. Subscribers do not.  
**Walk:** 51 ACTIVE + 1 PAST_DUE on page 2. Filter PAST DUE on page 1 → “No subscribers found.”

