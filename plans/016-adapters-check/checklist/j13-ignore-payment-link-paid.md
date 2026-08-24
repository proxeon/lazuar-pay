# J13 — `payment_link.paid` is not cash

**Track:** Razorpay join · **Depends:** J10  
**Analysis:** [`../08-razorpay-crosscheck.md`](../08-razorpay-crosscheck.md); Hub honors `payment.captured`  
**IDs:** —  
**Goal:** Merchants who enabled the wrong dashboard event must not mint `RCPT-` twice or early.

---

## J13.1 Live today

- [ ] Non-`payment.captured` (except failed) → ignored, EventId = header or event type

## J13.2

- [ ] Explicitly ignore `payment_link.paid` / `payment_link.expired` as ignored grains (`plinkpaid:{id}` or header)
- [ ] Do **not** use this event as the cash fulfill even if notes are present
- [ ] Join fallback (J11) is for **captured** payloads only

## J13.3 Exit

- [ ] `RailTests.Razorpay_payment_link_paid_is_ignored` — 200 ignored, zero documents
