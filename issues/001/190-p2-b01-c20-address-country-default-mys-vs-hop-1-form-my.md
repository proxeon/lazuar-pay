---
number: "190"
id: B01-C20
severity: P2
status: resolved
resolved_branch: fix/190-country-alpha3
source: plans/009-bugs/01-commerce-checkout-activation.md
head: "297ba98"
---

# 190 — B01-C20 — Address country default `MYS` vs hop-1 form `MY`

- **Severity:** P2
- **Status:** resolved
- **Source:** `plans/009-bugs/01-commerce-checkout-activation.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/190-country-alpha3`

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B01-C20 — Address country default `MYS` vs hop-1 form `MY`

**Severity:** P2  
**One-sentence fault:** When the product requires an address, the portal posts `country_code: "MY"`; the handler default (only when omitted) is `"MYS"`.

**Evidence.** `CheckoutForm.tsx` 53 (`useState("MY")`). `InitiateCheckoutCommandHandler.cs` 194 (`request.CountryCode ?? "MYS"`). The posted value wins, so CRM stores `MY` not `MYS`.

**Reproduction in words.** Requires-address product. Buyer submits. CRM stores `MY`. Downstream that expects alpha-3 `MYS` (LHDN is out of slice) sees a 2-letter code.

**Blast radius / tests / fix.** Address-required products only. B2B tests do not post an address. Normalize at the handler.

---

