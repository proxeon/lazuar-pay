---
number: "033"
id: B01-C07
severity: P1
status: resolved
source: plans/009-bugs/01-commerce-checkout-activation.md
head: "297ba98"
resolved_branch: fix/033-validate-coupon-chosen-price
---

# 033 — B01-C07 — Validate-coupon and hop-1 discount math ignore the selected price row

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/01-commerce-checkout-activation.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/033-validate-coupon-chosen-price`

Validate-coupon takes `interval` / `price_id` / `quantity` and returns line discount against the resolved price. Hop-1 uses those amounts; no catalog-price ratio.

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B01-C07 — Validate-coupon and hop-1 discount math ignore the selected price row

**Severity:** P1  
**One-sentence fault:** `ValidateCouponQuery` discounts `product.Price`; hop-1 then scales that number as a ratio of `product.price` against the selected yearly×qty line, which is wrong for FIXED coupons and can fail `MinimumOriginalPrice` on the wrong amount.

**Evidence.**

```32:41:apps/lazuar-api/Modules/Commerce/Application/Queries/ValidateCouponQueryHandler.cs
        coupon.Validate(product.Price, product.Id);

        var discount = coupon.CalculateDiscount(product.Price);
        var finalPrice = Math.Max(0, product.Price - discount);

        return new ValidateCouponResponseDto
        {
            Is_valid = true,
            Discount_amount = (double)discount,
            Final_price = (double)finalPrice
        };
```

```55:60:apps/lazuar-portal/src/modules/checkout/components/CheckoutView.tsx
      const data = await validateCouponCode(tenantSlug, product.slug, code);
      const discountRatio = data.discount_amount / product.price;
      const totalDiscount = basePriceForQuantity * discountRatio;
      setDiscountAmount(totalDiscount);
      setFinalPrice(Math.max(0, basePriceForQuantity - totalDiscount));
```

Initiate (the real charge) uses `resolved.Amount`.

**Reproduction in words.** Monthly 100, yearly 1000, FIXED 30 coupon. Validate: discount 30, final 70. UI on yearly qty 3: ratio 0.3, totalDiscount 900, shows RM 2100 off a RM 3000 line. Charge: (1000−30)×3 = 2910 (+ SST). Percentage coupons accidentally look right because the ratio is the percent. A coupon with `MinimumOriginalPrice = 500` cannot be applied on the yearly toggle if monthly is 100 — validate throws “minimum original price” even though 1000 qualifies.

**Blast radius.** Dual-price products + FIXED coupons. UI lie on every yearly toggle. Real initiate may still charge correctly (P1 trust, not always P1 money). Combined with B01-C06 the hop-1 number is wrong twice.

**Why tests missed it.** No validate-coupon test with `Prices` populated. No frontend money test.

**Fix direction.** Validate-coupon takes `interval` / `price_id` / `quantity`. Return unit discount and line discount against the resolved amount. Delete the client-side ratio.

---

