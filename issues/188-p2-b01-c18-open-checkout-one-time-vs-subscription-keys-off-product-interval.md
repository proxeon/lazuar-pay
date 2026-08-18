---
number: "188"
id: B01-C18
severity: P2
status: resolved
resolved_branch: fix/188-onetime-price-not-catalog-interval
source: plans/009-bugs/01-commerce-checkout-activation.md
head: "297ba98"
---

# 188 — B01-C18 — Open-checkout one-time vs subscription keys off `product.Interval`, not the paid price

- **Severity:** P2
- **Status:** resolved
- **Source:** `plans/009-bugs/01-commerce-checkout-activation.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/188-onetime-price-not-catalog-interval`

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B01-C18 — Open-checkout one-time vs subscription keys off `product.Interval`, not the paid price

**Severity:** P2  
**One-sentence fault:** Webhook creates a Subscription whenever `product.Interval != "one_time"`, even if `session.PriceId` points at a one-time `ProductPrice` (the domain `UpsertPrice` allows mixing).

**Evidence.**

```79:89:apps/lazuar-api/Modules/Commerce/Infrastructure/EventHandlers/GatewayPaymentCompletedIntegrationEventHandler.OpenCheckout.cs
        if (product.Interval != "one_time")
        {
            var subscription = new Subscription(...);
            var chosen = product.Prices.FirstOrDefault(p => p.Id == session.PriceId);
            var unitAmount = chosen?.Amount ?? product.Price;
            var interval = chosen?.Interval ?? product.Interval;
```

`Product.UpsertPrice` accepts `one_time` alongside `mo` if you only have one other interval. Ops create/update normally will not, but the domain allows it. Initiate would mint `SetupFutureUsage: false` for a resolved one-time price on a monthly product, then the webhook would still `Start` a subscription.

**Speculation (labeled):** this is not reachable from the current product form if it only writes default + yearly. It is reachable from any caller of `UpsertPrice`.

**Blast radius.** A one-time add-on price on a recurring product would create a subscription after a one-shot payment.

**Why tests missed it.** Completeness products have a single interval.

**Fix direction.** Branch on `chosen?.Interval ?? product.Interval`, the same value already computed two lines later.

---

