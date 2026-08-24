# P11 — PUT body grows per-rail fields

**Track:** Provider door · **Depends:** S17, P10  
**Analysis:** [00](../00-what-must-be-done.md) §3.4 / §5  
**IDs:** NP-GW-009  
**Goal:** `PutGatewayRequest` can carry the four rails’ secrets without a Hub config DTO.

---

## P11.1 Request JSON (snake_case)

- [ ] `provider` (required)
- [ ] `secret` (API key; required for all five)
- [ ] `webhook_secret` (required for all five in this program — Stripe `whsec_`, CHIP PEM, Billplz X-Signature, Xendit callback token, Razorpay webhook secret)
- [ ] `public_merchant_id` (required for `chip` and `billplz`; forbidden/ignored for others)
- [ ] `environment` (`test`|`live`; required for `billplz`; optional others, default `test`)

## P11.2 Validation (per name, even if class lands later)

- [ ] `chip`: `public_merchant_id` required (C31)
- [ ] `billplz`: `public_merchant_id` + `environment` required (B27, B12)
- [ ] `stripe` / `xendit` / `razorpay`: reject non-empty `public_merchant_id` **or** ignore — pick reject (400) so merchants do not think Brand ID applies
- [ ] Empty `secret` or empty `webhook_secret` → 400

## P11.3 Must not

- [ ] Do not accept Hub `gatewayType` uppercase-only
- [ ] Do not put these fields in Vite env

## P11.4 Exit

- [ ] Request type exists
- [ ] Unblocked for P12, C11, B11, X11, R11
