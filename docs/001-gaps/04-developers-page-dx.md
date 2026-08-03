<!-- Source subagent: 019fc650-3511-7762-8927-4f2bb5fdd380 -->
<!-- Full uncondensed subagent analysis — do not summarize -->

# Developers Page & Integration DX Gap Analysis

## What Exists Today

### Product surface

| Surface | Path / URL | Role |
|---|---|---|
| Developer Hub app | `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/developers-page` | Next.js 16 app rendering Scalar OpenAPI references |
| Local dev | `pnpm dev` on port **3002** (`package.json`) | Via `mprocs-dev.yaml` |
| Production mount | `hub.lazuar.com/docs*` → `developers:3000` | Caddy (`deploy/prod/Caddyfile`) |
| Base path | `NEXT_BASE_PATH=/docs` in Docker bake + Dockerfile | `next.config.ts` reads env |
| Spec packaging | `OPENAPI_SPEC_ROOT=/app/openapi-specs` | Specs copied from `packages/api-spec/dist` at image build |

### What the hub actually does

The hub is a **thin documentation shell**, not an integration console:

1. **Landing** (`apps/developers-page/app/page.tsx`) — four product cards linking to module references.
2. **Four Scalar routes** — each is a Next.js Route Handler that loads a YAML string and passes it to `@scalar/nextjs-api-reference`:
   - `/one` → `dist/one/openapi.yaml`
   - `/ops` → `dist/ops/openapi.yaml`
   - `/billing` → `dist/billing/openapi.yaml`
   - `/lhdn` → `dist/lhdn/openapi.yaml`
3. **Spec loader** (`apps/developers-page/lib/openapi.ts`) — resolves monorepo path vs Docker `OPENAPI_SPEC_ROOT`.

There is **no auth**, **no session**, **no “Try it with my key”**, **no SDK install instructions**, **no quickstarts**, **no webhook guide**, and **no credential generation**. The landing copy promises “API reference and integration guides”; only references exist.

### Adjacent “developer” surfaces (ops-page)

Ops console has a **Developer** nav group (`apps/ops-page/src/components/Sidebar.tsx`):

| Route | Page | What it manages |
|---|---|---|
| `/developer/webhooks` | `DeveloperSettingsPage.tsx` | **Outbound workspace webhooks** (One module) — URL, active flag, HMAC secret |
| `/developer/logs` | `DeliveryLogsPage.tsx` | Webhook delivery outbox status |

This is **event egress configuration**, not **API credential management** for calling Lazuar APIs.

### Backend integration primitives that *do* exist

| Capability | Location | Notes |
|---|---|---|
| LHDN `DeveloperApiKey` aggregate | `apps/lazuar-api/Modules/Lhdn/Domain/Aggregates/DeveloperApiKey.cs` | Hashed keys, `sk_live_` / `sk_test_` prefixes |
| Generate / revoke API keys | `Endpoints.cs` `POST/DELETE /lhdn/api-keys` | Behind `OrgAdmin` (JWT **or** API key role) |
| API key auth middleware | `ApiKeyAuthenticationMiddleware.cs` | Matches `Authorization: Bearer sk_live_*` / `sk_test_*` |
| Tenant skip for API keys | `TenantSecurityMiddleware.cs` | ApiKey auth skips `X-Tenant-Id` requirement |
| OrgAdmin policy | `Program.cs` | Roles: `SUPER_ADMIN`, `ADMIN`, **`API_CLIENT`** |
| LHDN TS SDK | `packages/lhdn-sdk-ts` (`@lazuar/lhdn-sdk` v0.1.0) | Kiota-generated |
| LHDN .NET SDK | `packages/lhdn-sdk-dotnet` (`Lazuar.Lhdn.Sdk` v0.1.0) | Kiota-generated |
| Publish runbook | ADR `011-sdk-publishing-runbook.md` | Manual NPM/NuGet publish |
| TypeSpec → OpenAPI → types pipeline | ADR 005 + `Taskfile.yml` `task gen` | Includes `gen:sdk-lhdn` |

### Explicit non-goals of current developers-page (evidence)

- README is stock `create-next-app` boilerplate — no product narrative.
- `AGENTS.md` / `CLAUDE.md` only warn about Next.js version quirks.
- Root monorepo README **omits `developers-page`** from project structure (lists api, ops, portal, superadmin only).
- ADR 016 domain strategy does **not** include a developers hostname (prod uses path `/docs` on hub, not `developers.lazuar.com` as ADR 007 examples suggested).

---

## OpenAPI / Scalar Documentation Surface

### Architecture (ADR 007)

