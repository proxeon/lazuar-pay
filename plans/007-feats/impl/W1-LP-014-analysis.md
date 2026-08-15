# W1-LP-014 — Quantity on checkout

**Status:** Analysis only — **do not implement from this file**  
**Date:** 2026-08-16  
**ID authority:** [00-implement-ids.md](../00-implement-ids.md) Wave 1 row `LP-014` (“Quantity on checkout”). Tracker in [00-checklist-tracker.md](../00-checklist-tracker.md): Wave 1, `Ours = P`. Sibling seed: [09-checkout-and-payment-links.md](../09-checkout-and-payment-links.md) `CK-010`.  
**Not this ID:** [01-lazuar-feature-inventory.md](../01-lazuar-feature-inventory.md) reuses `LP-014` for “Hosted product checkout” (SHIPPED). [18-pricing-onboarding-trust.md](../18-pricing-onboarding-trust.md) reuses `LP-014` for TOS clickwrap. Ignore those meanings.

**Invariant:** If the buyer can set N, the hop-2 charge is `unit_net × N`, the session remembers N, and the Order written after settle has that same N and line amount. Recurring seats that survive renewal are **not** this ticket.

---

## 0. Scope lock

In scope:

- Hosted product checkout (`lazuar-portal` hop 1)
- `POST /public/commerce/checkout` → `InitiateCheckoutCommandHandler`
- Payments generate contract: `Amount` vs `Quantity` (no adapter rewrite except if a comment is wrong)
- Persist quantity on **product** `CheckoutSession` and on **Order**
- Zero-amount + offline mark-paid using the snapshotted N
- Server reject of `0` / negative / huge / recurring / PWYW quantity

Out of scope (do not expand this ticket):

| Adjacent | Why not LP-014 |
|----------|----------------|
| **LP-060** Quantity / seats on Subscription + renewal + arrears + dunning AUTO_CHARGE | Wave 3. Billing engine still charges `product.Price` × 1. Persisting seats without using them is a lie. |
| **CK-012 / LP-013** PWYW charges the typed amount | Separate honesty bug. `customPrice` is display-only. Hide qty on PWYW. |
| Custom / quote line items (`AdHocLineItem.Quantity`) | Already first-class. Custom initiate already totals lines and sends Payments `Quantity: 1`. Portal `/pay/{id}` is `[MVP-HIDE]`. |
| M2M cashier | Always `quantity: 1` on a caller-supplied amount. Correct. |
| Variants, order bump, min/max qty on Product, merchant “adjustable qty” flag | Storefront OS. Not required to make the existing field honest. |
| Coupon policy change (FIXED off-the-order vs per unit) | Keep current per-unit math. Document it. |
| `order.completed` event catalog versioning / docs site | Optional payload field only if it stays additive. |
| BM/EN, branding, TIN, success-page copy | Other Wave 1 IDs. |

**Dependency (do not implement here):** hop-2 adapters already multiply `amount * quantity` (`GatewayCommon.ToMinorUnits*`, Stripe `LineItems.Quantity`). LP-014 must **stop pre-multiplying the amount Commerce sends**. Do not “fix” adapters by removing the multiplier — M2M / custom / renewal pass `Quantity: 1` and rely on that contract.

---

## 1. What “first-class quantity” means here

Two products, do not mix:

| Product | Buyer meaning | Honest LP-014 | LP-060 later |
|---------|---------------|---------------|--------------|
| `interval == one_time` + `FIXED` | “I want N copies / tickets / licenses of this SKU” | Stepper + charge `Price × N` once + persist N on session and Order | n/a |
| `mo` / `yr` | “N seats on this membership” | **Force N = 1.** Hide stepper. | Persist seats; renewals, arrears, AUTO_CHARGE, webhook `amount` all use `Price × seats` |
| `PWYW` | Typed unit amount | Force N = 1. Display `customPrice × N` would compound the existing charge lie. | n/a |

