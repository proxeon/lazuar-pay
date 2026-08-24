# S11 — gateway_credentials.public_merchant_id

**Track:** Schema · **Depends:** S10  
**Analysis:** [00](../00-what-must-be-done.md) §3.2  
**IDs:** NP-GW-001  
**Goal:** CHIP Brand ID and Billplz Collection ID are not secrets.

---

## S11.1 Column

- [ ] Add nullable `PublicMerchantId` string on `GatewayCredentialRow`
- [ ] Map `public_merchant_id`
- [ ] GET **may** return it (the merchant’s own Brand/Collection id)
- [ ] Null for `stripe` / `xendit` / `razorpay`

## S11.2 Must not

- [ ] Do not encrypt Brand/Collection (not a secret; encrypting hides support)
- [ ] Do not reuse this column for PEM or `whsec_`

## S11.3 Exit

- [ ] Column on the row type
- [ ] Unblocked for C11 / B11 PUT fields
