# D22 — `gateway_credentials`

**Track:** Database · **Depends:** D16  
**Analysis:** [03](../03-host-production-seams.md), [09](../09-data-migration.md)  
**Goal:** NP-GW-001. Encrypted BYOK column. Not plaintext `sk_live`.

---

## D22.1 Table

- [x] `gateway_credentials`: `org_id` + `provider`, `ciphertext`, last4 / metadata, `updated_at`
- [x] Unique enough to rotate/disable without a second org table
- [x] Never store plaintext `sk_live_` / CHIP Bearer / Billplz secret in this table

## D22.2 Encryption

- [x] This phase is the **column**. Encryption helper is **G11** — do not invent Hub `AesSecretVault` here
- [x] Do not copy Hub `Kms__MasterKey` / `Jwt:Secret` fallback
- [x] GET later returns last4 / provider only (G13). Column shape must allow that

## D22.3 Refuse

- [x] Not One `lzr_sk_`
- [x] Not `payments.TenantPaymentConfigurations` ciphertext copied across
- [x] No Vite env for these secrets

## D22.4 Exit

- [x] Table exists; no plaintext live key column
- [x] Unblocked for D23
