# fc14 — CHIP failure then paid still one receipt

**Track:** Fill CHIP · **Depends:** fc13  
**Analysis:** 09 method 14; namespaced `failed:` vs `paid:`  
**Goal:** `RailTests.Chip_failure_then_paid_still_mints_one_receipt`

---

## fc14.1

- [ ] Same purchase id: `purchase.payment_failure` then `purchase.paid` total 1000 MYR + checkout metadata
- [ ] One `RCPT-` after the second POST

## fc14.2 Must not

- [ ] Do not use bare purchase id as both EventIds

## fc14.3 Exit

- [ ] Green
