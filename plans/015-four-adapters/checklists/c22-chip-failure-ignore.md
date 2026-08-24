# C22 — purchase.payment_failure does not fulfill

**Track:** CHIP · **Depends:** C19  
**Analysis:** [00](../00-what-must-be-done.md) §5.1  
**IDs:** —  
**Goal:** Failed pay is not a receipt. Do not consume the paid unique grain.

---

## C22.1

- [x] `purchase.payment_failure` → 200 `{ ignored: "payment_failure" }` (or equivalent)
- [x] No `RCPT-`
- [x] If you insert unique, use `failed:{purchaseId}` not `paid:{purchaseId}`
- [x] A later `purchase.paid` for the same purchase **must still be able to fulfill** (namespace)

## C22.2 Exit

- [x] Test: failure then paid still mints one receipt (if you can send two events)
