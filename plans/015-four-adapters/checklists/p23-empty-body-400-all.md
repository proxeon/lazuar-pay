# P23 — Empty webhook body is 400 on every rail

**Track:** Provider door · **Depends:** P21  
**Analysis:** [00](../00-what-must-be-done.md) §5  
**IDs:** NP-GW-005  
**Goal:** Stripe already 400s empty body (`PublicPayTests`). CHIP/Billplz/Xendit/Razorpay must too.

---

## P23.1

- [ ] Read raw body **before** JSON/form parse
- [ ] Whitespace-only → 400 `"empty body"`
- [ ] Do this **before** signature verify (no 500 on empty + missing sig)
- [ ] Same status for all five names on the allow-list

## P23.2 Tests

- [ ] Keep existing empty-body test for stripe
- [ ] When each rail lands, add empty-body 400 (C26, B28, X23, R25)

## P23.3 Must not

- [ ] Do not 500 (Hub history)
- [ ] Do not 200 ignore empty

## P23.4 Exit

- [ ] Shared empty-body check in the webhook handler
- [ ] Unblocked for C26
