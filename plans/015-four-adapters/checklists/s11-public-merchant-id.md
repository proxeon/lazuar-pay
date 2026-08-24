# S11 — gateway_credentials.public_merchant_id

**Track:** Schema · **Depends:** S10  
**Analysis:** [00](../00-what-must-be-done.md) §3.2  
**IDs:** NP-GW-001  
**Goal:** CHIP Brand ID and Billplz Collection ID are not secrets.

---

## S11.1 Column

- [x] Add nullable `PublicMerchantId` string on `GatewayCredentialRow`
- [x] Map `public_merchant_id`
- [x] GET **may** return it (the merchant’s own Brand/Collection id)
- [x] Null for `stripe` / `xendit` / `razorpay`

## S11.2 Must not

- [x] Do not encrypt Brand/Collection (not a secret; encrypting hides support)
- [x] Do not reuse this column for PEM or `whsec_`

## S11.3 Exit

- [x] Column on the row type
- [x] Unblocked for C11 / B11 PUT fields
