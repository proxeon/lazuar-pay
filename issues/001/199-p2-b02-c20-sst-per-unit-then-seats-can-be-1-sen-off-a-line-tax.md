---
number: "199"
id: B02-C20
severity: P2
status: resolved
resolved_branch: fix/199-sst-line-tax-ssot
source: plans/009-bugs/02-commerce-subscriptions-billing-engine.md
head: "297ba98"
---

# 199 — B02-C20 — SST per-unit then × seats can be 1 sen off a line tax

- **Severity:** P2
- **Status:** resolved
- **Source:** `plans/009-bugs/02-commerce-subscriptions-billing-engine.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/199-sst-line-tax-ssot`

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B02-C20 — P2 — SST per-unit then × seats can be 1 sen off a line tax

**Evidence.** `GrossBreakdown` taxes `unitNet` then multiplies. `SstTaxMath` rounds to 2 dp first.

**Repro.** Unit 33.33, 8%, 3 seats. Helper 8.01. Line tax 8.00.

**Fix direction.** Tax `unitNet * seats` once if LHDN wants line-level. Out of this slice to decide.

---