“Subscription seats if needed” — **not needed**. A seat column that renewals ignore would fail Wave 3 exit criterion 4 in [20-sequencing-and-tracker-schema.md](../20-sequencing-and-tracker-schema.md) (“Quantity/seats either work through renewals or are removed from checkout”). LP-014 removes qty from recurring checkout. LP-060 puts it back on the Subscription.

**Payments contract lock (write this on `GenerateCheckoutSessionQuery` when implementing):**

- `Amount` = **unit** major units after per-unit discount (what one item costs).
- `Quantity` = integer multiplier applied **inside** the adapter.
- Line total the buyer pays = `Amount × Quantity` (Stripe line; Billplz/CHIP/Razorpay fold into one bill amount).
- Callers that already have a line total (custom session sum, M2M amount, renewal `product.Price` as a single seat) **must** pass `Quantity: 1`.

Today every caller except product initiate already does that. Product initiate is the only path that can send `Quantity > 1`, and it already pre-multiplies — so N>1 would **square** the charge.

---

## 2. Current files

### 2.1 Portal (field exists, no control)

| Path | Role |
|------|------|
| `apps/lazuar-portal/src/modules/checkout/components/CheckoutView.tsx` | `useState(1)`, `handleQuantityChange` (strips coupon), `basePriceForQuantity = (PWYW ? customPrice : product.price) * quantity`. Passes both into `CheckoutForm`. |
| `apps/lazuar-portal/src/modules/checkout/components/CheckoutForm.tsx` | Props `quantity` + `onQuantityChange`. Submit body includes `quantity`. **`onQuantityChange` is never called.** No stepper in JSX. |
| `apps/lazuar-portal/src/modules/checkout/components/OrderSummaryCard.tsx` | Subtotal = `context.currentPrice` (already × qty). PWYW input. **No qty row, no “× N”.** |
| `apps/lazuar-portal/src/modules/checkout/components/CheckoutLayout.tsx` | Form left / summary right (summary above on mobile). Stepper belongs on the summary. |
| `apps/lazuar-portal/src/modules/checkout/types.ts` | `CheckoutContext` has prices, not `quantity`. |
| `apps/lazuar-portal/src/modules/checkout/lib/api.ts` | `PublicCheckoutRequestDto` already has optional `quantity`. |
| `apps/lazuar-portal/src/app/[tenantSlug]/checkout/[productSlug]/page.tsx` | SSR product fetch. No qty query param. |

Humans always POST `quantity: 1`. The API already accepts other values.

### 2.2 Contract + initiate

| Path | Role |
|------|------|
| `packages/api-spec/modules/commerce/models/checkout.tsp` | `quantity?: int32` — no min/max, no doc. |
| `packages/api-spec/modules/commerce/models/product.tsp` | No `allow_quantity` / min / max. Price is unit. |
| `apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints/PublicCheckoutEndpoints.cs` | `req.Quantity ?? 1`. **No clamp.** `0` and negatives pass through. |
| `apps/lazuar-api/Modules/Commerce/Contracts/Commands/InitiateCheckoutCommand.cs` | `int Quantity` required on the command. |
| `apps/lazuar-api/Modules/Commerce/Application/Commands/InitiateCheckoutCommandHandler.cs` | Product path: `basePrice = product.Price * Quantity`; coupon `CalculateDiscount(product.Price) * Quantity`; `netAmount = max(0, basePrice - discount)`; **sends `netAmount` and `request.Quantity` to Payments.** Does not read `PricingModel` / `MinimumPrice`. Does not persist N. Custom-session branch totals `UnitPrice * Quantity` and sends Payments `Quantity: 1` (correct). |

Coupon validate (`ValidateCouponQueryHandler`) is **unit** only. Portal scales `discount_amount / product.price * basePriceForQuantity`. Server initiate uses the same per-unit rule. Keep it.

`Coupon.Validate(..., originalPrice)` uses **unit** `product.Price` for `MinimumOriginalPrice`. Honest for a per-unit coupon. Do not switch the minimum to line total in this ticket.

