# H11 — Process env is Stripe dev fallback only

**Track:** Harden · **Depends:** H10  
**Analysis:** [00](../00-what-must-be-done.md) §3.2  
**IDs:** NP-GW-001  
**Goal:** `Pay:StripeWebhookSecret` may help one-org local dogfood. Production must not forge every org with one secret.

---

## H11.1 Rules

- [ ] If org `WebhookCiphertext` is present → **always** use it
- [ ] If missing and environment is `Testing` or `Development` → may use `Pay:StripeWebhookSecret`
- [ ] If missing and environment is `Production` → **503** `"webhook secret missing"` (do not verify with a platform secret)
- [ ] Empty process env + empty row → 503 when the rail is configured (keep today’s “missing secret 503” for the no-fallback case)

## H11.2 Test

- [ ] Testing factory can still set `Pay:StripeWebhookSecret` for hermetic tests **or** PUT a `whsec_` on the row (prefer the row after P12)
- [ ] Document in host README: Production requires per-org `whsec_`

## H11.3 Must not

- [ ] Do not 200 unsigned events when secret is missing
- [ ] Do not 500 on missing secret (503 or 400 only)

## H11.4 Exit

- [ ] Production path cannot verify with only process env
- [ ] Unblocked for H12
