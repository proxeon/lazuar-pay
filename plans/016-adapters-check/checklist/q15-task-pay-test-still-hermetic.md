# Q15 — `task pay:test` stays hermetic

**Track:** Hygiene · **Depends:** G14, F00  
**Goal:** No live CHIP/Billplz/Xendit/Razorpay/Zitadel in CI.

---

## Q15.1

- [ ] Fake One + FakePsp still replaced in `PayApiFactory`
- [ ] D20 fixtures are files, not HTTP
- [ ] No `[Ignore]` for “needs live keys”

## Q15.2 Must not

- [ ] Do not add Stripe.net start against `sk_test_` in CI

## Q15.3 Exit

- [ ] `task pay:test` green on the branch
