# B18 — Form x_signature HMAC

**Track:** Billplz · **Depends:** P21, B11  
**Analysis:** [00](../00-what-must-be-done.md) §5.2  
**IDs:** NP-GW-004  
**Goal:** Body is **form**, not JSON. Field `x_signature`. Steal Hub `ComputeHmac`.

---

## B18.1

- [x] Read raw body as `application/x-www-form-urlencoded`
- [x] Parse fields (Hub `QueryHelpers.ParseQuery`)
- [x] Missing `x_signature` → 400
- [x] Source string: for each field except `x_signature`, `key+value`, **Ordinal** sort, join with `|`
- [x] HMAC-SHA256 with webhook secret, hex **lowercase**
- [x] Compare with `CryptographicOperations.FixedTimeEquals` on lowercase hex bytes (Hub `FixedTimeEqualsHex`)

## B18.2 Must not

- [x] Do not JSON-parse a Billplz callback
- [x] Do not use CHIP RSA verifier
- [x] Do not 500 on bad sig

## B18.3 Exit

- [x] Helper + B19
- [x] Unblocked for B20
