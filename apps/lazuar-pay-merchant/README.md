# Lazuar Pay — merchant

Staff Vite shell for focused Pay. Not `lazuar-ops` (`:3003`). Not `lazuar-admin` (`:5173`).

| | |
|---|---|
| Origin | `http://localhost:5178` (`strictPort`) |
| API | focused Pay `http://localhost:8081` (`VITE_PAY_API_URL`) |
| Login | One product login `:5175` (not this app’s homepage) |

Register the public SPA through **One** (Ada JWT), not Zitadel Console and never with `ZITADEL_PAT` in Pay:

```bash
export ACCESS_TOKEN='…'   # access_token, not id_token
export TENANT_ID='…'
WRITE_ENV=1 ./apps/lazuar-pay-merchant/scripts/register-spa.sh
```

`type: spa` is PKCE (no `client_secret`). One seed of this client is P40 convenience only.

Do not add a password form. Send `access_token` as Bearer — never `id_token`. Do not depend on `@repo/api-types-ts` (Hub).

```bash
task pay:merchant
# or
pnpm --filter lazuar-pay-merchant dev
```
