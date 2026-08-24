# R18 — payment.failed does not fulfill

**Track:** Razorpay · **Depends:** R17  
**Analysis:** Hub `IsPaymentFailedEvent`  
**IDs:** —  
**Goal:** Failed capture is not a receipt. Do not share EventId with captured.

---

## R18.1

- [x] `payment.failed` → 200 ignored
- [x] If unique inserted, use failed-namespace (R19), never bare `pay_`
- [x] Later `payment.captured` for the same pay_ **must still fulfill** if Razorpay can emit both — namespace protects

## R18.2 Exit

- [x] Test ignore
