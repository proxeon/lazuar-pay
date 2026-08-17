---
number: "185"
id: B01-C15
severity: P2
status: open
source: plans/009-bugs/01-commerce-checkout-activation.md
head: "297ba98"
---

# 185 — B01-C15 — Ad-hoc lines accept qty ≤ 0 and negative prices

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/01-commerce-checkout-activation.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B01-C15 — Ad-hoc lines accept qty ≤ 0 and negative prices

**Severity:** P2  
**One-sentence fault:** `AdHocLineItem` stores whatever it is given; a zero or negative quote still mints hop-2.

**Evidence.**

```12:17:apps/lazuar-api/Modules/Commerce/Domain/ValueObjects/AdHocLineItem.cs
    public AdHocLineItem(string description, int quantity, decimal unitPrice)
    {
        Description = description;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }
```

Session constructor only rejects **zero lines**, not zero money. Custom initiate with sum 0 still calls Payments with amount 0 and `SetupFutureUsage: false`. Stripe `Mode = "payment"` with a $0 line is invalid (`amount == 0 && setupFutureUsage` is the only $0 branch).

**Reproduction in words.** Ops posts a line `qty=1, unit=0` or `qty=-1, unit=100`. Quote shows RM 0 or negative. Pay either 400s at the adapter or, if a gateway accepts 0 payment, completes a custom session for free.

**Blast radius.** Clerk error. Not a buyer exploit unless ops API is open to them (it is `OrgMember`).

**Why tests missed it.** Create-custom tests use 500 / 1.

**Fix direction.** Domain: qty ≥ 1, unit ≥ 0, sum > 0 (or explicit $0 quote policy that does not mint a payment-mode session).

---

