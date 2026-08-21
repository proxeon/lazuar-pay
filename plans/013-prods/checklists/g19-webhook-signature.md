# G19 — Verify PSP signature

**Track:** Rails · **Depends:** G18  
**Analysis:** [06](../06-money-rails.md) §5.3 / §2.7  
**IDs:** NP-GW-004  
**Goal:** Bad sig is 4xx. `NP-GW-004`. Chosen rail only.

---

## G19.1 Chosen rail (live Hub adapter judgment)

- [ ] Decrypt **that** org’s webhook secret (G11/G12). Missing config → **400**, not 500
- [ ] If Stripe: `Stripe-Signature` + `EventUtility.ConstructEvent` (do not roll your own HMAC)
- [ ] If CHIP: `X-Signature` RSA PKCS#1 v1.5 SHA256 over **raw** body (PEM), as `ChipCollectGatewayAdapter` does
- [ ] Verify **only** the G10 rail. No Razorpay / Xendit / Billplz verify “for later”

## G19.2 Fail closed

- [ ] Bad or missing signature → **4xx** (**400**, not 500). **Do not 200**
- [ ] Hub signature-fail 500 is a lie — do not copy (Stripe retry storm)
- [ ] Do not JSON re-serialize before verify

## G19.3 Must not

- [ ] No Plane A HMAC on this route. No Bearer
- [ ] Do not 200 an unverified body

## G19.4 Exit

- [ ] `NP-GW-004` may move when G25 (or this commit) has a bad-sig test
- [ ] Unblocked for G25
