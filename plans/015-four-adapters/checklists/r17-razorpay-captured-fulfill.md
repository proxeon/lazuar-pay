# R17 — payment.captured fulfills

**Track:** Razorpay · **Depends:** R16, H12  
**Analysis:** [00](../00-what-must-be-done.md) §5.4  
**IDs:** NP-FUL-001  
**Goal:** Only `event == payment.captured`. Checkout id from `notes`.

---

## R17.1

- [x] Read `event`
- [x] `payload.payment.entity` amount cents / 100, currency, notes, id `pay_`
- [x] `FulfillPaidAsync(..., "razorpay", paymentId)`
- [x] H13 org bind; H14 amount match

## R17.2 Must not

- [x] Do not fulfill `order.paid` in this program unless A00 amended (stick to payment.captured)
- [x] Do not book `fee` / `tax` JSON (R21)

## R17.3 Exit

- [x] Fixture → `RCPT-`