**Product-scoped API references** — compile separate TypeSpec entrypoints into separate YAML artifacts and mount each under its own Scalar route. Rationale: avoid one mega OpenAPI, reduce audience mismatch, preserve bounded-context framing.

### Build pipeline

```text
packages/api-spec/*.tsp
        │  pnpm build (api-spec package.json)
        ▼
packages/api-spec/dist/
  openapi.yaml          ← main.tsp (full monolith contract for codegen)
  one/openapi.yaml
  ops/openapi.yaml
  billing/openapi.yaml
  lhdn/openapi.yaml
        │
        ├─► openapi-typescript → packages/api-types-ts
        ├─► NSwag → packages/api-types-dotnet
        ├─► Kiota (lhdn only) → lhdn-sdk-ts / lhdn-sdk-dotnet
        └─► developers-page Scalar routes (+ Docker COPY into image)
```

**`packages/api-spec/package.json` build script:**

```text
tsp compile main.tsp → dist
tsp compile docs-one.tsp → dist/one
tsp compile docs-ops.tsp → dist/ops
tsp compile docs-billing.tsp → dist/billing
tsp compile docs-lhdn.tsp → dist/lhdn
```

**Not compiled today:** `docs-commerce.tsp` exists but is **not** in the build script and has **no** Scalar route / hub card.

### Entry-point composition

| File | Service title | Imports | Server URL(s) |
|---|---|---|---|
| `main.tsp` | Lazuar Platform API | one, ops, commerce, communications, billing, lhdn, crm, platform | `localhost:8080/api/v1` |
| `docs-one.tsp` | Lazuar Platform API (Core) | one **+ billing** | local only |
| `docs-ops.tsp` | Lazuar Ops API | ops **+ billing** | local only |
| `docs-billing.tsp` | Lazuar Billing API | billing only | local only |
| `docs-lhdn.tsp` | Lazuar LHDN API | lhdn only | **prod + local**; `@useAuth(ApiKeyAuth\|BearerAuth)` |
| `docs-commerce.tsp` | Lazuar Commerce API | commerce + communications | **orphaned** (not built) |

### Scalar integration details

Each product route is identical pattern, e.g. billing:

```1:14:apps/developers-page/app/billing/route.ts
import { ApiReference } from "@scalar/nextjs-api-reference";
import { readOpenApiSpec } from "../../lib/openapi";

const openapiSpec = readOpenApiSpec("billing");

export const GET = ApiReference({
  spec: {
    content: openapiSpec,
  },
  theme: "default",
  metaData: {
    title: "Lazuar Billing API",
  },
});
```

Observations:

- Spec is read **at module load** (build/start time), not per-request from a live API.
- No `hideDownloadButton` in live code (ADR 007 sample used it; production routes do not).
- No custom Scalar auth config, environment switcher beyond OpenAPI `servers`, or theming beyond `"default"`.
- No markdown/MDX integration guides alongside Scalar.

### Auth documentation in OpenAPI

| Spec | Security schemes |
|---|---|
| one / ops / billing | `BearerAuth` (HTTP Bearer) only |
| lhdn | Global: `ApiKeyAuth` (header `Authorization`) **or** `BearerAuth`; operations also list `BearerAuth` |

**Mismatch with runtime auth:**

- First-party frontends use **HTTP-only JWT cookies** (`lazuar_auth` / `lazuar_admin_auth` in `Program.cs` JwtBearer `OnMessageReceived`) — cookie auth is **not** documented in OpenAPI.
- Integration API keys use `Authorization: Bearer sk_live_…` (middleware) — documented as both “ApiKey in Authorization header” and “Bearer”, which is ambiguous for Scalar “Try it”.
- Admin routes expect **`X-Tenant-Id`** (`TenantSecurityMiddleware`) — **not modeled** in TypeSpec headers for billing/ops/commerce admin ops, so Scalar will not prompt for tenant context.

### Path / server consistency bugs (high severity for “Try it”)

**LHDN TypeSpec uses absolute-looking path prefix:**

```9:11:packages/api-spec/modules/lhdn/routes.tsp
@useAuth(BearerAuth)
@route("/api/v1/lhdn")
interface LhdnOperations {
```

Generated OpenAPI paths are `/api/v1/lhdn/...` while `servers[].url` is already `http://localhost:8080/api/v1` (and prod `https://api.lazuar.com/api/v1`). Scalar “Try it” would call:

`{server}/api/v1/lhdn/...` → **`/api/v1/api/v1/lhdn/...`**

Other modules correctly use relative paths (`/one`, `/admin/billing`, `/ops`). **LHDN is the only product-scoped doc that double-prefixes.**

