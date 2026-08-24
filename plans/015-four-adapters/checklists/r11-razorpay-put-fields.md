# R11 — PUT razorpay: key_id:key_secret + webhook secret

**Track:** Razorpay · **Depends:** R10, P11  
**Analysis:** [00](../00-what-must-be-done.md) §5.4  
**IDs:** NP-GW-001  
**Goal:** Store API as Hub did (`keyId:keySecret`) plus webhook secret.

---

## R11.1

- [x] Accept either `secret` = `key_id:key_secret` **or** two fields `key_id` + `key_secret` joined with `:` before Protect
- [x] Require `webhook_secret` (Razorpay dashboard webhook secret)
- [x] Reject `public_merchant_id`
- [x] `active_provider=razorpay`
- [x] Writer only
- [x] last4 = last4 of key_id (or of secret) — document which

## R11.2 Exit

- [x] PUT round-trip
- [x] Unblocked for R12