### 2.3 Product (unit catalog, no qty)

`apps/lazuar-api/Modules/Commerce/Domain/Aggregates/Product.cs`

- One `Price`, `PricingModel` (`FIXED` / `PWYW`), `MinimumPrice`, `Currency`, `Interval` (`one_time` / `mo` / `yr`), `GatewayName`, `CheckoutConfiguration` (address / tax / phone only).
- No quantity column, no adjustable flag.
- Ops `ProductForm.tsx` default interval is **`one_time`**. Qty on checkout is the common SKU, not an edge.

Do **not** add Product fields for LP-014.

### 2.4 Session / Order / Subscription (N is forgotten)

| Aggregate | Qty today | Complete path |
|-----------|-----------|---------------|
| `CheckoutSession` | None on product ctor. Custom sessions store `AdHocLineItem.Quantity` in jsonb. | Snapshot is coupon + product id + metadata json only. |
| `Order` | No column. `AmountPaid` only. | Paid webhook: `@event.AmountPaid`. Zero-amount: `0`. Offline: `product.Price - unitDiscount` (**not × N**). |
| `Subscription` | No column. | Created from product id only. Renewals / arrears / AUTO_CHARGE: `product.Price`, Payments `Quantity: 1`. |

EF: `CommerceDbContext` + `CommerceDbContextModelSnapshot` — `CheckoutSessions` has no `Quantity`; `Orders` has no `Quantity`; `Subscriptions` has no `Quantity`.

Writers that re-price from Product and drop N:

- `ProcessZeroAmountCheckoutCommandHandler` — `finalPrice = product.Price - CalculateDiscount(product.Price)` (unit). Event `OriginalAmount = product.Price`.
- `MarkCheckoutAsPaidOfflineCommandHandler.HandleProductSessionAsync` — same unit math.
- `GatewayPaymentCompletedIntegrationEventHandler.OpenCheckout` — Order amount from gateway (OK if hop 2 was honest); no Order.Quantity.

### 2.5 Payments hop 2 (Amount is unit)

| Path | Behaviour |
|------|-----------|
| `GenerateCheckoutSessionQuery` | `decimal Amount`, `int Quantity = 1` |
| `CheckoutSessionCashier.GenerateAsync` | Forwards both unchanged |
| `StripeGatewayAdapter` | `UnitAmountDecimal = amount * 100`, `Quantity = quantity` → Stripe total = amount × qty |
| `BillplzGatewayAdapter` / `RazorpayGatewayAdapter` | `ToMinorUnitsTruncating(amount, quantity)` = `(int)(amount * qty * 100)` |
| `ChipCollectGatewayAdapter` | `ToMinorUnitsRounded(amount, quantity)` into a **single** CHIP product `price` (line total in sen) |
| `GatewayCommon.ProductDescription` | `"{name} (xN)"` when N>1 |
| `CreateIntegrationCheckoutCommandHandler` | `quantity: 1` |
| `RenewalCheckoutIssuer` | `product.Price`, `Quantity: 1` |
| `PublicArrearsEndpoints` | `sub.Price`, `1` |
| Custom initiate | line sum, `1` |

Tests already encode the multiplier: `GatewayCommonTests.ToMinorUnitsTruncating(10.50m, 2) == 2100`.

### 2.6 Fulfillment / webhooks

`order.completed` payload is `{ order_id, client_profile_id, product_id, status }`. No amount, no quantity. Integrator cannot tell “3 tickets” from the webhook.

`subscription.*` payload `amount` is catalog `product.Price` (unit). Fine while seats do not exist.

### 2.7 Tests today

- `CommerceProductCompletenessTests` initiate cases hard-code `Quantity: 1`. Paid path does not assert `GenerateCheckoutSessionQuery.Amount` vs `Quantity`.
- No test for N>1, N=0, recurring N>1, session persistence, or offline/zero-amount × N.
- Portal has no test runner.

---

## 3. End-to-end (one-time FIXED, N = 3) — today vs honest

