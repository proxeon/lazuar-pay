---
number: "138"
id: B09-U09
severity: P1
status: resolved
resolved_branch: fix/138-quote-success-token
source: plans/009-bugs/09-frontends-ops-portal-admin.md
head: "297ba98"
---

# 138 — B09-U09 — Quote settled CTA and custom-success return are tokenless

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/09-frontends-ops-portal-admin.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/138-quote-success-token`

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

#### B09-U09 — Quote settled CTA and custom-success return are tokenless (P1)

**Where:** `QuoteView.tsx` 96–98; `checkout/custom/success/page.tsx` 22.  
**Walk:** Pay a quote → “Open buyer portal” → U01/U02.

