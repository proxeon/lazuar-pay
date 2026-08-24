# B11 — PUT billplz fields

**Track:** Billplz · **Depends:** B10, P11, S11, S12  
**Analysis:** [00](../00-what-must-be-done.md) §5.2  
**IDs:** NP-GW-001  
**Goal:** Secret + Collection ID + X-Signature secret + environment.

---

## B11.1

- [ ] Require `secret` (API key), `webhook_secret` (X-Signature secret — store separately even if often equal), `public_merchant_id` (Collection ID), `environment` `test`|`live`
- [ ] Encrypt secret + webhook_secret
- [ ] Collection ID plaintext
- [ ] Sets `active_provider=billplz`
- [ ] Writer only

## B11.2 Must not

- [ ] Do not infer environment from hostname
- [ ] Do not skip webhook_secret because “it’s the same as secret” — still two fields (merchant can paste twice)

## B11.3 Exit

- [ ] PUT round-trip
- [ ] Unblocked for B12
