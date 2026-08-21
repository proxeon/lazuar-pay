# G19 — Verify PSP signature

**Track:** Rails · **Depends:** G18  
**Analysis:** [06](../06-money-rails.md) §5.3 / §2.7  
**IDs:** NP-GW-004  
**Goal:** Bad sig is 4xx. `NP-GW-004`. Chosen rail only.

---

## G19.1 Chosen rail (live Hub adapter judgment)

- [x] Decrypt **that** org’s webhook secret (G11/G12). Missing config → **400**, not 500
- [x] If Stripe: `Stripe-Signature` + `EventUtility.ConstructEvent` (do not roll your own HMAC)
- [x] If CHIP: `X-Signature` RSA PKCS#1 v1.5 SHA256 over **raw** body (PEM), as `ChipCollectGatewayAdapter` does
- [x] Verify **only** the G10 rail. No Razorpay / Xendit / Billplz verify “for later”

## G19.2 Fail closed

- [x] Bad or missing signature → **4xx** (**400**, not 500). **Do not 200**
- [x] Hub signature-fail 500 is a lie — do not copy (Stripe retry storm)
- [x] Do not JSON re-serialize before verify

## G19.3 Must not

- [x] No Plane A HMAC on this route. No Bearer
- [x] Do not 200 an unverified body

## G19.4 Exit

- [x] `NP-GW-004` may move when G25 (or this commit) has a bad-sig test
- [x] Unblocked for G25
