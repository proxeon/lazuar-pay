# W3-LP-063 — done

A product can carry monthly and yearly `ProductPrice` rows. `Product.Price` / `Interval` remain the default (write-through). Checkout accepts `interval` or `price_id`. The subscription snapshots `UnitAmount`, `PriceId`, and `BillingInterval`. Renewals use the snapshot, not a live catalog join.

## Files

- `ProductPrice` entity + `commerce.ProductPrices` (unique ProductId+Interval)
- Create/update `yearly_price`, public GET `prices[]`
- `InitiateCheckout` price resolve, hop 1 interval toggle
- Billing prefers `UnitAmount`

## Tests run

- Domain price write-through via `SubscriptionTrialTests`, checkout interval resolve via initiate path, Commerce filter **355 passed**

Not committed. Not pushed.

Tracker `LP-063` can move **N → Y**.
