# Lazuar Pay (focused host)

New money process. Not the modular monolith in `apps/lazuar-api/`.

Plan: [`plans/011-new-lazuar-pay`](../../plans/011-new-lazuar-pay/README.md). Tracker: [`11-checklist.md`](../../plans/011-new-lazuar-pay/11-checklist.md).

- One solution, one host, one test project.
- Listen on **8081**. Never bind 8080 (One and old Hub use it).
- Merchants come from **lazuar-one**. Local One API: `One__BaseUrl=http://localhost:8080/api/v1` (see `.env.example`). Do not copy `Modules/One`.
- Do not add MediatR, per-module DbContexts, or a project reference into `apps/lazuar-api`.

```bash
task pay:test
task pay:dev          # :8081 health, whoami, checkouts
# or
pnpm --filter lazuar-pay dev
```

TypeSpec: [`packages/pay-spec`](../../packages/pay-spec/) (`task pay:spec`). Not `packages/api-spec`.

Compose still points at `apps/lazuar-api`. Swap later when S1 dogfood is real. Do not set ops/portal `VITE_API_URL` to 8081.

## Live whoami (not CI)

One API and old Hub both want **8080**. For this proof, run **One** (API 8080, login 5175) and **Pay** (8081). Leave Hub `task dev` / compose `lazuar-api` **off**.

Fingerprint One: `GET http://localhost:8080/api/v1/` should name `lazuar-one-api` (Hub `/health` can also look like `{status:ok}`).

Log in at `http://localhost:5175` (product login). Demo user is whatever One README lists (often `ada@acme.test` / `Password1!`). Copy the **access_token**, not the `id_token`.

```bash
curl -sS -H "Authorization: Bearer $ACCESS_TOKEN" http://localhost:8081/v1/whoami
# no header → 401
```

Create a workspace in **lazuar-app** (`:5174`) first if `tenants` is empty, then:

```bash
curl -sS -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"org_id":"'"$ORG_ID"'","amount":10.00,"currency":"MYR","success_url":"https://example.test/ok","cancel_url":"https://example.test/no"}' \
  http://localhost:8081/v1/checkouts
# GET /v1/checkouts/{id} with the same Bearer
```

Checkout is an in-memory fixture (`status: open`). Not a real charge. Buyer has no One account.

Pay never holds a Zitadel PAT. Staff **VIEWER** is not a One tenant role (`owner` / `admin` / `member` only); `/v1/orgs/{orgId}/ready` checks `member`, not “cannot charge”.

Do not send merchants to `lazuar-admin` (`:5173`).
