---
number: "029"
id: B01-C03
severity: P1
status: resolved
source: plans/009-bugs/01-commerce-checkout-activation.md
head: "297ba98"
resolved_branch: fix/029-zero-amount-offline-chosen-price
---

# 029 — B01-C03 — Zero-amount and offline re-discount `product.Price`, not the chosen price row

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/01-commerce-checkout-activation.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/029-zero-amount-offline-chosen-price`

Zero-amount and mark-paid discount the chosen `PriceId` row. A 100% yearly coupon no longer throws.

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B01-C03 — Zero-amount and offline re-discount `product.Price`, not the chosen price row

**Severity:** P1  
**One-sentence fault:** Initiate applies the coupon to `resolved.Amount`; `ProcessZeroAmount` and mark-paid apply it to `product.Price`, then compare against the chosen row, so a 100% yearly coupon on a monthly-default product throws (or under-discounts cash).

**Evidence.** Initiate (honest):

```223:224:apps/lazuar-api/Modules/Commerce/Application/Commands/InitiateCheckoutCommandHandler.cs
            coupon.Validate(resolved.Amount, product.Id);
            unitDiscount = coupon.CalculateDiscount(resolved.Amount);
```

Zero-amount (not honest):

```50:64:apps/lazuar-api/Modules/Commerce/Application/Commands/ProcessZeroAmountCheckoutCommand.cs
                unitDiscount = coupon.CalculateDiscount(product.Price);
                // ...
        var chosen = product.Prices.FirstOrDefault(p => p.Id == session.PriceId);
        var unitAmount = chosen?.Amount ?? product.Price;
        var lineGross = unitAmount * quantity;
        var lineDiscount = unitDiscount * quantity;
        var isTrial = SubscriptionActivation.IsTrialOffer(product);
        var finalPrice = isTrial ? 0m : Math.Max(0, lineGross - lineDiscount);
        if (finalPrice > 0)
        {
            throw new InvalidOperationException("This checkout session requires payment and cannot bypass the gateway.");
        }
```

Mark-paid (same lie):

```85:92:apps/lazuar-api/Modules/Commerce/Application/Commands/MarkCheckoutAsPaidOfflineCommandHandler.cs
                unitDiscount = coupon.CalculateDiscount(product.Price);
                coupon.ConfirmReservation();
            }
        }

        var lineGross = product.Price * quantity;
        var lineDiscount = unitDiscount * quantity;
        var totalAmount = Math.Max(0, lineGross - lineDiscount);
```

Mark-paid is worse: the **transaction log and Order amount** use `product.Price`, while the subscription snapshot uses `chosen?.Amount ?? product.Price`. A yearly cash settlement is booked at the monthly catalog price.

008 already named this as P1 item 10. It is still in the tree.

**Reproduction in words.** Product default `Interval=mo`, `Price=100`, yearly row `1000`. Coupon `PERCENTAGE 100`. Buyer selects yearly on hop-1 (Billplz). Initiate: unitNet = 0, `lineNet = 0`, not vaulting, calls ProcessZeroAmount. ProcessZeroAmount: discount = 100, unitAmount = 1000, finalPrice = 900, throws. Session stays OPEN, coupon stays reserved, buyer sees a 400. Clerk mark-paid of a yearly session with a 10% coupon books `100 * qty * 0.9` instead of `1000 * qty * 0.9`.

Stripe yearly 100% coupon does **not** hit ProcessZeroAmount (hop-2 $0, type commerce). The webhook confirms the coupon and snapshots 1000. The Billplz / one-time-adjacent / mark-paid paths are the broken ones.

**Blast radius.** Dual-price catalogs (the Wave 3 monthly+yearly product). Reminder-only rails. Clerk cash against a quote that used a coupon. Inverse: a FIXED coupon sized to the monthly price can zero a yearly line if someone called ProcessZeroAmount with a yearly `PriceId` and a monthly `product.Price` larger than the coupon — wait, FIXED 100 on monthly 100 is 100 off; yearly unit 1000 − 100 = 900, still throws. A FIXED coupon of 1000 validated against yearly at initiate would discount only 100 at zero-amount. Always under-discount relative to the chosen row when the chosen row is larger.

**Why tests missed it.** Completeness $0 tests use `CreateProduct` with a single price equal to `product.Price`. `MarkCheckoutAsPaidOffline_OneTime_Qty3` has no coupon and no yearly row.

**Fix direction.** Both handlers must resolve `chosen` first, then `CalculateDiscount(unitAmount)` and (for mark-paid) `lineGross = unitAmount * quantity`. Confirm the reservation after the payable check so a throw does not need a reserve.

---

