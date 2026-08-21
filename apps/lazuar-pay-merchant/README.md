# Lazuar Pay — merchant

Staff Vite shell for focused Pay. Not `lazuar-ops` (`:3003`). Not `lazuar-admin` (`:5173`).

| | |
|---|---|
| Origin | `http://localhost:5178` (`strictPort`) |
| API | focused Pay `http://localhost:8081` (`VITE_PAY_API_URL`) |
| Login | One product login **`:5175`** (not this app’s homepage; issuer is Zitadel `:8085`) |
| Callback | `http://localhost:5178/callback` |

## Register the SPA (M10)

Public PKCE `type: spa`. Ada JWT, never `ZITADEL_PAT` in Pay:

```bash
export ACCESS_TOKEN='…'   # access_token, not id_token
export TENANT_ID='…'
WRITE_ENV=1 ./apps/lazuar-pay-merchant/scripts/register-spa.sh
```

## One allowlist + CORS (M25)

Before sign-in works, One login `REDIRECT_ALLOWLIST` must include `http://localhost:5178/callback` (and `http://127.0.0.1:5178/callback` if you use that twin). If this SPA calls One `POST /tenants`, One `App:CorsOrigins` must include `http://localhost:5178`. Pay CORS already allows 5178 — do **not** add ops `:3003`.

Production: empty One `CorsOrigins` fails boot — that is One’s rule, not a Pay PAT.

## Live whoami (M26)

Hub `task dev` / compose `api` **off**. Fingerprint One: `GET http://localhost:8080/api/v1/` names `lazuar-one-api`.

```bash
task pay:dev          # :8081
task pay:merchant     # :5178
# One API :8080, login :5175, Zitadel :8085
```

Open `http://localhost:5178` → Sign in → `:5175` → callback → workspaces from `GET /v1/whoami`. Demo user is whatever One lists (often `ada@acme.test`).

Send **access_token** as Bearer — never `id_token`. Tokens live in **sessionStorage**. Fetches omit cookies (`credentials` default) because localhost cookies are not port-scoped.

## Must not (M20, M27, P60)

No password form. No `POST /one/auth/login`. No Hub cookie. Do not set `lazuar-ops` `VITE_API_URL` to 8081. Do not port ops LHDN, chat, WhatsApp, Hub CRM, quotes-as-tax, credits, or the Hub sidebar catalog.

`owner` / `admin` may paste keys and create catalog later. `member` is read-only on money. One has no VIEWER role.

Do not depend on `@repo/api-types-ts` (Hub).

```bash
task pay:merchant
# or
pnpm --filter lazuar-pay-merchant dev
pnpm --filter lazuar-pay-merchant test
```
