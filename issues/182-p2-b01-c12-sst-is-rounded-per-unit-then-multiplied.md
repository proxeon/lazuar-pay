---
number: "182"
id: B01-C12
severity: P2
status: open
source: plans/009-bugs/01-commerce-checkout-activation.md
head: "297ba98"
---

# 182 — B01-C12 — SST is rounded per unit then multiplied

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/01-commerce-checkout-activation.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B01-C12 — SST is rounded per unit then multiplied

**Severity:** P2  
**One-sentence fault:** Exclusive 8% on the line can differ by sen from 8% on the unit × seats because `Math.Round` runs on the unit.

**Evidence.** `GrossBreakdown` in §4.4. Example: unitNet 10.03, 8%, qty 3 → unit tax 0.80, line tax 2.40, gross 32.49. Tax on 30.09 = 2.41, gross 32.50.

**Reproduction in words.** Sell 3 seats of a price that is not a multiple of 0.125. Hop-2 charge is `unitGross * qty` in the adapter. MyInvois (out of slice) typically wants tax on the line.

**Blast radius.** Sen-level. SST merchants with odd unit prices and qty > 1.

**Why tests missed it.** Tests use 100 × 1 and 100 × 3 (8% lands on whole sen).

**Fix direction.** Compute tax on `unitNet * seats`, then allocate, or document that Lazuar’s SSoT is per-unit. Do not mix the two across hop-1 vs LHDN.

---

