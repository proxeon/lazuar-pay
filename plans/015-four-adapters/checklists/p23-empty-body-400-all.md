# P23 — Empty webhook body is 400 on every rail

**Track:** Provider door · **Depends:** P21  
**Analysis:** [00](../00-what-must-be-done.md) §5  
**IDs:** NP-GW-005  
**Goal:** Stripe already 400s empty body (`PublicPayTests`). CHIP/Billplz/Xendit/Razorpay must too.

---

## P23.1

- [x] Read raw body **before** JSON/form parse
- [x] Whitespace-only → 400 `"empty body"`
- [x] Do this **before** signature verify (no 500 on empty + missing sig)
- [x] Same status for all five names on the allow-list

## P23.2 Tests

- [x] Keep existing empty-body test for stripe
- [x] When each rail lands, add empty-body 400 (C26, B28, X23, R25)

## P23.3 Must not

- [x] Do not 500 (Hub history)
- [x] Do not 200 ignore empty

## P23.4 Exit

- [x] Shared empty-body check in the webhook handler
- [x] Unblocked for C26
