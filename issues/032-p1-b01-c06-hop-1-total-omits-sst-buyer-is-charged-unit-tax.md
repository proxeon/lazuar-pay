---
number: "032"
id: B01-C06
severity: P1
status: open
source: plans/009-bugs/01-commerce-checkout-activation.md
head: "297ba98"
---

# 032 — B01-C06 — Hop-1 total omits SST; buyer is charged unit+tax

- **Severity:** P1
- **Status:** open
- **Source:** `plans/009-bugs/01-commerce-checkout-activation.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B01-C06 — Hop-1 total omits SST; buyer is charged unit+tax

**Severity:** P1  
**One-sentence fault:** Public product DTO includes `sst_tax_type` / `sst_rate_percent`; the hosted summary never uses them; initiate adds exclusive SST after submit.

**Evidence.** `OrderSummaryCard` total is `finalPriceToDisplay` derived from `currentPrice` / coupon. Grep of that file for `sst` is empty. Initiate paid path:

```336:360:apps/lazuar-api/Modules/Commerce/Application/Commands/InitiateCheckoutCommandHandler.cs
            if (unitTax > 0)
            {
                metadata["sst_tax_type"] = sstType;
                metadata["sst_tax_amount"] = (unitTax * quantity).ToString("0.00");
                metadata["sst_rate_percent"] = product.SstRatePercent.ToString("0.##");
            }
            // Amount is unit price (net + SST); adapters multiply by Quantity.
            var gatewayQuery = new GenerateCheckoutSessionQuery(
                tenantId.Value,
                unitGross,
                // ...
                quantity,
```

Product GET **does** return the rate (`CommerceQueryService.Products.cs` 140–141). The lie is hop-1 chrome, not missing catalog data.

008 P1 item 6. Still true. In this slice because it is the first-charge amount the buyer consents to.

**Reproduction in words.** SST-registered merchant, product `02` / 8%, price 100. Hop-1 shows RM 100. Billplz/Stripe hop-2 shows RM 108. Buyer who does not read the processor page is over-surprised, not undercharged. Charge is legally the SST-inclusive amount; consent UX is the pre-tax amount.

**Blast radius.** Every SST `02` product on hosted checkout. Conversion and complaint risk. Not a silent undercharge on hop-1 itself (that was the pre-eba0741 bug).

**Why tests missed it.** No portal test of the total. API tests that initiate without `IBillingQueryService` never add tax, so they cannot catch a UI miss.

**Fix direction.** Compute exclusive tax on hop-1 from product SST fields **and** a public “merchant has SST id” flag (or always show “SST applies at payment if registered”). Show tax line + gross. Keep GrossBreakdown as SSoT.

---

