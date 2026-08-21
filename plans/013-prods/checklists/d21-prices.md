# D21 — `prices`

**Track:** Database · **Depends:** D20  
**Analysis:** [03](../03-host-production-seams.md), [09](../09-data-migration.md)  
**Goal:** NP-CAT-003. Amounts in **MYR**. Intervals `mo` | `yr` | `one_off`.

---

## D21.1 Table

- [ ] `prices`: `id`, `product_id`, `currency` **MYR**, `amount`, `interval` (`mo` | `yr` | `one_off`)
- [ ] `product_id` references `products` (same database)
- [ ] Qty / seats column **optional** (Bar C). Do not block D21 on SST × seats

## D21.2 Currency

- [ ] Start **MYR** (NP-CAT-003)
- [ ] Do not invent a multi-currency product matrix in this table

## D21.3 Refuse

- [ ] No LHDN / tax-document columns on the price
- [ ] No Hub `ProductPrices` copy including parked types
- [ ] No second `billing` schema for amounts

## D21.4 Exit

- [ ] Table exists; MYR is the currency
- [ ] Unblocked for D22
