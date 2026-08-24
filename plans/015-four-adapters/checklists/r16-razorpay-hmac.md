# R16 — X-Razorpay-Signature HMAC-SHA256 raw body

**Track:** Razorpay · **Depends:** P21, R11  
**Analysis:** [00](../00-what-must-be-done.md) §5.4; Hub `Utils.verifyWebhookSignature`  
**IDs:** NP-GW-004  
**Goal:** HMAC of **raw JSON bytes** with webhook secret. Not form HMAC, not RSA.

---

## R16.1

- [ ] Header `X-Razorpay-Signature`
- [ ] Missing → 400
- [ ] HMAC-SHA256(rawBody, webhookSecret) hex compare fixed-time
- [ ] Invalid → 400
- [ ] Then JSON parse

## R16.2 Must not

- [ ] Do not use Razorpay.Api `Utils` (that **is** the SDK)
- [ ] Implement HMAC yourself (like Billplz)

## R16.3 Exit

- [ ] Good sig continues; bad sig 400
