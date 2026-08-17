---
number: "211"
id: B03-C23
severity: P2
status: open
source: plans/009-bugs/03-commerce-dunning-arrears-portal.md
head: "297ba98"
---

# 211 — B03-C23 — Newest-sub token subject ignores status

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/03-commerce-dunning-arrears-portal.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B03-C23 — P2 — Newest-sub token subject ignores status

`GetNewestSubscriptionForClientAsync` (`CommerceRepository.cs` 106–116): no `Status` filter. Newest CANCELED / PENDING is the HMAC subject. Sibling rule still opens ACTIVE rows. Confusing, rarely money.

---

