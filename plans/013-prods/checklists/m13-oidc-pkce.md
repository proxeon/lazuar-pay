# M13 — OIDC code + PKCE

**Track:** Merchant · **Depends:** M12  
**Analysis:** [04](../04-merchant-frontend.md)  
**Goal:** Wire `react-oidc-context` like `lazuar-app`. No password form.  
**011:** NP-ONE-002

---

## M13.1 Libraries (from One app, not ops)

- [ ] Add `react-oidc-context` + `oidc-client-ts` like `lazuar-app`
- [ ] `response_type: 'code'`, PKCE (S256 library default)
- [ ] `automaticSilentRenew: true`
- [ ] `WebStorageStateStore({ store: window.sessionStorage })`
- [ ] `onSigninCallback` `history.replaceState` (strip `code` from the URL)

## M13.2 Config from M11

- [ ] `authority` = `VITE_ZITADEL_AUTHORITY` (`:8085`)
- [ ] `client_id` = `VITE_ZITADEL_CLIENT_ID` (public)
- [ ] `redirect_uri` = `VITE_ZITADEL_REDIRECT_URI`
- [ ] `scope` = `VITE_ZITADEL_SCOPE`

## M13.3 Must not

- [ ] No password / email fields on `:5178`
- [ ] No Hub `LoginPage` `POST /one/auth/login`
- [ ] No `client_secret` in the OIDC settings object

## M13.4 Exit

- [ ] Sign-in can start PKCE against `:8085` (callback route is M14)
- [ ] Unblocked for M14
