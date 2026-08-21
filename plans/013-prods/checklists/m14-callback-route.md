# M14 — `/callback` route

**Track:** Merchant · **Depends:** M13  
**Analysis:** [04](../04-merchant-frontend.md)  
**Goal:** Exact redirect URI `http://localhost:5178/callback`. AuthProvider wraps the router.

---

## M14.1 Router (One app pattern, not ops)

- [x] Add `react-router-dom` from **One** `lazuar-app`, not from `lazuar-ops`
- [x] Route `/callback` for the OIDC code exchange
- [x] `AuthProvider` from `react-oidc-context` **wraps** the router

## M14.2 Exact match

- [x] Callback URL is exactly the app `redirect_uris` (`http://localhost:5178/callback`)
- [x] Do not use a different path (`/auth/callback`, `/oidc`, preview `:4178`) as dogfood
- [x] Friendly errors on the callback page; never print tokens

## M14.3 Must not

- [x] Do not copy ops `LoginPage` / forgot / reset / verify routes
- [x] Do not treat `/callback` as the product homepage

## M14.4 Exit

- [x] Browser can land on `/callback?code&state` and complete the exchange
- [x] Unblocked for M15
