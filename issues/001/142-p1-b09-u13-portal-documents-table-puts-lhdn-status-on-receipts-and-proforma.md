---
number: "142"
id: B09-U13
severity: P1
status: resolved
resolved_branch: fix/142-portal-doc-lhdn-status
source: plans/009-bugs/09-frontends-ops-portal-admin.md
head: "297ba98"
---

# 142 — B09-U13 — Portal documents table puts LHDN Status on receipts and proformas

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/09-frontends-ops-portal-admin.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/142-portal-doc-lhdn-status`

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

#### B09-U13 — Portal documents table puts LHDN Status on receipts and proformas (P1)

**Where:** `portal/page.tsx` 215, 226.  
**Walk:** Official Receipt row, Status column shows `B2C_RECEIPT` or `—` next to a tax-looking header. Buyer asks why their receipt is not VALID.

