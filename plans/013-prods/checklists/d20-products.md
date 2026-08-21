# D20 — `products`

**Track:** Database · **Depends:** D16  
**Analysis:** [03](../03-host-production-seams.md), [09](../09-data-migration.md)  
**Goal:** Catalog table. No Hub product-type enum explosion. No LHDN columns.

---

## D20.1 Table

- [x] `products`: `id`, `org_id`, `name`, optional `description`, `created_at`
- [x] `org_id` is One tenant id. No FK to a Pay org table
- [x] Insert happens when Ada creates a product — do not seed on `tenant.created`

## D20.2 Refuse

- [x] No Hub product type enum explosion
- [x] No LHDN columns (`SstTaxType` 01 theatre, MSIC, UBL, VALID)
- [x] No `HasDefaultSchema("commerce")`
- [x] No copy of `commerce.Products` row-for-row

## D20.3 Not this file

- [x] HTTP create/list is CAT, not this phase
- [x] Prices are D21

## D20.4 Exit

- [x] Table exists on `lazuar_pay` via D16 migrator
- [x] Unblocked for D21
