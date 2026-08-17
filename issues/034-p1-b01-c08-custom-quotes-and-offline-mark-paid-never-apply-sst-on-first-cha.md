---
number: "034"
id: B01-C08
severity: P1
status: open
source: plans/009-bugs/01-commerce-checkout-activation.md
head: "297ba98"
---

# 034 — B01-C08 — Custom quotes and offline mark-paid never apply SST on first charge

- **Severity:** P1
- **Status:** open
- **Source:** `plans/009-bugs/01-commerce-checkout-activation.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B01-C08 — Custom quotes and offline mark-paid never apply SST on first charge

**Severity:** P1  
**One-sentence fault:** Hop-1 SST exists only on the product initiate path; a custom quote hop-2 and a clerk mark-paid book the pre-tax line even when the merchant has an SST id.

**Evidence.** Custom initiate amount is the raw sum, currency `MYR`, no GrossBreakdown, no `sst_tax_*` metadata (§4.1). Mark-paid `totalAmount` is catalog/coupon math with no tax (§4.8). Quote GET `Total_amount` is the same raw sum (`CommerceQueryService.CustomCheckouts.cs` 67, 112). Billing journals tax from metadata:

```159:174:apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/GatewayPaymentCompletedHandler.cs
        if (@event.Metadata != null
            && @event.Metadata.TryGetValue("sst_tax_amount", out var raw)
            && decimal.TryParse(...)
            && parsed > 0)
        {
            return parsed;
        }
```

No stamp → tax 0 on the ledger for that first charge (ledger internals are out of slice; the missing stamp is in slice).

**Reproduction in words.** SST-registered studio sends a QT- for RM 5000 design. Buyer pays RM 5000. No 8% collected. Clerk marks a product session paid: tx log 100, not 108.

**Blast radius.** Every custom quote and every offline settlement for an SST-registered merchant. This is first-charge tax, not renewal tax (renewals are report 02).

**Why tests missed it.** Custom tests assert 500 and quantity 1. Mark-paid tests assert 300 for qty 3 at 100. No billing fake with an SST number is injected into those handlers.

**Fix direction.** Run GrossBreakdown on custom line totals (or per line) when the merchant has an SST id; stamp metadata; show tax on `QuoteView`. Mark-paid should book the same gross the hop-1 product path would have charged, or require the clerk to type the tax-inclusive amount explicitly.

---

