# P20 — Machine `lzr_sk_` (parked)

**Do not start until C99.**  
**Analysis:** [08](../08-machine-keys.md)  
**Whoami uses user JWT.** Jobs/webhooks need a key later.

---

## P20.1 Mint

- [ ] Mint in **One** (UI / documented curl), not in Pay
- [ ] Explicit scopes: at least `tenant:read` and `authz:check` as needed — **never** empty/`*`
- [ ] Pay env e.g. `One:ApiKey` shown once; never commit

## P20.2 Use

- [ ] `Authorization: Bearer lzr_sk_…` for Pay→One **worker** calls
- [ ] User JWT still forwarded for interactive whoami
- [ ] API-key `authz/check` requires `user_id` in body (One rule) — do not send the key id as user_id

## P20.3 Must not

- [ ] Zitadel PAT in Pay
- [ ] Homemade Pay API-key table duplicating One
- [ ] Wait on npm `@lazuar/one-client`