Backend reality: `MapGroup("/api/v1")` + `MapGroup("/lhdn")` → real path `/api/v1/lhdn/...`. Spec path should be `/lhdn/...` with server base `/api/v1`, matching one/billing/ops.

### Production URL drift

| Claimed | Actual |
|---|---|
| ADR 007: `developers.lazuar.com/{product}` | Deployed as **`hub.lazuar.com/docs/{product}`** |
| LHDN OpenAPI server: `https://api.lazuar.com/api/v1` | Hub Caddy exposes API at **`hub.lazuar.com/api/*`** |
| Landing “v1” badges | OpenAPI `info.version` is **`0.0.0`** everywhere |

### What Scalar currently advertises (by product)

| Hub card | Audience reality |
|---|---|
| **Lazuar One (Core)** | Login, register, workspaces, invites, entitlements, storage, **workspace webhooks**, plus **entire billing admin surface** (docs-one imports billing) |
| **Ops Console API** | AI chat / execute-action ops endpoints **+ billing again** — operator/internal surface, poor external-integration fit |
| **Billing** | Ledger, credits, packages, financial summary — **JWT + OrgAdmin**, not external SKD style |
| **LHDN Gateway** | Submit documents, webhooks, certificates, API keys — **closest to true external integration product** |

---

## Product-Scoped API References

### Implemented vs intended

| Product | `docs-*.tsp` | Built YAML | Scalar route | Hub card | External SDK |
|---|---|---|---|---|---|
| One | yes | yes | yes | yes | no |
| Ops | yes | yes | yes | yes | no |
| Billing | yes | yes | yes | yes | no |
| LHDN | yes | yes | yes | yes | **yes** (TS + .NET) |
| Commerce | **yes (orphan)** | **no** | **no** | **no** | no |
| Communications | only via commerce docs | no | no | no | no |
| CRM | main only | no product docs | no | no | no |
| Platform | main only | no | no | no | no |

### Scope pollution

- **`docs-one` and `docs-ops` both re-import billing routes**, so “One” and “Ops” references are not pure product surfaces; billing appears three times across hub tabs.
- ADR 007 examples still mention **Community / Vault** routes that were product-killed or deferred (ADR 021/022/023 CaaS pivot). Checklist is partially stale vs current modules.

### Contract vs implementation gaps (LHDN — integration-critical)

| Contract (TypeSpec / OpenAPI / SDK) | Backend `Modules/Lhdn/Infrastructure/Endpoints.cs` |
|---|---|
| `POST /api-keys` generate | Implemented |
| `GET /api-keys` list | **Missing** (only appears in TypeSpec) |
| `DELETE /api-keys/{id}` revoke | Implemented |
| `POST /documents` + Idempotency-Key | Implemented |
| `GET /documents/{internalId}` | Implemented |
| `POST /documents/{internalId}/cancel` | Implemented |
| `POST/GET/DELETE /webhooks` | Implemented |
| `PUT /workspaces/{id}/lhdn-certificate` | Implemented |
| `POST /taxpayer/validate` | **Not mapped** in Endpoints (gateway adapter has LHDN MyInvois validate helper only) |

Without list API keys, even a raw curl-based admin cannot inventory keys after generation (plain key is one-time `plain_key` response only). UI cannot build a Stripe-like key table without implementing list.

### Auth model for LHDN “integration” vs “dashboard”

Intended product intent (PO): **generate credentials in frontend → call backend with credentials (not JWT)**.

Implemented backend intent (partial):

1. **Dashboard / console** (cookie JWT + role ADMIN) calls `POST /lhdn/api-keys` under `OrgAdmin`.
2. **Integration client** sends `Bearer sk_*` → middleware stamps role `API_CLIENT` + tenant from `DeveloperApiKeys` table.
3. Same endpoint group is `RequireAuthorization("OrgAdmin")`, and `API_CLIENT` is **allowed** — so an integration key can theoretically generate more keys / revoke / change certificates. There is **no separate “integration-only” policy** scoping keys to document submit/status vs key management.

`ApiKeyDto` stores **prefix only** (`sk_live_` / `sk_test_`), not a visible key fingerprint — even if list were implemented, UX would be weaker than Stripe’s `sk_live_…Ab12` last-four style unless extended.

---

## Credential Generation & Management UX

### Intended flow (product owner)

```text
Frontend (ops) → Generate API credentials
                 ↓
Store hash / show plain secret once
                 ↓
Integrator uses credentials against backend API
(not session JWT)
```

### What exists in backend (LHDN only)

