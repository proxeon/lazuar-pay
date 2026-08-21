# M11 — Merchant OIDC env

**Track:** Merchant · **Depends:** M10  
**Analysis:** [04](../04-merchant-frontend.md)  
**Goal:** Public Vite env for Pay + Zitadel. No secrets. No Hub API URL.

---

## M11.1 `.env.example`

- [x] `VITE_PAY_API_URL=http://localhost:8081`
- [x] `VITE_ZITADEL_AUTHORITY=http://localhost:8085`
- [x] `VITE_ZITADEL_CLIENT_ID` (placeholder; real value is local `.env` from M10)
- [x] `VITE_ZITADEL_REDIRECT_URI=http://localhost:5178/callback`
- [x] `VITE_ZITADEL_SCOPE=openid profile email offline_access`

## M11.2 Must not appear

- [x] No `client_secret` / `VITE_ZITADEL_CLIENT_SECRET`
- [x] No `ZITADEL_PAT` in any merchant env
- [x] No Hub `VITE_API_URL`

## M11.3 Honesty

- [x] Authority is the issuer `:8085`, **not** login `:5175`
- [x] Comment: never Hub `:8080`; never point `lazuar-ops` here
- [x] `.env` stays gitignored; only `.env.example` is committed

## M11.4 Exit

- [x] Example env matches the table; no secrets in the bundle
- [x] Unblocked for M12
