# E12 — Production empty ciphertext is 503 even if process env set

**Track:** Env secrets · **Depends:** E10  
**Analysis:** [`../09-tests-inventory.md`](../09-tests-inventory.md) §10.1 method 6  
**IDs:** H11  
**Goal:** BYOK in Production is the row or nothing.

---

## E12.1 Method `WebhookTests.Production_missing_org_whsec_is_503_even_if_process_env_set`

- [ ] `UseEnvironment("Production")`
- [ ] `Pay:WrapKey` = 32-byte base64 (SecretBox required)
- [ ] `Pay:StripeWebhookSecret` = `whsec_process`
- [ ] PUT stripe then **null** `WebhookCiphertext`
- [ ] Sign with `whsec_process`, POST completed-shaped body
- [ ] Assert **503**, not 200

## E12.2 Must not

- [ ] Do not hit live Stripe
- [ ] Do not skip because “Production boot is hard” without listing the extra keys you needed

## E12.3 Exit

- [ ] Green or a comment listing blocking Production config — do not silent-skip
