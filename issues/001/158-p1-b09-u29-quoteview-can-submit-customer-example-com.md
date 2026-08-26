---
number: "158"
id: B09-U29
severity: P1
status: resolved
resolved_branch: fix/158-quote-placeholder-email
source: plans/009-bugs/09-frontends-ops-portal-admin.md
head: "297ba98"
---

# 158 — B09-U29 — QuoteView can submit `customer@example.com`

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/09-frontends-ops-portal-admin.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/158-quote-placeholder-email`

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

#### B09-U29 — QuoteView can submit `customer@example.com` (P1)

**Where:** `QuoteView.tsx` 50–51.  
If `client_email` is empty, checkout goes out as that mailbox.

