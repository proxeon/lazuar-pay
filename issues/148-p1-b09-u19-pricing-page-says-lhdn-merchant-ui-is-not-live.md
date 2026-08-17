---
number: "148"
id: B09-U19
severity: P1
status: open
source: plans/009-bugs/09-frontends-ops-portal-admin.md
head: "297ba98"
---

# 148 — B09-U19 — Pricing page says LHDN merchant UI is not live

- **Severity:** P1
- **Status:** open
- **Source:** `plans/009-bugs/09-frontends-ops-portal-admin.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

#### B09-U19 — Pricing page says LHDN merchant UI is not live (P1)

**Where:** `PricingPage.tsx` 120–124; `GetPublicPricingQueryHandler.cs` 58; `GetPublicPricingQueryHandlerTests.cs` 97.  
API flag is hard-coded false. FE prints a sentence that Wave 2 made false. The test cements the flag.

