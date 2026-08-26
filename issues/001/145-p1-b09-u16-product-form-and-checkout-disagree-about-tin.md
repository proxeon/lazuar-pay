---
number: "145"
id: B09-U16
severity: P1
status: resolved
resolved_branch: fix/145-tin-copy
source: plans/009-bugs/09-frontends-ops-portal-admin.md
head: "297ba98"
---

# 145 — B09-U16 — Product form and checkout disagree about TIN

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/09-frontends-ops-portal-admin.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/145-tin-copy`

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

#### B09-U16 — Product form and checkout disagree about TIN (P1)

**Where:** `ProductForm.tsx` 222 vs `CheckoutForm.tsx` 96–110 vs `messages.ts` `form.taxIdHint` vs `QuoteView.tsx` 37–40 (no validate).  
Three bars: ops says no validate; product checkout validates immediately; quotes collect TIN and do not call MyInvois. Checkout hint says “later step.”