```
Buyer POST /public/commerce/checkout { quantity: 3 }
  → Initiate: lineNet = Price * 3 − unitDiscount * 3
  → Session OPEN (no N stored)
  → GenerateCheckoutSessionQuery(Amount: lineNet, Quantity: 3)
  → Stripe/Billplz/CHIP charge lineNet * 3 = Price * 9     ← SQUARE
Buyer pays hop 2
  → GatewayPaymentCompleted.AmountPaid = squared amount
  → Order.AmountPaid = squared; no Quantity
Renewal / seats: n/a (one_time)
```

Humans never see this because the stepper is missing. Shipping the stepper **without** the unit-amount fix is a money bug, not a feature.

Honest:

```
Buyer sets N=3 on summary
  → POST quantity: 3
  → Reject unless product is FIXED + one_time and 1 ≤ N ≤ 99
  → unitNet = Price − unitDiscount
  → lineNet = unitNet * N
  → Session.Quantity = 3
  → if lineNet == 0 → zero-amount (Order.Quantity = 3, Order.AmountPaid = 0)
  → else GenerateCheckoutSessionQuery(Amount: unitNet, Quantity: 3)
  → hop 2 charges unitNet * 3
  → webhook Order.AmountPaid = that total, Order.Quantity = 3
```

---

## 4. What is already correct

1. **DTO + command + portal state exist.** Do not invent a second field name.
2. **Payments adapters already know how to multiply.** Stripe line qty; Billplz/CHIP/Razorpay fold sen; description `xN`.
3. **Custom checkout quantity is already honest** (`sum(unit * qty)` + Payments qty 1).
4. **M2M / renewal / arrears pass qty 1** with a total-or-unit-that-equals-total. Leave them alone.
5. **Coupon preview is unit; initiate scales per unit.** Portal and server agree. A “RM 10 off” code is RM 10 off **per item**. Keep and say so in hop-1 copy if you show a discount row at N>1.
6. **Paid Order amount can be the gateway truth** once hop 2 is not squared.
7. **Default ops product is `one_time`.** Qty is useful without building seats.

Tracker `Ours = P` is right: plumbing is half-wired; the field is a ghost; N>1 is unsafe.

---

## 5. Exact gaps

### G1 — Hop-2 amount is squared when N>1 (blocker)

`InitiateCheckoutCommandHandler` computes `netAmount` as a **line** total, then passes `request.Quantity` into `GenerateCheckoutSessionQuery`. Adapters treat `Amount` as **unit**.

Must pass `unitNet` + `Quantity`. Do not change adapter math.

### G2 — No quantity UI

`onQuantityChange` is dead. Buyers cannot set N. `OrderSummaryCard` does not show “× N”.

### G3 — N is not persisted

Product `CheckoutSession` has no `Quantity`. After pay / offline / debug you cannot see what was intended. Zero-amount and offline re-read `product.Price` as a single unit.

### G4 — Order has no quantity

Fulfillment (“3 licenses”) cannot be reconstructed once a coupon exists (`AmountPaid / Price` is wrong). `order.completed` has no `quantity`.

### G5 — `Quantity` is unvalidated

`req.Quantity ?? 1` accepts `0` and negatives.

- N = 0, no 100% coupon: `netAmount == 0` → session saved OPEN → `ProcessZeroAmount` throws (unit price still > 0) → leftover OPEN session, possible coupon reserve.
- N < 0: `Math.Max(0, …)` can also hit the zero-amount branch.
- Huge N: huge or overflowed gateway amount.

Must validate **before** `AddCheckoutSession`.

### G6 — Recurring + PWYW would lie if the stepper were global

- Recurring N>1: first charge ×N, renewal ×1, no seats. **Refuse in LP-014.**
- PWYW: summary uses `customPrice * N`; initiate uses `product.Price * N`. Two lies.

Force N = 1 (and hide the control) unless `pricing_model == FIXED` **and** `interval == one_time`.

