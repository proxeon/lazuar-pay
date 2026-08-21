# M11 — Merchant OIDC env

**Track:** Merchant · **Depends:** M10  
**Analysis:** [04](../04-merchant-frontend.md)  
**Goal:** Public Vite env for Pay + Zitadel. No secrets. No Hub API URL.

---

## M11.1 `.env.example`

- [ ] `VITE_PAY_API_URL=http://localhost:8081`
- [ ] `VITE_ZITADEL_AUTHORITY=http://localhost:8085`
- [ ] `VITE_ZITADEL_CLIENT_ID` (placeholder; real value is local `.env` from M10)
- [ ] `VITE_ZITADEL_REDIRECT_URI=http://localhost:5178/callback`
- [ ] `VITE_ZITADEL_SCOPE=openid profile email offline_access`

## M11.2 Must not appear

- [ ] No `client_secret` / `VITE_ZITADEL_CLIENT_SECRET`
- [ ] No `ZITADEL_PAT` in any merchant env
- [ ] No Hub `VITE_API_URL`

## M11.3 Honesty

- [ ] Authority is the issuer `:8085`, **not** login `:5175`
- [ ] Comment: never Hub `:8080`; never point `lazuar-ops` here
- [ ] `.env` stays gitignored; only `.env.example` is committed

## M11.4 Exit

- [ ] Example env matches the table; no secrets in the bundle
- [ ] Unblocked for M12
