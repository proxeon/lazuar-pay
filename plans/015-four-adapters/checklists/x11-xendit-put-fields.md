# X11 — PUT xendit: secret + callback token

**Track:** Xendit · **Depends:** X10, P11  
**Analysis:** [00](../00-what-must-be-done.md) §5.3  
**IDs:** NP-GW-001  
**Goal:** No Brand ID. Callback token is the webhook secret.

---

## X11.1

- [x] Require `secret` (Xendit secret key) and `webhook_secret` (`x-callback-token` value)
- [x] Reject `public_merchant_id` if sent (P11)
- [x] Encrypt both
- [x] `active_provider=xendit`
- [x] Writer only

## X11.2 Exit

- [x] PUT round-trip
- [x] Unblocked for X12
