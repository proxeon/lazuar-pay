# D21 — `prices`

**Track:** Database · **Depends:** D20  
**Analysis:** [03](../03-host-production-seams.md), [09](../09-data-migration.md)  
**Goal:** NP-CAT-003. Amounts in **MYR**. Intervals `mo` | `yr` | `one_off`.

---

## D21.1 Table

- [x] `prices`: `id`, `product_id`, `currency` **MYR**, `amount`, `interval` (`mo` | `yr` | `one_off`)
- [x] `product_id` references `products` (same database)
- [x] Qty / seats column **optional** (Bar C). Do not block D21 on SST × seats

## D21.2 Currency

- [x] Start **MYR** (NP-CAT-003)
- [x] Do not invent a multi-currency product matrix in this table

## D21.3 Refuse

- [x] No LHDN / tax-document columns on the price
- [x] No Hub `ProductPrices` copy including parked types
- [x] No second `billing` schema for amounts

## D21.4 Exit

- [x] Table exists; MYR is the currency
- [x] Unblocked for D22
