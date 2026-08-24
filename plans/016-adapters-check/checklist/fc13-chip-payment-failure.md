# fc13 — CHIP payment_failure is ignored

**Track:** Fill CHIP · **Depends:** S13  
**Analysis:** 09 method 13; C22  
**Goal:** `RailTests.Chip_payment_failure_is_ignored`

---

## fc13.1

- [ ] Signed `event_type: purchase.payment_failure`, purchase id `purch_fail`
- [ ] 200, body contains `payment_failure`, zero documents

## fc13.2 Exit

- [ ] Green
- [ ] Unblocked for fc14
