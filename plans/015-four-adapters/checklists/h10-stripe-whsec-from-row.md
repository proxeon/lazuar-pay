# H10 — Stripe verify uses org webhook ciphertext

**Track:** Harden · **Depends:** S10, S16, S17  
**Analysis:** [00](../00-what-must-be-done.md) §3.2 / §3.6; [014/08](../../014-evals/08-webhooks-secrets-fulfillment.md)  
**IDs:** NP-GW-004  
**Goal:** Plane B Stripe signature uses **that org’s** `whsec_`, not only process env.

---

## H10.1 Live today (must change)

- [x] `WebhookEndpoints.cs` currently reads `config["Pay:StripeWebhookSecret"]` for every org
- [x] Load `GatewayCredentialRow` for `(orgId, "stripe")`
- [x] `SecretBox.Unprotect(row.WebhookCiphertext)` → `EventUtility.ValidateSignature` / `ConstructEvent`
- [x] Missing org webhook secret: follow H11 (dev fallback vs Production 503)
- [x] Missing Stripe-Signature header → 400

## H10.2 Must not

- [x] Do not verify with `sk_` (`COMMIT_EDITMSG` on `ee2db8e5` was right: `sk_` is not a signing secret)
- [x] Do not use Hub tenant `DecryptOrPlaintext`
- [x] Do not share one `whsec_` across orgs in Production

## H10.3 Test

- [x] Existing `WebhookTests` seed PUT must also send `webhook_secret` (P12) or tests break — update factory seed
- [x] Signed event still 200 + `RCPT-`

## H10.4 Exit

- [x] Org row is the SoT for Stripe verify
- [x] Unblocked for H11, H19
