# E16 — Existing missing-whsec 503 still true in Testing

**Track:** Env secrets · **Depends:** E10  
**Analysis:** `WebhookTests.Missing_webhook_secret_is_503_when_rail_configured`  
**IDs:** H10  
**Goal:** E10 must not break the factory test (factory is Testing, so fallback still exists — the test **nulls** ciphertext **and** sets process secret `""`).

---

## E16.1 Live test (keep)

- [ ] Factory `StripeWebhookSecret = ""`, PUT, null ciphertext, POST unsigned body → 503
- [ ] After E10 this remains 503 (Testing fallback is empty)

## E16.2

- [ ] Re-run this method after E10
- [ ] Do not delete it

## E16.3 Exit

- [ ] Still green