### G7 — Zero-amount / offline ignore N

Even after G1, 100% coupon × 3 must write `Order.Quantity = 3` and ledger `OriginalAmount` / `DiscountAmount` as **line** figures (`ZeroAmountCheckoutCompletedIntegrationEvent` → `ZeroAmountCheckoutHandler` balances discount vs revenue). Offline mark-paid must charge/record `(Price − unitDiscount) * N`.

### G8 — No tests for the money identity

No assertion that `GenerateCheckoutSessionQuery.Amount * Quantity == lineNet`.

**Not gaps for this ticket**

| Observation | Why not LP-014 |
|-------------|----------------|
| Subscription has no Quantity | LP-060 |
| Billing engine / arrears / renewal use `product.Price` | Correct while seats do not exist |
| FIXED coupon × N is per item | Document; do not change |
| Price edit while session OPEN | Pre-existing; paid path uses gateway amount. Optional amount snapshot is extra. |
| Custom `/pay/{id}` 404 | Hidden product |
| PWYW input vs catalog charge | CK-012 |
| No merchant “disable qty” flag | Existing one_time links become adjustable. Accept for v1, or add a flag later. |

---

## 6. Minimal code changes

Cap: **`CommerceCheckoutQuantity.Max = 99`** (integer). Stripe Payment Links-shaped. One named constant in Commerce Application; portal uses the same numbers.

### 6.1 Must change

| File | Function | Change |
|------|----------|--------|
| `Modules/Commerce/Application/CommerceCheckoutQuantity.cs` | **new** | `Min = 1`, `Max = 99`. `NormalizeOrThrow(int? raw, Product product)`: default 1; reject non-integers already implied by `int`; reject outside range; if product is not `FIXED` + `one_time`, require `== 1`. |
| `InitiateCheckoutCommandHandler.cs` | product branch, **before** CRM/coupon | Call `NormalizeOrThrow`. Then `unitDiscount = coupon?.CalculateDiscount(product.Price) ?? 0`; `unitNet = max(0, product.Price − unitDiscount)`; `lineNet = unitNet * quantity`. Persist session **with quantity**. Zero-amount if `lineNet == 0`. Paid: `GenerateCheckoutSessionQuery(..., Amount: unitNet, Quantity: quantity, ...)`. |
| `PublicCheckoutEndpoints.cs` | POST `/checkout` | Keep `?? 1`. Handler throws → existing 400. Do not silently clamp a posted `0` to 1 (that would hide a client bug). |
| `CheckoutSession.cs` | product constructor | Add `int quantity = 1`. Store `Quantity` (get; private set). Custom ctor leaves `Quantity = 1` (unused; lines live in jsonb). |
| `CommerceDbContext.cs` + new Commerce migration | — | `commerce.CheckoutSessions.Quantity` `integer NOT NULL DEFAULT 1`. `commerce.Orders.Quantity` `integer NOT NULL DEFAULT 1`. |
| `Order.cs` | ctor | Add `int quantity = 1`; persist. |
| `ProcessZeroAmountCheckoutCommand.cs` | `Handle` | `N = session.Quantity` (min 1). `unitDiscount` as today. `lineGross = product.Price * N`; `lineDiscount = unitDiscount * N`; `final = lineGross - lineDiscount`; throw if `final > 0`. `new Order(..., 0m, currency, N)`. Event `OriginalAmount = lineGross`, `DiscountAmount = lineDiscount`. |
| `MarkCheckoutAsPaidOfflineCommandHandler.cs` | `HandleProductSessionAsync` | Same line math; `new Order(..., totalAmount, currency, session.Quantity)`. |
| `GatewayPaymentCompletedIntegrationEventHandler.OpenCheckout.cs` | one_time branch | `new Order(..., @event.AmountPaid, currency, session.Quantity)`. |
| `CheckoutView.tsx` | state + handlers | Clamp `handleQuantityChange` to 1..99. Expose `quantityAdjustable = product.pricing_model === "FIXED" && product.interval === "one_time"`. If not adjustable, keep `quantity === 1` and do not render a control. Put `quantity` on `CheckoutContext`. |
| `OrderSummaryCard.tsx` | summary | If adjustable: − / input / + stepper (or number input `min=1 max=99 step=1`). Line: unit price × N = subtotal. If N>1, show unit then “× N”. Total Due remains `finalPrice ?? currentPrice`. |
| `CheckoutForm.tsx` | — | Keep sending `quantity`. Safe to drop unused `onQuantityChange` from the form if the stepper lives only on the summary (View already owns state). Do not add a second stepper in the form. |
| `packages/api-spec/modules/commerce/models/checkout.tsp` | `quantity` | Doc: optional, default 1, only meaningful for FIXED one-time; min 1 max 99. Regen contracts if the repo’s TypeSpec gate requires it. |

