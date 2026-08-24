# J14 — `order.paid` is not cash

**Track:** Razorpay join · **Depends:** J13  
**Analysis:** 016 refuse: do not fulfill `order.paid` blindly  
**IDs:** —  
**Goal:** Same as J13 for the Orders API event.

---

## J14.1

- [ ] `event == order.paid` → ignored, zero documents
- [ ] May share the J13 test method as a second POST with a new payload (one method, two events) — or `Razorpay_order_paid_is_ignored`

## J14.2 Must not

- [ ] Do not add Orders HTTP create in this program

## J14.3 Exit

- [ ] Hermetic ignore
