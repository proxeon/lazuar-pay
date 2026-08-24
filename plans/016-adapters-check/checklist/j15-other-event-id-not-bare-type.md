# J15 — Ignored Razorpay EventId is not bare event type

**Track:** Razorpay join · **Depends:** A00  
**Analysis:** [`../08-razorpay-crosscheck.md`](../08-razorpay-crosscheck.md) P1-11; 015 R19  
**IDs:** —  
**Goal:** Two ignored `payment.authorized` deliveries without a header must not collide on EventId `payment.authorized`.

---

## J15.1 Live today

- [ ] Non-captured, non-failed: `headerEventId ?? eventType ?? "razorpay"` — bare type

## J15.2 Change

- [ ] Prefer `X-Razorpay-Event-Id` header
- [ ] Else `"{event}:{paymentId}"` if payment id present
- [ ] Else `"{event}:none"` — never a second event’s paid grain
- [ ] Failed already uses `failed:{pay_}` — keep
- [ ] Captured already uses `captured:{pay_}` or header — keep

## J15.3 Must not

- [ ] Do not use bare `pay_` as EventId (fail-then-pay collision)

## J15.4 Exit

- [ ] `RailTests.Razorpay_event_id_prefers_header` (fr16) still required