```text
POST /api/v1/lhdn/api-keys  { name, is_test_mode }
  → GenerateApiKeyCommand
  → plain token "sk_test_…" | "sk_live_…" (returned once)
  → store KeyHash + Prefix + Name

DELETE /api/v1/lhdn/api-keys/{id}
  → Revoke + ApiKeyRevokedIntegrationEvent
  → memory cache invalidation (ApiKeyRevokedIntegrationEventHandler)
```

### What exists in frontend

| UI | API keys for Lazuar? | Notes |
|---|---|---|
| ops-page Developer | **No** | Outbound webhooks + delivery logs only |
| ops-page Payment Settings | No | **BYOK third-party** gateway secrets (Stripe/Billplz/etc.) |
| ops-page Email Settings | No | **Resend** provider key |
| portal-page | No | Buyer checkout/portal only |
| superadmin-page | No evidence of Lazuar API key vault | — |
| developers-page | No | Read-only docs |

**Conclusion:** LHDN developer API key **generation is backend-ready and TypeSpec-documented**, but **there is no credential management UI**. Integrators would need to call the generate endpoint with a human JWT session (and somehow obtain one + tenant context) — not productized.

### Outbound webhook UX (partial “developer” story)

Ops **does** productize webhook registration for **One workspace outbound events**:

- Configure URL + active status
- Show signing secret with copy (`QuickCopy`)
- Document `X-Lazuar-Signature` HMAC-SHA256 (UI copy)
- Delivery log table (status, event type, errors)

This is **push-to-customer-app** DX, not **pull-with-API-key** DX. No link from this page to `/docs` or LHDN.

### Certificate / tax identity UX for LHDN

- TypeSpec + backend: `PUT /lhdn/workspaces/{id}/lhdn-certificate` (P12 base64 + passphrase).
- One TypeSpec also has workspace `lhdn-config` paths in OpenAPI (`/one/workspaces/{id}/lhdn-config`).
- **No ops-page UI** was found for certificate upload or LHDN taxpayer profile under Developer settings.

### Auth artifact confusion matrix

| Credential type | Who creates it | Where stored | Used by |
|---|---|---|---|
| JWT in cookie (`lazuar_auth`) | Login (`/one/auth/login`) | Browser cookie | ops-page, internal APIs |
| LHDN `sk_live_` / `sk_test_` | `POST /lhdn/api-keys` | Hash in `lhdn.DeveloperApiKeys` | External integrators / SDKs |
| Workspace webhook secret | One webhook save | DB; shown in ops | Customer servers verifying Lazuar POSTs |
| Payment gateway API keys | Merchant pastes into ops | Payments module vault | Lazuar → Billplz/Stripe (BYOK) |
| Resend API key | Merchant pastes into ops | Communications | Transactional email |

PO statement “Not using JWT directly for integrations” is **architecturally intended for LHDN keys**, but **undelivered in UI** and **undifferentiated in docs** (most hub modules document only Bearer JWT-style admin APIs).

---

## Integration Onboarding Journey Gaps

### Ideal journey (Stripe/Twilio-class)

1. Sign up / create workspace  
2. Open **Developers → API keys**  
3. Create test key; copy once  
4. Install SDK / curl sample  
5. Hit sandbox endpoint  
6. Configure webhooks + verify signature  
7. Switch to live keys  
8. Monitor logs / usage  

### Lazuar today (reconstructed)

| Step | Status | Evidence |
|---|---|---|
| 1. Create workspace | Exists (One + ops) | One routes, ops login |
| 2. Find developer section | Partial | Webhooks only under Developer nav |
| 3. Generate API key | **Backend only, no UI** | LHDN endpoints; zero ops wiring |
| 4. Docs / first call | **Broken for LHDN “Try it”** | Double `/api/v1` path; servers URL drift |
| 5. SDK install | Package code exists at **0.1.0**; publish is **manual runbook** | ADR 011; no hub mention of `@lazuar/lhdn-sdk` |
| 6. Webhooks | Workspace outbound webhooks in ops; LHDN webhooks API-only | Split product stories |
| 7. Test vs live | `is_test_mode` on key gen; claim `IsTestMode` | No UI to choose/create either |
| 8. Monitoring | Webhook delivery logs only | No API request logs, no usage dashboard |
| 9. Commerce headless integration | Public commerce routes exist | **No product docs on hub** despite CaaS positioning |

### Landing page honesty gap

`page.tsx` modules:

- **One** — “Global identity…” → mostly **first-party console API**, not partner integration API  
- **Ops** — “Operator and workspace management surfaces used by the Lazuar console” → **explicitly internal**  
- **Billing** — ledger/credits for workspaces → **admin**, JWT  
- **LHDN** — only card that matches external integration language (“Submit clean JSON…”)  