### 6.2 Should change (same ticket, small)

| File | Change |
|------|--------|
| `GenerateCheckoutSessionQuery.cs` | XML-doc: Amount is **unit**; Quantity multiplies in the adapter. Line total = Amount × Quantity. |
| `OrderCompletedIntegrationEventHandler.cs` | Additive `quantity` on `order.completed` JSON (from `Order.Quantity`). Do not bump a version; unknown fields are ignored. Load the Order (handler already has the repo) instead of changing the integration event unless that is cleaner. |
| `OrderSummaryCard` discount row | If N>1 and FIXED coupon, label “Discount (per item × N)” so RM 10 × 3 = RM 30 is not a surprise. |
| `types.ts` | `quantity`, `quantityAdjustable` on `CheckoutContext`. |

### 6.3 Do not change

- `Subscription` aggregate, billing engine, `RenewalCheckoutIssuer`, `PublicArrearsEndpoints`, dunning `product.Price` AUTO_CHARGE
- Adapter `ToMinorUnits*` / Stripe `Quantity`
- Custom checkout / `AdHocLineItem`
- M2M cashier
- PWYW submit (still no `custom_amount` field)
- Product / `CheckoutConfiguration`
- Coupon `CalculateDiscount` / `Validate` (stay unit)
- Portal success page, magic tokens, TypeSpec `token`

### 6.4 Optional later (not required to close LP-014)

- Product `allow_quantity` default true for one_time (hide stepper on a PDF SKU).
- Snapshot `UnitNet` / `LineNet` on the session so offline ignores a mid-flight catalog price edit.
- `?qty=` on the buy link.
- Validate-coupon query that accepts quantity (preview already scales in the View).
- Seats: LP-060.

---

## 7. Tests to add

Portal has no unit runner. Put the identity in **API module tests**. Manual smoke for the stepper.

### 7.1 `CommerceCheckoutQuantity`

New: `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/CommerceCheckoutQuantityTests.cs`

| Case | Expect |
|------|--------|
| `null` / omitted | 1 |
| `1` on FIXED one_time | 1 |
| `3` on FIXED one_time | 3 |
| `0`, `-1`, `100` | throw |
| `3` on `mo` / `yr` | throw |
| `3` on PWYW one_time | throw |
| `1` on `mo` or PWYW | 1 |

### 7.2 Initiate paid path (extend `CommerceProductCompletenessTests`)

Need a `one_time` product (today `CreateProduct` defaults `interval: "mo"`).

| Case | Expect |
|------|--------|
| FIXED one_time, qty 3, no coupon, Price 100 | `GenerateCheckoutSessionQuery.Amount == 100`, `Quantity == 3` (not Amount 300) |
| FIXED one_time, qty 3, 10% coupon | Amount == 90, Quantity == 3 |
| FIXED one_time, qty 3 | Session.Quantity == 3, Status OPEN |
| FIXED one_time, qty 0 | throw **before** `AddCheckoutSession` |
| `mo`, qty 3 | throw; no session |
| Custom session branch | still Amount = line sum, Quantity == 1 (regression) |

### 7.3 Zero-amount + offline + webhook Order

