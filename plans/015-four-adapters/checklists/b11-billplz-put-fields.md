# B11 — PUT billplz fields

**Track:** Billplz · **Depends:** B10, P11, S11, S12  
**Analysis:** [00](../00-what-must-be-done.md) §5.2  
**IDs:** NP-GW-001  
**Goal:** Secret + Collection ID + X-Signature secret + environment.

---

## B11.1

- [x] Require `secret` (API key), `webhook_secret` (X-Signature secret — store separately even if often equal), `public_merchant_id` (Collection ID), `environment` `test`|`live`
- [x] Encrypt secret + webhook_secret
- [x] Collection ID plaintext
- [x] Sets `active_provider=billplz`
- [x] Writer only

## B11.2 Must not

- [x] Do not infer environment from hostname
- [x] Do not skip webhook_secret because “it’s the same as secret” — still two fields (merchant can paste twice)

## B11.3 Exit

- [x] PUT round-trip
- [x] Unblocked for B12
