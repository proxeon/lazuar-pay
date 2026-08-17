---
number: "304"
id: B09-U36
severity: P2
status: open
source: plans/009-bugs/09-frontends-ops-portal-admin.md
head: "297ba98"
---

# 304 — B09-U36 — Checkout i18n holes

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/09-frontends-ops-portal-admin.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

#### B09-U36 — Checkout i18n holes (P2)

`CheckoutForm.tsx` 228–251 (“ID type”, “ID value”); `CheckoutView.tsx` 160 (“Yearly” / “Monthly”); portal, update-payment, QuoteView, legal: English only. The i18n test only checks dictionary key parity.