| Case | Expect |
|------|--------|
| 100% coupon, qty 3, one_time | Session COMPLETED; Order.AmountPaid == 0; Order.Quantity == 3; `ZeroAmountCheckoutCompleted.OriginalAmount == Price * 3` |
| Offline mark-paid, qty 3, no coupon, Price 100 | Order.AmountPaid == 300; Order.Quantity == 3 |
| `GatewayPaymentCompleted` open session qty 3, one_time, AmountPaid 300 | one Order, Quantity 3, AmountPaid 300; no Subscription |

### 7.4 Adapter regression (already exists — keep)

Do **not** change `GatewayCommonTests` qty=2 → 2100. Add a comment in the initiate test that those helpers must keep multiplying.

### 7.5 Manual (portal)

1. FIXED one_time RM 50: stepper 1 → 3; summary RM 150; Billplz/Stripe hop 2 is RM 150, description `x3`.
2. Monthly product: no stepper; POST `quantity: 3` → 400; charge never starts.
3. PWYW: no stepper.
4. 10% coupon then qty 3: coupon clears (existing View behaviour); re-apply; discount is 10% of line.
5. FIXED RM 10 coupon × 3: discount RM 30 (per item). Hop 2 = line net.

---

## 8. Risks

| Risk | Severity | Mitigation |
|------|----------|------------|
| Ship stepper before G1 | **High (money)** | Do backend unit-amount + tests first. Portal last. |
| “Fix” adapters to stop multiplying | **High** | Breaks the documented helper tests and any future unit+qty caller. Commerce is the bug. |
| Persist `Subscription.Quantity` “for later” | High (honesty) | First charge ×N, renewal ×1. Leave the column out. |
| Show stepper on every product | Med | Recurring/PWYW force 1 + hide. |
| Existing one_time links become multi-qty | Low | Default 1. Same URL. Acceptable for CaaS tickets/licenses. Flag later if a merchant yells. |
| FIXED coupon × N surprises “RM 10 off the order” | Low | Keep per-unit; label the discount row. |
| TypeSpec regen drift | Low | Doc-only on `quantity` is enough if CI allows; otherwise regen `api-types-*`. |
| Order webhook `quantity` surprises old integrators | Low | Additive JSON. |
| Ledger zero-amount still unit if event not updated | Med | G7 must send line gross/discount so `ZeroAmountCheckoutHandler` stays balanced. |

---

## 9. Acceptance criteria

Close LP-014 when all of the following are true:

1. FIXED `one_time` hop 1 shows a quantity control (1–99). Summary line amount is `unit × N`. Submitted `quantity` matches the control.
2. `GenerateCheckoutSessionQuery` for that purchase has `Amount == unitNet` and `Quantity == N`. Hop 2 charge is `unitNet × N`, not `unitNet × N × N`.
3. `commerce.CheckoutSessions.Quantity` and `commerce.Orders.Quantity` equal N for that purchase (paid, zero-amount, and offline).
4. Recurring and PWYW: no stepper; API `quantity` other than 1 (or omitted→1) is 400; no leftover OPEN session.
5. `quantity` 0 / negative / >99 is 400 before persist.
6. No `Subscriptions.Quantity` column. Billing engine, renewal issuer, and arrears still charge `product.Price` × 1.
7. Custom / M2M / renewal still send Payments `Quantity: 1`.
8. Tests in §7.1–7.3 exist and pass.
9. Manual §7.5 on one sandbox rail (Billplz or Stripe) shows hop-2 amount = summary total.

---

## 10. Suggested implement order

1. `CommerceCheckoutQuantity` + session/order columns + constructor defaults (G3, G5, G6 server-side)  
2. Initiate: unitNet + persist N + tests §7.2 (G1)  
3. Zero-amount, offline, open-checkout Order.Quantity + tests §7.3 (G7)  
4. Portal stepper + summary × N (G2)  
5. Manual §7.5  

That is the whole ticket. Seats are LP-060.
