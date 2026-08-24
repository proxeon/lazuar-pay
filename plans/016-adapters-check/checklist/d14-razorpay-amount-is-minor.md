# D14 — Razorpay payment `amount` is already minor

**Track:** Units · **Depends:** A00  
**Analysis:** live `GetInt64`; create sends `ToMinor`  
**IDs:** R21  
**Goal:** Do not `ToMinor` the webhook amount.

---

## D14.1

- [ ] Comment on parse: paise/sen already. RM10.00 → 1000
- [ ] fr22 mismatch uses `amount: 999` not `9.99`

## D14.2 Must not

- [ ] Do not book JSON `tax` / `fee` (existing)

## D14.3 Exit

- [ ] Comment exists
