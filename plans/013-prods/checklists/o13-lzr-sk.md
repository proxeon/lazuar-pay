# O13 — `lzr_sk_` as Bearer

**Track:** One extras · **Depends:** C99 (whoami already forwards Bearer)  
**Analysis:** [08](../08-one-identity-production.md) §6  
**IDs:** NP-ONE-014  
**Goal:** Pay accepts a One machine key the same way it accepts a user JWT: One said 200.

---

## O13.1 Mint on One (document, do not rebuild)

- [ ] Document: mint with One `POST /tenants/{tenantId}/api-keys` and **explicit** scopes (e.g. `tenant:read`, `authz:check`)
- [ ] Never `*` / empty / `admin` for the worker; never omit scopes “so One defaults”
- [ ] Secret shown once; never `VITE_*`; never git

## O13.2 Forward (already C13)

- [ ] Pay forwards `Authorization` to One `GET /me` and `authz/check`
- [ ] If One accepts the key (200), Pay does
- [ ] Same header for `lzr_sk_` as for access_token — not a second auth scheme

## O13.3 Test (hermetic)

- [ ] Fake One 200 when the test sends `Authorization: Bearer lzr_sk_…`
- [ ] Pay 200 on whoami (or a gated route) — no live Zitadel

## O13.4 Must not

- [ ] Pay never stores a Zitadel PAT to mint keys
- [ ] No homemade Pay API-key table; no Stripe `sk_live_` as `ONE_API_KEY`

## O13.5 Exit

- [ ] Docs + fake-One test
- [ ] Unblocked for O14 (HMAC is a different secret)
