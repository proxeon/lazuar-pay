# S10 — gateway_credentials.webhook_ciphertext

**Track:** Schema · **Depends:** A00  
**Analysis:** [00](../00-what-must-be-done.md) §3.2  
**IDs:** NP-GW-001  
**Goal:** Per-org webhook signing secret, encrypted. Closes the platform-`whsec` hole for every rail.

---

## S10.1 Column

- [ ] Add nullable `WebhookCiphertext` on `GatewayCredentialRow` (`apps/lazuar-pay/src/Lazuar.Pay/Data/Rows.cs`)
- [ ] Map `gateway_credentials.webhook_ciphertext` text null in `PayDbContext`
- [ ] Encrypt with existing `SecretBox.Protect` / `Unprotect` (`Secrets/SecretBox.cs`)
- [ ] Never log plaintext; never return on GET (S18)

## S10.2 Stripe meaning

- [ ] This column is Stripe `whsec_…` (Dashboard **endpoint** signing secret), **not** `sk_`
- [ ] Existing `Ciphertext` remains the API key (`sk_` / CHIP Bearer / Billplz secret / Xendit secret / Razorpay `key_id:key_secret`)

## S10.3 Must not

- [ ] Do not leave `Pay:StripeWebhookSecret` as the only source of truth after H10
- [ ] Do not put `whsec_` in Vite `VITE_*`

## S10.4 Exit

- [ ] Property exists on the row type (S17 lands the migrator)
- [ ] Unblocked for S11, S16, H10