Publishing **Ops Console API** and full **One auth** surfaces on a public developer hub is a **security/product framing issue** (audience mismatch ADR 007 warned about, then partially recreated by which products were linked).

### “Integration guides” content gap

No MDX/guides under `developers-page`. Internal docs exist for LHDN XML/signing (`docs/lhdn/*`) and ADRs, but they are **not published** to the hub. Postman under `docs/postman/` targets **LHDN MyInvois government** APIs (`client_id` / `client_secret` to MyInvois identity), **not** Lazuar’s API — easy to confuse new integrators.

### SDK auth DX footgun

Both SDKs use Kiota `ApiKeyAuthenticationProvider` with header name `"Authorization"` and the raw `apiKey` string:

```17:21:packages/lhdn-sdk-ts/src/index.ts
  const authProvider = new ApiKeyAuthenticationProvider(
    options.apiKey,
    "Authorization",
    ApiKeyLocation.Header
  );
```

Middleware requires the value to start with **`Bearer sk_live_`** or **`Bearer sk_test_`**. Unless consumers pass `apiKey: "Bearer sk_live_…"`, requests fail 401. Neither factory docs nor hub explain this; OpenAPI `ApiKeyAuth` implies raw key in header without `Bearer ` scheme prefix. **Docs, middleware, and SDK are three different mental models.**

### Idempotency

Document submit requires `Idempotency-Key` (enforced in Endpoints; TypeSpec models the header). .NET SDK auto-injects a GUID via `IdempotencyHandler`; TS SDK does **not** auto-inject — asymmetric DX.

---

## SDK Publishing State

### What is generated

`task gen` → `gen:sdk-lhdn`:

```yaml
# Taskfile.yml
kiota generate -l typescript -d packages/api-spec/dist/lhdn/openapi.yaml
  → packages/lhdn-sdk-ts/src/generated
kiota generate -l csharp -d packages/api-spec/dist/lhdn/openapi.yaml
  → packages/lhdn-sdk-dotnet/src/Generated
```

Hand-written wrappers:

- TS: `initLhdnClient({ apiKey, baseUrl? })`
- .NET: `LhdnClientFactory.Create(apiKey, baseUrl?)` + automatic idempotency on POST

### Package metadata

| Package | Identity | Version | Target |
|---|---|---|---|
| TS | `@lazuar/lhdn-sdk` | 0.1.0 | Kiota preview deps |
| .NET | `Lazuar.Lhdn.Sdk` | 0.1.0 | net8.0 |

### Publishing process

ADR 011 is a **manual** checklist (`npm login` / `npm publish --access public`, `dotnet pack` + `nuget push`). No CI workflow evidence in this exploration for automated publish on tag. No changelog, no samples package, no README inside SDK folders was checked beyond factory entrypoints — package is pre-product polish.

### What is *not* an external SDK

| Package | Role |
|---|---|
| `@repo/api-types-ts` | **Internal** OpenAPI types for monorepo frontends (cookie/JWT apps) |
| `Lazuar.ApiContracts` | **Internal** C# DTOs for API boundary |
| Postman MyInvois collection | Government API, not Lazuar |

There is **no** Commerce/Checkout public SDK, **no** webhook signature verification helper package, **no** multi-language beyond TS/.NET for LHDN.

### Versioning story

OpenAPI `info.version: 0.0.0` for all product specs while hub badges say `v1` and SDKs are `0.1.0` — no coherent public versioning narrative.

---

## Comparison to Stripe Dashboard / Twilio Console DX

| Capability | Stripe / Twilio | Lazuar today |
|---|---|---|
| **Docs site** | Guides + API ref + recipes | Scalar OpenAPI only |
| **Product segmentation** | Products as first-class (Payments, Billing, Tax…) | Partial (4 tabs; commerce missing; ops should not be public) |
| **API keys UI** | Create/reveal once/revoke/restrict | Backend LHDN only; **no UI** |
| **Test / live mode** | Global toggle + key prefixes | Key prefix only; no productized mode UX |
| **Auth for integrations** | Secret keys / restricted keys; not dashboard session | Intended sk_*; most docs are Bearer admin |
| **Try-it console** | Authenticated with user keys | Anonymous Scalar; path/server bugs |
| **SDKs** | First-class install in docs | LHDN packages exist; **not linked from hub** |
| **Webhooks** | Endpoint UI + signing secret + event catalog + retries UI | Workspace webhooks in ops; LHDN webhooks API-only; no event catalog on hub |
| **Worked examples** | Quickstart in 5 minutes | None published |
| **Error model docs** | Human pages | RFC7807 models only in OpenAPI |
| **Changelog / versioning** | Explicit | `0.0.0` specs |
| **Scope restriction** | Restricted keys / ACL | Single `API_CLIENT` role on entire LHDN group |
| **Internal vs external APIs** | Separate (Dashboard vs API) | Hub documents **console** APIs (One/Ops/Billing) as if external |
| **Credential generation location** | Dashboard (frontend) | Intended ops/frontend; **unimplemented** |

