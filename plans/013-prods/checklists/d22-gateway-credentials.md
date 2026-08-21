# D22 — `gateway_credentials`

**Track:** Database · **Depends:** D16  
**Analysis:** [03](../03-host-production-seams.md), [09](../09-data-migration.md)  
**Goal:** NP-GW-001. Encrypted BYOK column. Not plaintext `sk_live`.

---

## D22.1 Table

- [ ] `gateway_credentials`: `org_id` + `provider`, `ciphertext`, last4 / metadata, `updated_at`
- [ ] Unique enough to rotate/disable without a second org table
- [ ] Never store plaintext `sk_live_` / CHIP Bearer / Billplz secret in this table

## D22.2 Encryption

- [ ] This phase is the **column**. Encryption helper is **G11** — do not invent Hub `AesSecretVault` here
- [ ] Do not copy Hub `Kms__MasterKey` / `Jwt:Secret` fallback
- [ ] GET later returns last4 / provider only (G13). Column shape must allow that

## D22.3 Refuse

- [ ] Not One `lzr_sk_`
- [ ] Not `payments.TenantPaymentConfigurations` ciphertext copied across
- [ ] No Vite env for these secrets

## D22.4 Exit

- [ ] Table exists; no plaintext live key column
- [ ] Unblocked for D23
