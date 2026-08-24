# B18 — Form x_signature HMAC

**Track:** Billplz · **Depends:** P21, B11  
**Analysis:** [00](../00-what-must-be-done.md) §5.2  
**IDs:** NP-GW-004  
**Goal:** Body is **form**, not JSON. Field `x_signature`. Steal Hub `ComputeHmac`.

---

## B18.1

- [ ] Read raw body as `application/x-www-form-urlencoded`
- [ ] Parse fields (Hub `QueryHelpers.ParseQuery`)
- [ ] Missing `x_signature` → 400
- [ ] Source string: for each field except `x_signature`, `key+value`, **Ordinal** sort, join with `|`
- [ ] HMAC-SHA256 with webhook secret, hex **lowercase**
- [ ] Compare with `CryptographicOperations.FixedTimeEquals` on lowercase hex bytes (Hub `FixedTimeEqualsHex`)

## B18.2 Must not

- [ ] Do not JSON-parse a Billplz callback
- [ ] Do not use CHIP RSA verifier
- [ ] Do not 500 on bad sig

## B18.3 Exit

- [ ] Helper + B19
- [ ] Unblocked for B20
