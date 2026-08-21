# M22 — sessionStorage

**Track:** Merchant · **Depends:** M13  
**Analysis:** [04](../04-merchant-frontend.md)  
**Goal:** Tokens in `sessionStorage`. Pay fetch never `credentials: include`.

---

## M22.1 Store

- [ ] `WebStorageStateStore` uses `window.sessionStorage` (copy `lazuar-app`)
- [ ] Do not fork to `localStorage` “so Ada stays logged in”
- [ ] Do not invent a Pay session cookie

## M22.2 Fetch

- [ ] `fetch` to Pay: credentials **omit** (the default for this cross-origin call)
- [ ] Never `credentials: "include"`
- [ ] Comment: **localhost cookies are not port-scoped** (Hub `lazuar_auth` would ride along)

## M22.3 Must not

- [ ] Do not add `AllowCredentials` on Pay CORS “to match One”
- [ ] Do not read `document.cookie` to find the login session

## M22.4 Exit

- [ ] OIDC user is in sessionStorage; Pay calls are header-only
- [ ] Unblocked for M23
