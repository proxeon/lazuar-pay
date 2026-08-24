# X11 — PUT xendit: secret + callback token

**Track:** Xendit · **Depends:** X10, P11  
**Analysis:** [00](../00-what-must-be-done.md) §5.3  
**IDs:** NP-GW-001  
**Goal:** No Brand ID. Callback token is the webhook secret.

---

## X11.1

- [ ] Require `secret` (Xendit secret key) and `webhook_secret` (`x-callback-token` value)
- [ ] Reject `public_merchant_id` if sent (P11)
- [ ] Encrypt both
- [ ] `active_provider=xendit`
- [ ] Writer only

## X11.2 Exit

- [ ] PUT round-trip
- [ ] Unblocked for X12