**Twilio-like gaps:** no Account SID/Auth Token pair model, no Console “Tools”, no debugger for request logs.

**Stripe-like gaps:** no Developers home inside ops with keys + webhooks + events; no “starting integration” checklist; no publishable vs secret key split (may not need publishable if all server-side).

---

## Misalignment with Product Intent

### Product owner intent (given)

1. developers-page currently documents **backend API**, not **integration API**.  
2. Integration flow: **generate credentials in frontend → use against backend**.  
3. **Not JWT** for integrations.

### How the codebase aligns / misaligns

| Intent | Reality | Verdict |
|---|---|---|
| Hub = integration DX | Hub = Scalar dump of modular monolith contracts (including **Ops AI chat** and **login**) | **Misaligned** |
| Credentials in frontend | Generate/revoke API exists; **no frontend** | **Backend half-done** |
| Non-JWT integrations | LHDN middleware + `sk_*` keys | **Correct for LHDN only** |
| API-first CaaS (README) | Commerce public API exists; **not on hub** | **Strategic miss** |
| Compliance CaaS (ADR 021) | LHDN is the star integration product | Hub treats One/Ops/Billing as peers |
| ADR 007 product-scoped docs | Pattern implemented | Commerce orphan; billing duplicated; stale Community/Vault examples |
| ADR 006 external vs internal contracts | TypeSpec is “edge”; still documents **internal console** edges to public | **Boundary blur** |
| “Not JWT for integrations” | Frontends use cookie JWT; docs show Bearer; integrators need sk_* | **Three-way confusion** |

### Root cause summary

The monorepo optimized for **contract-first internal type safety** (ADR 005/006) and then **reused the same contracts as public developer documentation** (ADR 007) without a second filter for **external integration surface area**. LHDN is the only module that grew true integration primitives (keys, SDKs, ApiKey middleware); the hub did not re-center on that, and ops never got a keys UI.

---

## Recommendations (Prioritized)

### P0 — Unblock a real LHDN integration path

1. **Ship API Keys UI in ops-page** under Developer  
   - List (implement missing `GET /lhdn/api-keys`), create (test/live), one-time reveal, revoke  
   - Show prefix + name + created_at; never re-show full secret  
   - Deep-link to `hub.lazuar.com/docs/lhdn`  

2. **Fix LHDN OpenAPI base path**  
   - Change `@route("/api/v1/lhdn")` → `@route("/lhdn")` so server `/api/v1` + path compose correctly  
   - Align production server URL with real host (`hub.lazuar.com` or true `api.` DNS)  

3. **Normalize API key Authorization format**  
   - Pick one: either middleware accepts raw `sk_*` **or** SDKs always prepend `Bearer `  
   - Document the single format in Scalar description + SDK README  

4. **Split authorization policies**  
   - `API_CLIENT` should **not** manage keys/certificates by default  
   - Key management: human `ADMIN` only  
   - Integration: submit/get/cancel/list status (+ optional webhooks)  

### P1 — Make developers-page an integration hub, not a Swagger dump

5. **Re-curate public products**  
   - Primary: **LHDN** (and later Commerce public checkout)  
   - Demote or gate **Ops** (internal)  
   - Split **One**: public auth vs workspace admin — or remove admin from public hub  
   - Add **Commerce** (`docs-commerce.tsp` already exists — wire build + route + card)  

6. **Add Quickstarts** (MDX or static pages)  
   - “Submit first e-invoice in 5 minutes” (curl + TS + .NET)  
   - “Verify `X-Lazuar-Signature`”  
   - “Headless checkout link / public commerce”  

7. **Surface SDKs on hub**  
   - Install snippets, version, link to NPM/NuGet once published  
   - Run ADR 011 once so packages are actually public  

8. **Event catalog** for outbound webhooks  
   - Document payload shapes already dispatched by `OutboundWebhookDispatcherJob`  
   - Link ops Developer Webhooks ↔ docs  

### P2 — Contract hygiene & productization

9. Implement or remove TypeSpec ops that lack backends (`listApiKeys`, `taxpayer/validate` on Lazuar edge).  

10. Model `X-Tenant-Id` (and cookie vs bearer) in TypeSpec for admin surfaces; or exclude admin from public specs entirely.  

11. Set real `info.version` and hub badges from the same source.  

