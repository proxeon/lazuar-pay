# W20 — Missing One webhook secret is 503

**Track:** One HMAC · **Depends:** W10  
**Analysis:** live already 503s if `Pay:OneWebhookSecret` empty  
**IDs:** —  
**Goal:** Keep fail-closed. Do not fall back to Stripe `whsec_`.

---

## W20.1 Live today (keep)

- [ ] Empty `Pay:OneWebhookSecret` → 503 `"One webhook secret missing"`
- [ ] Do not verify with `Pay:StripeWebhookSecret`

## W20.2 Exit

- [ ] Existing behaviour remains; add `OneWebhookTests.Missing_secret_is_503` if absent
