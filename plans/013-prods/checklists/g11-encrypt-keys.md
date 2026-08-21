# G11 — Encrypt BYOK keys at rest

**Track:** Rails · **Depends:** D22  
**Analysis:** [06](../06-money-rails.md) §4  
**IDs:** NP-GW-001  
**Goal:** Ciphertext in Pay DB. Wrap key in Pay env. `NP-GW-001`.

---

## G11.1 Wrap key (Pay env, not Vite)

- [ ] Wrap key in **Pay process env** (`Pay:DataKey` / equivalent). Not Vite. Not `VITE_*`
- [ ] Not Hub `Kms:MasterKey`. Not `Jwt:Secret` fallback. Missing key → refuse save (or refuse boot)
- [ ] `:5178` / `:5179` stay `VITE_PAY_API_URL` only — no `VITE_STRIPE_*` / `VITE_CHIP_*` / `VITE_KMS_*`

## G11.2 At rest

- [ ] D22 `gateway_credentials` (or the D22 name) stores ciphertext per `org_id` + provider
- [ ] Encrypt the provider secret (Stripe `sk_…` / CHIP Bearer). Never persist plaintext
- [ ] Never log plaintext (secret, wrap key, decrypted key)
- [ ] No Hub `DecryptOrPlaintext` (undecryptable ≠ send ciphertext to the PSP)

## G11.3 Tests

- [ ] Fixture wrap key in the test host only
- [ ] Round-trip encrypt → decrypt on a fixture secret
- [ ] No live PSP. `task pay:test` hermetic

## G11.4 Exit

- [ ] IsolationTests still ban `MediatR` / `Modules.` / `BuildingBlocks`
- [ ] `NP-GW-001` may move when G12 paste works (prefer G11+G12 same commit if small)
- [ ] Unblocked for G12
