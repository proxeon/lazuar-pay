# G11 — Encrypt BYOK keys at rest

**Track:** Rails · **Depends:** D22  
**Analysis:** [06](../06-money-rails.md) §4  
**IDs:** NP-GW-001  
**Goal:** Ciphertext in Pay DB. Wrap key in Pay env. `NP-GW-001`.

---

## G11.1 Wrap key (Pay env, not Vite)

- [x] Wrap key in **Pay process env** (`Pay:DataKey` / equivalent). Not Vite. Not `VITE_*`
- [x] Not Hub `Kms:MasterKey`. Not `Jwt:Secret` fallback. Missing key → refuse save (or refuse boot)
- [x] `:5178` / `:5179` stay `VITE_PAY_API_URL` only — no `VITE_STRIPE_*` / `VITE_CHIP_*` / `VITE_KMS_*`

## G11.2 At rest

- [x] D22 `gateway_credentials` (or the D22 name) stores ciphertext per `org_id` + provider
- [x] Encrypt the provider secret (Stripe `sk_…` / CHIP Bearer). Never persist plaintext
- [x] Never log plaintext (secret, wrap key, decrypted key)
- [x] No Hub `DecryptOrPlaintext` (undecryptable ≠ send ciphertext to the PSP)

## G11.3 Tests

- [x] Fixture wrap key in the test host only
- [x] Round-trip encrypt → decrypt on a fixture secret
- [x] No live PSP. `task pay:test` hermetic

## G11.4 Exit

- [x] IsolationTests still ban `MediatR` / `Modules.` / `BuildingBlocks`
- [x] `NP-GW-001` may move when G12 paste works (prefer G11+G12 same commit if small)
- [x] Unblocked for G12
