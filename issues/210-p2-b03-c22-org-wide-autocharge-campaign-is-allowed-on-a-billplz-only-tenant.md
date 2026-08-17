---
number: "210"
id: B03-C22
severity: P2
status: open
source: plans/009-bugs/03-commerce-dunning-arrears-portal.md
head: "297ba98"
---

# 210 — B03-C22 — Org-wide AUTO_CHARGE campaign is allowed on a Billplz-only tenant

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/03-commerce-dunning-arrears-portal.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B03-C22 — P2 — Org-wide AUTO_CHARGE campaign is allowed on a Billplz-only tenant

`DunningCampaignAutoChargeGuard`: empty product list **returns** (lines 44–47). Default seed adds AUTO_CHARGE 1 and 5. Runtime skip + consume (B03-C03’s cousin). Ops thinks retries exist; logs say skipped.

---

