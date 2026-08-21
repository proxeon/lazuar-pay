# Lazuar Pay — merchant

Staff Vite shell for focused Pay. Not `lazuar-ops` (`:3003`). Not `lazuar-admin` (`:5173`).

| | |
|---|---|
| Origin | `http://localhost:5178` (`strictPort`) |
| API | focused Pay `http://localhost:8081` (`VITE_PAY_API_URL`) |
| Login | One product login `:5175` (not this app’s homepage) |

OIDC is not wired yet. Do not add a password form. Send `access_token` as Bearer when whoami is called from the browser — never `id_token`. Do not depend on `@repo/api-types-ts` (Hub).

```bash
task pay:merchant
# or
pnpm --filter lazuar-pay-merchant dev
```
