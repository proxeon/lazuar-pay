# C11 — PUT chip: Bearer + Brand ID + PEM

**Track:** CHIP · **Depends:** C10, P11, S11  
**Analysis:** [00](../00-what-must-be-done.md) §5.1  
**IDs:** NP-GW-001, NP-GW-009  
**Goal:** Store the three CHIP pieces Hub split across ApiKey / MerchantId / WebhookSecret.

---

## C11.1

- [ ] `provider=chip`: require `secret` (Bearer / secret key), `public_merchant_id` (Brand ID), `webhook_secret` (PEM including headers)
- [ ] Encrypt secret + PEM (S16)
- [ ] Store Brand ID in `PublicMerchantId` plaintext
- [ ] Writer only (H18)
- [ ] Sets `active_provider=chip` (P13)
- [ ] GET may show Brand ID + last4 of API key + `webhook_configured`

## C11.2 Test

- [ ] PUT chip missing Brand ID → 400 (C31)
- [ ] PUT chip missing PEM → 400
- [ ] Member PUT → 403

## C11.3 Must not

- [ ] Do not auto-register webhooks (C28)
- [ ] Do not treat Brand ID as a secret

## C11.4 Exit

- [ ] Chip row round-trips
- [ ] Unblocked for C12
