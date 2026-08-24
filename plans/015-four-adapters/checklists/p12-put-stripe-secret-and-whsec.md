# P12 — Stripe PUT requires sk_ and whsec_

**Track:** Provider door · **Depends:** P11, H10  
**Analysis:** [00](../00-what-must-be-done.md) §3.2  
**IDs:** NP-GW-001, NP-GW-009  
**Goal:** Merchant paste matches BYOK verify. UI no longer trains “sk_test_ is the only secret.”

---

## P12.1 Live today

- [x] PUT only stores `secret` as API key
- [x] Webhook verify uses process env

## P12.2 Change

- [x] `provider=stripe`: require `secret` (sk_test_ / sk_live_) and `webhook_secret` (whsec_)
- [x] Protect both (S16)
- [x] `last4` = API key last4
- [x] Rotate: sending a new `webhook_secret` updates ciphertext; omit vs empty — empty 400; omit-on-update may keep existing (document). Prefer **require both on every PUT** for Bar-size honesty (simpler)

## P12.3 Test

- [x] PUT stripe without `webhook_secret` → 400
- [x] PUT both → GET `webhook_configured: true`, no plaintext

## P12.4 Must not

- [x] Do not store `whsec_` in `Ciphertext` mixed with `sk_`
- [x] Do not accept a `whsec_` pasted into the sk_ box as if it were a key

## P12.5 Exit

- [x] Stripe PUT writes both columns
- [x] Unblocked for U11; update `WebhookTests` seed
