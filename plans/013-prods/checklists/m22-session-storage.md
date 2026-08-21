# M22 — sessionStorage

**Track:** Merchant · **Depends:** M13  
**Analysis:** [04](../04-merchant-frontend.md)  
**Goal:** Tokens in `sessionStorage`. Pay fetch never `credentials: include`.

---

## M22.1 Store

- [x] `WebStorageStateStore` uses `window.sessionStorage` (copy `lazuar-app`)
- [x] Do not fork to `localStorage` “so Ada stays logged in”
- [x] Do not invent a Pay session cookie

## M22.2 Fetch

- [x] `fetch` to Pay: credentials **omit** (the default for this cross-origin call)
- [x] Never `credentials: "include"`
- [x] Comment: **localhost cookies are not port-scoped** (Hub `lazuar_auth` would ride along)

## M22.3 Must not

- [x] Do not add `AllowCredentials` on Pay CORS “to match One”
- [x] Do not read `document.cookie` to find the login session

## M22.4 Exit

- [x] OIDC user is in sessionStorage; Pay calls are header-only
- [x] Unblocked for M23