12. Certificate / LHDN taxpayer setup UI in ops (without this, keys alone cannot complete compliance integrations).  

13. TS SDK: auto `Idempotency-Key` parity with .NET.  

14. Restrict cache TTL / key last-four storage for list UX.  

### P3 — Platform DX maturity

15. Authenticated docs “Try it” with workspace-scoped test keys (optional).  

16. API request logs / usage against credit wallet (ties to prepaid utility model in README).  

17. CI publish SDKs on tag; changelog automation.  

18. Update monorepo README + ADR 007/016 examples to match `hub.lazuar.com/docs` deployment.  

19. Replace or clearly label `docs/postman` MyInvois collection vs Lazuar collection.  

20. Long-term: separate **Internal OpenAPI** (main.tsp for codegen) from **Public OpenAPI** (strict external surface) so developers-page never ships Ops chat by accident.

---

## File-by-File Notes

### `apps/developers-page/`

| Path | Notes |
|---|---|
| `app/page.tsx` | Hub landing; 4 modules; claims “integration guides” without delivering them; static cards only. |
| `app/layout.tsx` | Metadata “Lazuar API Documentation”; Geist fonts; no nav chrome. |
| `app/globals.css` | Tailwind v4 import only. |
| `app/one/route.ts` | Scalar for One (actually One+Billing per docs-one). |
| `app/ops/route.ts` | Scalar for Ops (+Billing). **Internal product on public hub.** |
| `app/billing/route.ts` | Scalar for Billing admin. |
| `app/lhdn/route.ts` | Scalar for LHDN; best integration candidate; inherits path bugs from YAML. |
| `lib/openapi.ts` | Good monorepo/Docker dual path resolution. |
| `next.config.ts` | `standalone` + optional `basePath` from env. |
| `Dockerfile` | Compiles TypeSpec in image; copies `dist` → `/app/openapi-specs`; healthcheck `GET /docs`. |
| `package.json` | Next 16.2.7, Scalar `@scalar/nextjs-api-reference` ^0.10.20, port 3002. |
| `README.md` | **Boilerplate**; no product docs. |
| `AGENTS.md` / `CLAUDE.md` | Next.js agent warning only. |
| `public/*` | Default Next assets + favicons. |

### `packages/api-spec/`

| Path | Notes |
|---|---|
| `main.tsp` | Full platform import graph for internal codegen. |
| `docs-one.tsp` | Product docs entry; **imports billing**. |
| `docs-ops.tsp` | Product docs entry; **imports billing**. |
| `docs-billing.tsp` | Clean billing-only scope. |
| `docs-lhdn.tsp` | Only entry with dual auth + prod server; good pattern for others. |
| `docs-commerce.tsp` | **Orphan** — not in `package.json` build. |
| `package.json` | Build omits commerce; watch only on main.tsp. |
| `tspconfig.yaml` | Emit OpenAPI3 `openapi.yaml`. |
| `README.md` | Dev-oriented TypeSpec how-to; examples still mention Community; BearerAuth-centric. |
| `common/models.tsp` | ProblemDetails, pagination, Id/Status responses. |
| `modules/lhdn/routes.tsp` | **`/api/v1/lhdn` double prefix**; full integration surface including api-keys. |
| `modules/lhdn/models.tsp` | Submit document, webhooks, GenerateApiKey*, ApiKeyDto (no key fingerprint field). |
| `modules/one/routes.tsp` | Auth + workspaces + webhooks; first-party. |
| `modules/one/models.tsp` | WebhookEndpointDto includes `secret_key`. |
| `modules/ops/routes.tsp` | Chat/agent — **should not be public integration surface**. |
| `modules/billing/routes.tsp` | Admin billing under BearerAuth. |
| `modules/commerce/public-routes.tsp` | Headless checkout/public portal — **undocumented on hub**. |
| `modules/commerce/admin-routes.tsp` | Console commerce. |
| `dist/*/openapi.yaml` | Generated artifacts; lhdn paths show double prefix; versions `0.0.0`. |

### ADRs

| Path | Relevance |
|---|---|
| `005-typespec-api-contract-generation.md` | Contract-first pipeline; internal types priority. |
| `006-separation-of-external-and-internal-contracts.md` | Edge vs MediatR; does not solve “which edge is public”. |
| `007-product-scoped-api-references.md` | Defines developers-page pattern; stale Community/Vault examples; domain name outdated vs `/docs`. |
| `011-sdk-publishing-runbook.md` | Manual LHDN SDK publish only. |
| `014-apps.md` | Module encyclopedia; documents `DeveloperApiKey` under LHDN; superapp framing. |
| `016-platform-domain-strategy.md` | api/ops/portal only — **developers path not specified**. |
| `020-lazuar-platform-integration-roadmap.md` | External vendor integrations (BYOK gateways, WhatsApp, Xero) — not partner DX for calling Lazuar. |
| `021-compliance-caas-pivot.md` | Strategic reason LHDN/compliance should dominate developer narrative. |

