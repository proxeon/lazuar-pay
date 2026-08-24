# fc12 — CHIP missing currency does not pay

**Track:** Fill CHIP · **Depends:** D10  
**Analysis:** 09 method 12; C24  
**Goal:** `RailTests.Chip_missing_currency_does_not_pay`

---

## fc12.1

- [ ] Signed `purchase.paid` `total:1000` **no** `currency` (or `""`)
- [ ] 400 `missing currency`, zero documents, checkout `open`

## fc12.2 Must not

- [ ] Do not invent MYR

## fc12.3 Exit

- [ ] Green
