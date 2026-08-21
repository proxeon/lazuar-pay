# M14 — `/callback` route

**Track:** Merchant · **Depends:** M13  
**Analysis:** [04](../04-merchant-frontend.md)  
**Goal:** Exact redirect URI `http://localhost:5178/callback`. AuthProvider wraps the router.

---

## M14.1 Router (One app pattern, not ops)

- [ ] Add `react-router-dom` from **One** `lazuar-app`, not from `lazuar-ops`
- [ ] Route `/callback` for the OIDC code exchange
- [ ] `AuthProvider` from `react-oidc-context` **wraps** the router

## M14.2 Exact match

- [ ] Callback URL is exactly the app `redirect_uris` (`http://localhost:5178/callback`)
- [ ] Do not use a different path (`/auth/callback`, `/oidc`, preview `:4178`) as dogfood
- [ ] Friendly errors on the callback page; never print tokens

## M14.3 Must not

- [ ] Do not copy ops `LoginPage` / forgot / reset / verify routes
- [ ] Do not treat `/callback` as the product homepage

## M14.4 Exit

- [ ] Browser can land on `/callback?code&state` and complete the exchange
- [ ] Unblocked for M15