### Auth & keys (API)

| Path | Notes |
|---|---|
| `apps/lazuar-api/src/Lazuar.Api/Middleware/ApiKeyAuthenticationMiddleware.cs` | `Bearer sk_*` only; Dapper lookup on `lhdn.DeveloperApiKeys`; 5m cache. |
| `apps/lazuar-api/src/Lazuar.Api/Middleware/TenantSecurityMiddleware.cs` | Skips tenant header for ApiKey identity. |
| `apps/lazuar-api/src/Lazuar.Api/Program.cs` | JWT cookie; OrgAdmin includes `API_CLIENT`; maps LHDN endpoints. |
| `apps/lazuar-api/src/Lazuar.Api/EventHandlers/ApiKeyRevokedIntegrationEventHandler.cs` | Cache bust on revoke. |
| `Modules/Lhdn/Infrastructure/Endpoints.cs` | Generate/revoke keys; **no list**; no taxpayer validate route. |
| `Modules/Lhdn/Application/Commands/GenerateApiKeyCommand.cs` | Prefix + hash storage. |
| `Modules/Lhdn/Domain/Aggregates/DeveloperApiKey.cs` | Aggregate model. |

### Ops / portal credential UIs

| Path | Notes |
|---|---|
| `apps/ops-page/src/modules/workspace/pages/DeveloperSettingsPage.tsx` | **Webhooks only** — not API keys. |
| `apps/ops-page/src/modules/workspace/pages/DeliveryLogsPage.tsx` | Outbound delivery audit. |
| `apps/ops-page/src/App.tsx` | Routes `/developer/webhooks`, `/developer/logs` only. |
| `apps/ops-page/src/components/Sidebar.tsx` | Developer section labels. |
| `apps/ops-page/src/modules/workspace/pages/PaymentSettingsPage.tsx` | Third-party payment secrets (BYOK). |
| `apps/ops-page/src/modules/workspace/pages/EmailSettingsPage.tsx` | Resend key. |
| `apps/portal-page/**` | No developer credential management. |

### SDKs & generation

| Path | Notes |
|---|---|
| `packages/lhdn-sdk-ts/src/index.ts` | Factory; Authorization header; no Bearer enforcement docs. |
| `packages/lhdn-sdk-dotnet/src/LhdnClientFactory.cs` | Same auth; auto Idempotency-Key. |
| `packages/lhdn-sdk-ts/package.json` | `@lazuar/lhdn-sdk@0.1.0`. |
| `packages/lhdn-sdk-dotnet/Lazuar.Lhdn.Sdk.csproj` | NuGet metadata 0.1.0. |
| `Taskfile.yml` (`gen`, `gen:sdk-lhdn`) | Orchestrates full pipeline. |

### Deploy

| Path | Notes |
|---|---|
| `deploy/prod/Caddyfile` | `/docs*` → developers; `/api/*` → api; single host hub. |
| `deploy/prod/docker-compose.yml` | `developers` service + `OPENAPI_SPEC_ROOT`. |
| `docker-bake.hcl` | `developers-page` target, `NEXT_BASE_PATH=/docs`. |
| `mprocs-dev.yaml` | Local developers-page process. |
| Root `README.md` | Omits developers-page from structure; API-first marketing not reflected in hub. |

### Other

| Path | Notes |
|---|---|
| `docs/postman/postman_collection.json` | **MyInvois government** collection — not Lazuar partner API. |
| `packages/api-types-ts` / `api-types-dotnet` | Internal consumers of **main** OpenAPI, not product-scoped docs. |

---

### Bottom line

Lazuar has **solid contract infrastructure** (TypeSpec, product-scoped OpenAPI, Scalar hosting, LHDN API keys + middleware + Kiota SDKs) but the **developer product is unfinished**: the hub publishes **console-shaped APIs**, **omits commerce**, and **does not productize credential generation** in the frontend. The integration path the product owner describes is **architecturally sketched only for LHDN** and **blocked by missing UI, missing list endpoint, OpenAPI path/server bugs, and auth-format ambiguity**. Closing P0 items would turn the existing backend primitives into a credible first external DX slice; re-scoping the hub to integration surfaces (LHDN + public commerce) would align with the Compliance CaaS strategy rather than documenting the modular monolith’s internal edges.
