# U15 — Razorpay fields

**Track:** Merchant UI · **Depends:** U10, R11  
**Analysis:** [00](../00-what-must-be-done.md) §5.4  
**IDs:** —  
**Goal:** key_id, key_secret, webhook secret. No e-mandate label.

---

## U15.1

- [x] Key ID
- [x] Key secret
- [x] Webhook secret
- [x] PUT joins key_id:key_secret (R11)
- [x] Copy: hosted payment link; **not** e-mandate (R22)
- [x] Webhook URL hint: `/v1/webhooks/razorpay/{orgId}`

## U15.2 Must not

- [x] Do not label the rail “e-mandate” (008 lie)

## U15.3 Exit

- [x] Three fields + honest copy
