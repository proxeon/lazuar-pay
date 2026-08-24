# I16 — Stripe Session Idempotency-Key

**Track:** Idempotent start · **Depends:** I10  
**Analysis:** [`../04-stripe-crosscheck.md`](../04-stripe-crosscheck.md) §13.2 item 7  
**IDs:** —  
**Goal:** Belt for the first create. Does not replace I10 (Stripe.net is unstubbed in CI).

---

## I16.1

- [ ] `StripeHosted.CreateHostedUrlAsync` passes `IdempotencyKey = "lazuar-checkout:" + checkout.Id` (or equivalent RequestOptions)
- [ ] Same checkout cannot mint two payable `cs_` if two first-creates race before I10 persist

## I16.2 Must not

- [ ] Do not treat this as I10 proof — G14 uses FakePsp rails, not Stripe.net
- [ ] Do not add a Stripe HTTP seam in this phase unless you already have one
- [ ] Do not set `Mode = "setup"`

## I16.3 Exit

- [ ] Source grep / comment on `StripeHosted` shows the key
- [ ] Optional unit of `SessionCreateOptions` if you extract it; not required to hit live Stripe
