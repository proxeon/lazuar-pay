# M10 — Register `lazuar-pay-merchant` SPA

**Track:** Merchant · **Depends:** B00  
**Analysis:** [04](../04-merchant-frontend.md), [08](../08-one-identity-production.md)  
**Goal:** One app object for `:5178`. Public PKCE client. Pay never holds `ZITADEL_PAT`.  
**011:** NP-ONE-001, NP-ONE-004

---

## M10.1 Create the app object

- [ ] Register via One `POST /api/v1/tenants/{id}/apps` **or** a One seed like `seed-platform-spa-clients.sh`
- [ ] Name **`lazuar-pay-merchant`**
- [ ] `OIDC_APP_TYPE_USER_AGENT`, `AUTH_METHOD` NONE, token **JWT**, PKCE
- [ ] Redirect `http://localhost:5178/callback`; post-logout `http://localhost:5178/`
- [ ] **Not** Zitadel Console-only (break-glass leftovers only)

## M10.2 Client id

- [ ] Copy `client_id` into merchant `.env` as `VITE_ZITADEL_CLIENT_ID` (gitignored)
- [ ] No `client_secret` in Vite
- [ ] Pay **never** holds `ZITADEL_PAT` (One ops does)

## M10.3 Must not

- [ ] Do not mix this PR with C13 whoami (already closed)
- [ ] Do not put PAT / OpenFGA admin / login-client PAT in Pay

## M10.4 One seed (optional)

- [ ] If a One seed PR is needed, document it as **P40 convenience only** — not a One product feature
- [ ] Seed is allowed; Ada via `lazuar-app` JWT + `POST …/apps` is also allowed

## M10.5 Exit

- [ ] One app object exists; local `client_id` is in gitignored `.env`
- [ ] Unblocked for M11
