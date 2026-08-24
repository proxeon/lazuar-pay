# S16 — SecretBox wraps webhook secrets

**Track:** Schema · **Depends:** S10  
**Analysis:** [00](../00-what-must-be-done.md) §3.2  
**IDs:** NP-GW-001  
**Goal:** Same box as API keys. PEM and `whsec_` are secrets.

---

## S16.1 Live

- [ ] PUT encrypts both `secret` and `webhook_secret` with `SecretBox.Protect`
- [ ] Unprotect only inside hosted-url + webhook handlers (never in GET, never in Vite)
- [ ] CHIP PEM is a multi-line secret — store the full PEM
- [ ] `Last4` stays the **API key** last4, not the last4 of a PEM

## S16.2 Must not

- [ ] Do not port Hub `AesSecretVault` / `DecryptOrPlaintext`
- [ ] Do not add a second KMS
- [ ] Do not log Unprotect output

## S16.3 Exit

- [ ] PUT encrypts `webhook_secret`
- [ ] Unblocked for H10
