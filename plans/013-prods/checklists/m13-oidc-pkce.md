# M13 — OIDC code + PKCE

**Track:** Merchant · **Depends:** M12  
**Analysis:** [04](../04-merchant-frontend.md)  
**Goal:** Wire `react-oidc-context` like `lazuar-app`. No password form.  
**011:** NP-ONE-002

---

## M13.1 Libraries (from One app, not ops)

- [x] Add `react-oidc-context` + `oidc-client-ts` like `lazuar-app`
- [x] `response_type: 'code'`, PKCE (S256 library default)
- [x] `automaticSilentRenew: true`
- [x] `WebStorageStateStore({ store: window.sessionStorage })`
- [x] `onSigninCallback` `history.replaceState` (strip `code` from the URL)

## M13.2 Config from M11

- [x] `authority` = `VITE_ZITADEL_AUTHORITY` (`:8085`)
- [x] `client_id` = `VITE_ZITADEL_CLIENT_ID` (public)
- [x] `redirect_uri` = `VITE_ZITADEL_REDIRECT_URI`
- [x] `scope` = `VITE_ZITADEL_SCOPE`

## M13.3 Must not

- [x] No password / email fields on `:5178`
- [x] No Hub `LoginPage` `POST /one/auth/login`
- [x] No `client_secret` in the OIDC settings object

## M13.4 Exit

- [x] Sign-in can start PKCE against `:8085` (callback route is M14)
- [x] Unblocked for M14
