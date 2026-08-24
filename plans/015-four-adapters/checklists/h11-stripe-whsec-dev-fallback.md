# H11 — Process env is Stripe dev fallback only

**Track:** Harden · **Depends:** H10  
**Analysis:** [00](../00-what-must-be-done.md) §3.2  
**IDs:** NP-GW-001  
**Goal:** `Pay:StripeWebhookSecret` may help one-org local dogfood. Production must not forge every org with one secret.

---

## H11.1 Rules

- [x] If org `WebhookCiphertext` is present → **always** use it
- [x] If missing and environment is `Testing` or `Development` → may use `Pay:StripeWebhookSecret`
- [x] If missing and environment is `Production` → **503** `"webhook secret missing"` (do not verify with a platform secret)
- [x] Empty process env + empty row → 503 when the rail is configured (keep today’s “missing secret 503” for the no-fallback case)

## H11.2 Test

- [x] Testing factory can still set `Pay:StripeWebhookSecret` for hermetic tests **or** PUT a `whsec_` on the row (prefer the row after P12)
- [x] Document in host README: Production requires per-org `whsec_`

## H11.3 Must not

- [x] Do not 200 unsigned events when secret is missing
- [x] Do not 500 on missing secret (503 or 400 only)

## H11.4 Exit

- [x] Production path cannot verify with only process env
- [x] Unblocked for H12
