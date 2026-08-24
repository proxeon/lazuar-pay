# E11 — Development empty ciphertext is 503

**Track:** Env secrets · **Depends:** E10  
**Analysis:** P0-E leftover rows  
**IDs:** H11  
**Goal:** `ASPNETCORE_ENVIRONMENT=Development` with platform `whsec_` cannot forge Ada’s Stripe.

---

## E11.1

- [ ] Test or documented manual: environment Development, `Pay:StripeWebhookSecret` set, row `WebhookCiphertext` null, signed with the process secret → **503**, not 200
- [ ] Factory stays Testing for the rest of the suite (E16)

## E11.2 Must not

- [ ] Do not `UseEnvironment("Development")` on `PayApiFactory` globally

## E11.3 Exit

- [ ] `WebhookTests.Development_missing_org_whsec_is_503_even_if_process_env_set` **or** a dedicated factory subclass
