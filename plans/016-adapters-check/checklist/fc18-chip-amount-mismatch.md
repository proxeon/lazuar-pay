# fc18 — CHIP amount mismatch does not pay

**Track:** Fill CHIP · **Depends:** D10, D17  
**Analysis:** 09 method 18  
**Goal:** `RailTests.Chip_amount_mismatch_does_not_pay`

---

## fc18.1

- [ ] Checkout 10. Signed paid `total: 999` MYR
- [ ] 400, zero documents, **no** event row
- [ ] `total` is cents (D10)

## fc18.2 Exit

- [ ] Green
