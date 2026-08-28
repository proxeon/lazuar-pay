# 020 freeze (K00)

Locked 28 August 2026. Amend here, do not silently contradict in a PR.

## Product

| Topic | Lock |
|-------|------|
| What Pay is | Hosted cashier for **One workspaces**. Not a Stripe-shaped platform until K99a. |
| Job A (named program) | Machine key + `pay_url` + Plane C `payment.completed` + Pay sample |
| Job B | First-party go-live honesty. H is cheap and parallel. G does not gate K99a. |
| Tenant | `org_id` = One tenant UUID. No Pay organizations table. |
| Buyers | Not One humans. Public `/v1/pay/{token}` has no Bearer. |
| Staff humans | One OIDC access_token JWT. Merchant SPA never sends `lzr_sk_`. |
| Machines | One `lzr_sk_` as `Authorization: Bearer`. Pay does **not** mint keys. |
| Key writer rule | A key bound to one **active** tenant is **member and writer of that org** on Pay money doors. One scopes gate **One** APIs. Do not require `admin`/`*` on `/me.role` for keys. Human JWT writer remains `owner`/`admin` overlay. |
| Scopes to mint on One | Explicit `tenant:read` + `authz:check`. Never `[]`, `*`, `admin`, `payments.checkouts:write`. |
| God-key | No `ONE_API_KEY` / `lzr_sk_` in Pay env attached to every request. Mode M (P12) is one-tenant workers, later. |
| Wrong family | Pay rejects `sk_live_`, `sk_test_`, Zitadel PAT, Hub `sk_` as **Pay** Bearer (M10). Vault Stripe secrets stay Family B. |

## HTTP

| Topic | Lock |
|-------|------|
| Listen | **8081**. Hub museum keeps 8080. |
| JSON | snake_case. Problem `{ status, title, detail }` already live. |
| `pay_url` | `{CheckoutBaseUrl}/c/{public_token}` (match checkout SPA). Trailing slash stripped. |
| Mint | `POST /v1/checkouts` remains the kernel mint. Merchant SPA may keep minting payment-links. |
| Plane C path | `PUT` or `POST /v1/orgs/{orgId}/webhooks` (singular one active endpoint per org). Not `/v1/one/webhooks`. Not `/v1/webhooks/{provider}/{orgId}`. |
| Plane C event | **`payment.completed` only** for the hatch. Optional `webhook.test`. No `payment.failed` until rails write failed (P15). |
| Plane C dialect | Product **One**: `X-Lazuar-Signature: v1={hex}` + `X-Lazuar-Timestamp`. Signed `{unix}.{raw body}`. Secret is full `whsec_…` UTF-8, **not** Standard Webhooks base64 decode. |
| Plane C TX | Insert delivery row in fulfill `SaveChanges`. **No HTTP inside that transaction.** Worker is a Pay hosted service, off in Testing. |
| SSRF | Production: https (or public http if we must), no loopback, no link-local, no metadata IP. Testing: loopback hatch for samples. |
| Secret once | 201/rotate returns `webhook_secret`. GET returns `webhook_configured` + prefix + url. Same pattern as One. |
| Wrap | `Pay:WrapKey` / `SecretBox`. Do not reuse One’s webhook encryption key. |

## Spec / sample / docs

| Topic | Lock |
|-------|------|
| Honesty | Host Map* first. Then `main.tsp`. Then `task pay:spec` + honesty script. Dist gitignored. |
| Sample | `examples/pay-node`. Plain `fetch`. Not `@repo/api-types-ts`. Not Hub `examples/hub-cashier-next`. |
| Sample proof | Key mints 201, `pay_url` present, Test start or Plane B, verified `payment.completed`, toy row unlocked. |
| README | May say hosted cashier. Must not say production-ready / we have API keys / we have merchant webhooks until the matching phase is done. |

## Refuse (do not “clarify” in a PR)

MediatR; `IEnumerable<IHostedRail>`; Hub `@repo/api-types-ts`; `Modules.One` copy; project reference `apps/lazuar-api`; Pay `sk_*` / `api_keys` table; Zitadel PAT; OpenFGA admin; Pay user/member/org tables; god-key; retarget Hub compose onto 8081; ops :3003 / portal :3004 on Pay CORS; SST/LHDN on pay path; Hub outbound dispatcher / `GatewayPaymentCompletedIntegrationEvent`; fire-and-forget HTTP in fulfill; Standard Webhooks npm on the host; waiting on npm `@lazuar/one-client`; `/v2`.

## Tests

| Topic | Lock |
|-------|------|
| CI | Hermetic `PayApiFactory` Fake One. No sibling project reference. |
| Key tests | Fake One distinguishes JWT vs `lzr_sk_`. After M14, key `POST /v1/checkouts` is **201**. JWT member stays 403. |
| Occupancy | Do not reopen 019 P0 as this program. |
| Isolation | Existing bans stay. W28 adds Hub outbound tokens. |
