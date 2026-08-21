# D20 — `products`

**Track:** Database · **Depends:** D16  
**Analysis:** [03](../03-host-production-seams.md), [09](../09-data-migration.md)  
**Goal:** Catalog table. No Hub product-type enum explosion. No LHDN columns.

---

## D20.1 Table

- [ ] `products`: `id`, `org_id`, `name`, optional `description`, `created_at`
- [ ] `org_id` is One tenant id. No FK to a Pay org table
- [ ] Insert happens when Ada creates a product — do not seed on `tenant.created`

## D20.2 Refuse

- [ ] No Hub product type enum explosion
- [ ] No LHDN columns (`SstTaxType` 01 theatre, MSIC, UBL, VALID)
- [ ] No `HasDefaultSchema("commerce")`
- [ ] No copy of `commerce.Products` row-for-row

## D20.3 Not this file

- [ ] HTTP create/list is CAT, not this phase
- [ ] Prices are D21

## D20.4 Exit

- [ ] Table exists on `lazuar_pay` via D16 migrator
- [ ] Unblocked for D21
