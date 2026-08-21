# 013 Bar B — locked decisions

**Filled by:** [b00-align-freeze.md](./b00-align-freeze.md)  
**Evidence:** [`../01`](../01-production-ready-bar.md)–[`../10`](../10-ci-observability-decommission.md)  
**Do not change a row without amending B00.**

| Topic | Lock |
|-------|------|
| Bar | **B** = 011 dogfood sentence on 8081 + 5178 + 5179. Not Hub parity. Not Hub dark. Not Bar C. |
| Host | `apps/lazuar-pay` only. Not `apps/lazuar-api`. Not a Go rewrite in this program. |
| Listen | **8081**. Never 8080. |
| Merchant UI | `apps/lazuar-pay-merchant` **5178** `strictPort`. Not `lazuar-ops` `:3003`. |
| Checkout UI | `apps/lazuar-pay-checkout` **5179** `strictPort`. Not `lazuar-portal` `:3004`. |
| Login | One product login **`:5175`**. Not Pay homepage. Not `:3005`. Not `:5173`. |
| One API | `http://localhost:8080/api/v1` locally. Env `One:BaseUrl`. Fingerprint `name=lazuar-one-api`. |
| Auth | Bearer `access_token` (or later `lzr_sk_`). No Pay password, no cookie JWT, no `id_token` as Bearer. |
| SPA | Public OIDC code + PKCE. Register via One `POST /tenants/{id}/apps` (or One seed). Redirect `http://localhost:5178/callback`. Not Console-only. |
| Token picker | Copy One `pickApiBearerToken`. JWT `access_token` only. |
| Token store | `sessionStorage`. No `credentials: "include"` on `localhost` (cookies are not port-scoped). |
| Whoami | Existing `GET /v1/whoami`. SPA calls it after callback. Endpoint only, not middleware. |
| Org | One tenant id **is** `org_id`. No Pay `organizations` / `users` tables. Thin `org_settings` keyed by tenant id is allowed. |
| Path vs header | `{orgId}` in path is SoT. `X-Lazuar-Tenant-Id` is a hint. |
| VIEWER | One roles: `owner` \| `admin` \| `member`. Pay: only `owner`/`admin` paste keys / charge / refund. `member` sees ops. Not `check(member)` as 021. |
| Buyer | No One/Zitadel account. Public `GET /v1/pay/{token}` + `POST /v1/pay/{token}/start`. Do **not** ungated `GET /v1/checkouts/{id}`. |
| Shareable URL | `http://localhost:5179/c/{token}`. |
| JSON | snake_case. `packages/pay-spec` only. Not Hub `task gen`. |
| CORS | 5178 + 5179 (+ 127.0.0.1 twins). Never 3003/3004/5173. |
| DB | Greenfield Postgres, published **5435** locally. DB name `lazuar_pay`. One schema. One migrator. Not Hub `lazuar_mvp`. Not One `lazuar`. |
| Persistence shape | SQL or **one** `PayDbContext`. Concrete stores. No MediatR, no nine contexts, no outbox-to-self. |
| Rails | **Stripe** for first dogfood (CHIP parked until Bar C / second rail). Not five adapters. Billplz-class never silent debit. |
| Webhook PSP | `POST /v1/webhooks/{provider}/{orgId}`. Signature, empty 400, unique `(org_id, provider, event_id)`. |
| Fulfillment | Same HTTP request, one DB transaction: paid + seat/one-off + journal + `RCPT-` + audit. |
| Receipt | Official Receipt `RCPT-{MYT year}-#####`. Never UUID. Never Tax Invoice. Never VALID. |
| SST | Fail closed if registration unknown. Do not undercharge even at qty=1. Steal `SstTaxMath` judgment, not the module. |
| One HMAC | Different route + table from PSP. `tenant.suspended` stops **new** charges before live dogfood is called production-ready. |
| Secrets | Pay may hold: public OIDC `client_id`, BYOK ciphertext + wrap key, PSP webhook secrets, One HMAC secret, `lzr_sk_` for jobs. **Never** Zitadel PAT / login PAT / OpenFGA admin / masterkey / Hub `Jwt:Secret`. |
| Tests | `task pay:test` hermetic. IsolationTests keep cathedral bans and add Vite Hub-types ban. |
| Old UIs | P60: ops/portal stay on Hub 8080. Do not set `VITE_API_URL` to 8081. |
| One staging | NOT PASSED is **not** a Pay blocker. HTTP façade must be up. |
| Hub DX | Do not use `task dev` / `pnpm dev` / compose `api` on the dogfood laptop while One owns 8080. |

## Filled in B00 (must not be blank before G10 / K10)

| Topic | Value | Notes |
|-------|-------|-------|
| First rail | **Stripe** | Hosted Checkout `mode=payment`. CHIP is the next Malaysian rail, not this Bar B. Billplz is reminder-only — not first. |
| Public pay identifier | `token` on `/v1/pay/{token}` | Paper 05 option B. Merchant GET stays member-gated. |
| Migrator | **One EF `PayDbContext`**, one migrations folder | Not nine contexts. Tests may use EF InMemory; prod is Npgsql on **5435**. |
| Connection string name | `ConnectionStrings:Pay` | Locked in [D12](./d12-connection-string.md). Not Hub’s trio. |
