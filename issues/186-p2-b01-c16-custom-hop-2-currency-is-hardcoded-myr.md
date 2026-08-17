---
number: "186"
id: B01-C16
severity: P2
status: open
source: plans/009-bugs/01-commerce-checkout-activation.md
head: "297ba98"
---

# 186 — B01-C16 — Custom hop-2 currency is hardcoded `MYR`

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/01-commerce-checkout-activation.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B01-C16 — Custom hop-2 currency is hardcoded `MYR`

**Severity:** P2  
**One-sentence fault:** Product checkout uses `product.Currency`; custom initiate always sends `"MYR"` and mark-paid custom always books `"MYR"`.

**Evidence.** `InitiateCheckoutCommandHandler.cs` 149; `MarkCheckoutAsPaidOfflineCommandHandler.cs` 188. `CreateCustomCheckoutCommand` has no currency field.

**Reproduction in words.** A workspace whose products are SGD still issues MYR processor sessions for quotes.

**Blast radius.** Today the product is Malaysia-first. The moment a non-MYR tenant uses quotes, first charge is the wrong ISO code.

**Why tests missed it.** All custom tests assume MYR.

**Fix direction.** Persist currency on the quote (workspace default or request field) and thread it through initiate + mark-paid + DTO.

---

