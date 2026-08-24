# H16 — Wrap key: no git-known default outside Testing

**Track:** Harden · **Depends:** A00  
**Analysis:** [00](../00-what-must-be-done.md) §3.6; live `SecretBox.LoadKey`  
**IDs:** NP-GW-001  
**Goal:** Production must not encrypt every org `sk_` with `SHA256("lazuar-pay-dev-wrap-key")`.

---

## H16.1 Live today

- [ ] `SecretBox.cs` hashes `"lazuar-pay-dev-wrap-key"` when `Pay:WrapKey` is missing
- [ ] Comment already says “Dev/test only”

## H16.2 Change

- [ ] `Testing` environment: keep a deterministic test key (factory may set `Pay:WrapKey` or allow the hash)
- [ ] `Development`: may keep the hash **or** require `.env` — pick require `Pay:WrapKey` if `.env.example` already documents it
- [ ] `Production`: missing or not 32-byte base64 → **throw at Protect/Unprotect** (or fail boot). Do not encrypt with the git string
- [ ] `.env.example` documents `Pay__WrapKey` 32-byte base64

## H16.3 Must not

- [ ] Do not commit a real wrap key
- [ ] Do not use Hub `Jwt:Secret` as KMS

## H16.4 Exit

- [ ] Production path cannot use the git string
- [ ] Tests still green
