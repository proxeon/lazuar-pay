# M12 — Show webhook_configured, never the secret

**Track:** Merchant · **Depends:** A00  
**Analysis:** GET already returns `webhook_configured`; SPA ignores it  
**IDs:** S18  
**Goal:** Ada can see “webhook secret on file” after reload.

---

## M12.1

- [ ] If GET `webhook_configured === true`, show copy “Webhook secret on file” (not the value)
- [ ] Input stays empty after save (already cleared)

## M12.2 Must not

- [ ] Do not render ciphertext, last4 of PEM, or `whsec_` prefix from GET (GET has no secret)

## M12.3 Exit

- [ ] UI branch exists
