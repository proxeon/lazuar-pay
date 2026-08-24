# D20 — Checked-in unit fixtures per rail

**Track:** Units · **Depends:** D10–D14  
**Analysis:** P0-D “lived payload or pin units”  
**IDs:** —  
**Goal:** FakePsp tests read the same numbers a dashboard payload uses. No live HTTP in CI.

---

## D20.1

- [ ] Add `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Fixtures/` (or embed constants next to tests)
- [ ] One JSON (or form) per rail: CHIP paid, Billplz paid form, Xendit PAID, Razorpay captured, Stripe completed
- [ ] Each file comments the unit of `total` / `paid_amount` / `amount` / `amount_total`
- [ ] RailTests/WebhookTests load these instead of inline magic numbers **or** keep inline but match the fixture exactly

## D20.2 Must not

- [ ] Do not `HttpClient` to CHIP in `task pay:test`
- [ ] Do not commit live secrets

## D20.3 Exit

- [ ] Four non-Stripe fixtures exist and are used by at least the happy-path tests
