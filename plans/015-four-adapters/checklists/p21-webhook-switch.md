# P21 — Webhook switches on known provider names

**Track:** Provider door · **Depends:** H12, P10  
**Analysis:** [00](../00-what-must-be-done.md) §3.4  
**IDs:** NP-API-002  
**Goal:** One route `POST /v1/webhooks/{provider}/{orgId}`. Parse per rail. Same fulfill.

---

## P21.1

- [x] Keep the route (already mapped)
- [x] `switch` on normalized provider
- [x] Each arm: verify with **that org’s** webhook ciphertext, parse event id + checkout id, then **same** H12 TX + `FulfillPaidAsync(checkoutId, provider, providerRef)`
- [x] Stripe arm stays EventUtility
- [x] CHIP/Billplz/Xendit/Razorpay arms land with those tracks — stub 400 `"rail not implemented"` is OK only if the name is **not** on the PUT allow-list yet
- [x] No Bearer on this route

## P21.2 Must not

- [x] Do not `IMediator.Send(ProcessGatewayWebhookCommand)`
- [x] Do not share a verifier between CHIP RSA, Billplz form HMAC, Xendit token, Razorpay HMAC, Stripe
- [x] Do not use `/v1/one/webhooks` (Plane A)

## P21.3 Exit

- [x] Switch exists; Stripe still green
- [x] Unblocked for C18, B18, X14, R16
