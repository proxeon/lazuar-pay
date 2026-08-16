# W3-LP-063 — Multiple prices per product (mo / yr)

**Status:** Analysis only — **do not implement from this file**  
**Date:** 2026-08-16  
**ID authority:** [00-implement-ids.md](../00-implement-ids.md) Wave 3 `LP-063`. Tracker: *Multiple prices per product* — Lazuar **N**. Alias `SL-008`.  
**Not this ID:** Seats (`LP-060`). Plan change across products (`LP-058`). Per-currency prices. Graduated/volume tiers. Add-ons (`LP-062` skip). PWYW.

**Invariant:** One catalog SKU can offer **monthly and yearly** amounts without cloning `basic-mo` / `basic-yr`. Checkout picks a price. The subscription stores **which price** (or a snapshot of interval + unit amount). Renewals use that snapshot, not a sibling product.

---

## 0. Scope lock

In scope:

- Thin `ProductPrice` child: `interval` (`mo`\|`yr`), `amount`, `is_default`
- Product still has legacy `Price` + `Interval` = the default row (compat)
- Hop 1 interval toggle
- `Subscription.PriceId` or snapshotted `BillingInterval` + `UnitAmount`

Out of scope:

- N currencies  
- Intro prices  
- Archived price still billed (grandfather = snapshot on the sub — yes, **keep snapshot**)  
- More than two intervals

---

## 1. Verdict

`Product` is one amount + one interval. Merchants duplicate products to sell annual. That breaks plan change (two `ProductId`s) and MRR joins.

Minimal is **not** Stripe Prices-as-a-platform. Two rows per product is enough.

---

## 2. Current files

| Path | Role |
|------|------|
| `Product.Price` / `Interval` | Single point |
| `product.tsp` | Same |
| `ProductForm.tsx` | One interval select, one price |
| `InitiateCheckout` | `product.Price * quantity` |
| Billing / dunning | `product.Price` |

No `ProductPrices` table.

---

## 3. Exact gaps

| # | Gap |
|---|-----|
| G1 | No second amount |
| G2 | Checkout cannot pick yr vs mo on one slug |
| G3 | Editing `Product.Price` rewrites every renewer (grandfather hole — snapshot on sub) |

---

## 4. Recommended model

```
commerce.ProductPrices
  Id, ProductId, Interval (mo|yr), Amount, IsDefault
  unique (ProductId, Interval)

Product.Price / Interval remain the default price (write-through on save)

Checkout:
  GET product returns prices[]
  POST checkout { price_id } or { interval }
  reject interval that has no row

Subscription:
  PriceId?
  UnitAmount  // snapshot at activate / apply-pending
  BillingInterval snapshot if not already implied

Renewal amount = UnitAmount * Quantity
  (stop re-reading product.Price once snapshot exists)
```

v1 allow-list: at most `mo` + `yr`. Creating a third interval = 400.

Default backfill: one `ProductPrice` from existing columns.

---

## 5. Minimal code changes

| File | Change |
|------|--------|
| New entity + FK + migration | `ProductPrices` |
| `Product` helpers | `GetPrice(interval)` |
| TypeSpec `ProductDto` | `prices: { id, interval, amount }[]` |
| `ProductForm` | Monthly + yearly amounts (yearly optional) |
| Checkout hop 1 | Toggle if `prices.length > 1` |
| `InitiateCheckout` | Resolve price; persist snapshot on sub |
| Billing / arrears / webhook | Prefer `sub.UnitAmount` else `product.Price` |
| `task gen` | |

Must not: delete `Product.Price`; usage tiers; per-currency.

---

## 6. Tests

| Case | Expect |
|------|--------|
| Product with mo+yr | Public GET lists both |
| Checkout `interval=yr` | Charge yearly amount; sub snapshot yearly |
| Checkout missing interval | Default price |
| Billing | Uses snapshot even if merchant edits catalog |
| Only `mo` row | Yearly toggle hidden; `interval=yr` 400 |

---

## 7. Acceptance

1. One product page can sell monthly or yearly.  
2. Renewals follow the **chosen** amount after a catalog edit of the other interval.  
3. Duplicate `basic-mo` / `basic-yr` is no longer required in docs.  
4. Plan change (LP-058) can target another product **or** (optional same PR) switch interval via pending price — interval switch on the **same** product is allowed here because both prices share gateway/currency.

Tracker **N → Y** after 1–2.

---

## 8. Order

1. Table + backfill + GET  
2. Snapshot on activate  
3. Engine reads snapshot  
4. Form + hop 1  
5. Tests  

Prefer landing snapshot **before** LP-161 (MRR should use `UnitAmount`).

Do **not** implement from this file.
