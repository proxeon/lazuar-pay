# C11 — PUT chip: Bearer + Brand ID + PEM

**Track:** CHIP · **Depends:** C10, P11, S11  
**Analysis:** [00](../00-what-must-be-done.md) §5.1  
**IDs:** NP-GW-001, NP-GW-009  
**Goal:** Store the three CHIP pieces Hub split across ApiKey / MerchantId / WebhookSecret.

---

## C11.1

- [x] `provider=chip`: require `secret` (Bearer / secret key), `public_merchant_id` (Brand ID), `webhook_secret` (PEM including headers)
- [x] Encrypt secret + PEM (S16)
- [x] Store Brand ID in `PublicMerchantId` plaintext
- [x] Writer only (H18)
- [x] Sets `active_provider=chip` (P13)
- [x] GET may show Brand ID + last4 of API key + `webhook_configured`

## C11.2 Test

- [x] PUT chip missing Brand ID → 400 (C31)
- [x] PUT chip missing PEM → 400
- [x] Member PUT → 403

## C11.3 Must not

- [x] Do not auto-register webhooks (C28)
- [x] Do not treat Brand ID as a secret

## C11.4 Exit

- [x] Chip row round-trips
- [x] Unblocked for C12
