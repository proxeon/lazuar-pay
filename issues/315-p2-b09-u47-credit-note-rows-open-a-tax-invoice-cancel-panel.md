---
number: "315"
id: B09-U47
severity: P2
status: open
source: plans/009-bugs/09-frontends-ops-portal-admin.md
head: "297ba98"
---

# 315 — B09-U47 — Credit-note rows open a tax-invoice cancel panel

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/09-frontends-ops-portal-admin.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

#### B09-U47 — Credit-note rows open a tax-invoice cancel panel (P2)

`CreditNotesPage.tsx` mounts `TaxInvoiceDetailPanel`.

