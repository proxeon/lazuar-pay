# X14 — x-callback-token fixed-time compare

**Track:** Xendit · **Depends:** P21, X11  
**Analysis:** [00](../00-what-must-be-done.md) §5.3; Hub `VerifyCallbackToken`  
**IDs:** NP-GW-004  
**Goal:** Header equals stored token. Not HMAC, not RSA.

---

## X14.1

- [ ] Header `x-callback-token` (case-insensitive)
- [ ] Unprotect org `WebhookCiphertext`
- [ ] UTF-8 bytes, length check, `CryptographicOperations.FixedTimeEquals`
- [ ] Missing / mismatch → 400
- [ ] Empty body still 400 first (P23)

## X14.2 Must not

- [ ] Do not 500
- [ ] Do not use Stripe EventUtility

## X14.3 Exit

- [ ] Tests: good token continues; bad token 400
