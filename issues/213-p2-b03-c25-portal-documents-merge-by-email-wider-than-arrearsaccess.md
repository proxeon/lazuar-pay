---
number: "213"
id: B03-C25
severity: P2
status: resolved
resolved_branch: fix/213-portal-docs-profile-scope
source: plans/009-bugs/03-commerce-dunning-arrears-portal.md
head: "297ba98"
---

# 213 — B03-C25 — Portal documents merge by email, wider than ArrearsAccess

- **Severity:** P2
- **Status:** resolved
- **Source:** `plans/009-bugs/03-commerce-dunning-arrears-portal.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/213-portal-docs-profile-scope`

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B03-C25 — P2 — Portal documents merge by email, wider than ArrearsAccess

`PortalDocumentQueryService.cs` 57–77. Two CRM profiles, one inbox, one org: one token lists both document sets. Sibling rule on money verbs is tighter.

---

