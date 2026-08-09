# TypeSpec Contracts — Organization, Quality & Maintenance

**Date:** 2026-08-09  
**Repo:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay`  
**Scope:** `packages/api-spec/**` entirely, gen pipeline (`Taskfile` `gen*`), `packages/api-types-ts`, `packages/api-types-dotnet`, LHDN Kiota SDKs, OpenAPI drift vs Minimal API, honesty gates.  
**Nature:** Uncondensed maintenance analysis. Read-only against the tree; no app code modified.  
**Related prior art:** `docs/001-gaps/13-typespec-api-contracts.md` (historical; many P0s fixed since), ADRs 005/006/007/011, `docs/contracts/openapi-vs-minimal-api.md`, `docs/api-versioning.md`, Phase C.8 contracts job in `.github/workflows/ci.yml`.

---

## 1. Executive summary

TypeSpec is the intended single source of truth (SSoT) for the HTTP edge. The pipeline is real, CI-gated, and product-scoped docs mostly work. Compared to the earlier gap report (`docs/001-gaps/13-typespec-api-contracts.md`), **several critical failures are fixed**:

| Historical P0 | Current status |
|---|---|
| LHDN `@route("/api/v1/lhdn")` double prefix | **Fixed** — routes are `@route("/lhdn")`; Kiota builds under `/lhdn` |
| Portal `portal/cancel` phantom | **Fixed** — implemented in `PublicEndpoints.cs` |
| LHDN `taxpayer/validate` / list API keys missing | **Fixed** — present in Lhdn `Endpoints.cs` |
| Billing `net-profit` / summary date filters | **Fixed** — endpoints implement both |
| Ops stream + system-message missing from TypeSpec | **Fixed** — in `ops/routes.tsp` |
| `docs-commerce` orphaned from build | **Fixed** — compiled + Scalar route + hub card |
| `docs-one` / `docs-ops` billing leakage | **Fixed** — product docs no longer import billing |
| OpenAPI `info.version` `0.0.0` | **Fixed** — all specs emit `1.0.0` |
| Taskfile sources only root `*.tsp` | **Fixed** — `packages/api-spec/**/*.tsp` |

**Residual maintenance debt (this report’s focus):**

1. **Local DTO dual-definition** on hot paths (Commerce subscribers, Payments integration checkouts) — TypeSpec DTOs exist but endpoints bind hand-written records.
2. **Impl-only product routes** still outside TypeSpec (billing signed PDF, communications broadcast status/preview + public compliance, messaging notify/logs).
3. **Shape drift** (broadcast targeting fields advertised, not mapped).
4. **Orphan models** polluting the monolith OpenAPI (CRM full set, `LinkedCheckoutDto`, `PaymentRecordDto`, unused `PaymentWebhookPayloadDto` as schema-only).
5. **Generated client hygiene:** dead `Generated/Models.cs`, awkward NSwag `Amount_myr` names, payments OpenAPI path trailing slash, payments docs missing security schemes.
6. **Stale docs:** `packages/api-spec/README.md` still describes `auth/` / `community/`; ADR 007 examples still Community/Vault; `docs/001-gaps/13-…` is a snapshot of an older tree.
7. **No automated OpenAPI-vs-Minimal-API path honesty test** — CI only regenerates clients and `git diff`s them; it does not prove paths match `Endpoints*.cs`.

Overall quality is **production-usable for LHDN + Payments M2M + Commerce public**, with **console-admin surfaces largely covered**, and **internal modules (Messaging, CRM, inbound gateway webhooks) intentionally or accidentally thin**.

---

## 2. Inventory — file structure

### 2.1 Package layout (actual)

```text
packages/api-spec/
├── common/
│   └── models.tsp                 # Core: ProblemDetails, IdResponse, StatusResponse, PaginatedResponse, LinkedCheckoutDto
├── modules/
│   ├── one/          models.tsp, routes.tsp
│   ├── ops/          models.tsp, routes.tsp
│   ├── billing/      models.tsp, routes.tsp
│   ├── lhdn/         models.tsp, routes.tsp
│   ├── commerce/     models.tsp, admin-routes.tsp, public-routes.tsp
│   ├── communications/ models.tsp, admin-routes.tsp
│   ├── payments/     models.tsp, routes.tsp
│   ├── platform/     routes.tsp          # imports one + commerce models
│   ├── crm/          models.tsp          # models only — no routes
│   └── messaging/    models.tsp          # intentionally blank
├── main.tsp                          # Monolith orchestrator (all imports)
├── docs-one.tsp
├── docs-ops.tsp
├── docs-billing.tsp
├── docs-lhdn.tsp
├── docs-commerce.tsp                 # commerce + communications
├── docs-payments.tsp
├── tspconfig.yaml                    # openapi3 → openapi.yaml
├── package.json                      # multi-entry build script
├── README.md                         # STALE examples
└── dist/                             # gitignored intermediate; present after gen / Docker build
    ├── openapi.yaml                  # full platform
    ├── one|ops|billing|lhdn|commerce|payments/openapi.yaml
```

**Not present (README claims them):** `modules/auth/`, `modules/community/`. Those modules were removed (ADR 022).

### 2.2 Entry points & emit config

| Artifact | Role |
|---|---|
| `main.tsp` | Full import graph → `dist/openapi.yaml` → NSwag + openapi-typescript |
| `docs-*.tsp` | Product-scoped OpenAPI for Scalar on `lazuar-developers` |
| `tspconfig.yaml` | `@typespec/openapi3`, `output-file: openapi.yaml` only — no multi-version, no openapi-info override beyond `@info` on services |
| `package.json` `build` | Compiles main + six product docs in one long shell chain |

**Build command (current):**

```text
tsp compile main.tsp           → dist/openapi.yaml
tsp compile docs-one.tsp       → dist/one/openapi.yaml
tsp compile docs-ops.tsp       → dist/ops/openapi.yaml
tsp compile docs-billing.tsp   → dist/billing/openapi.yaml
tsp compile docs-lhdn.tsp      → dist/lhdn/openapi.yaml
tsp compile docs-commerce.tsp  → dist/commerce/openapi.yaml
tsp compile docs-payments.tsp  → dist/payments/openapi.yaml
```

All emit `info.version: 1.0.0` and dual servers (`https://hub.lazuar.com/api/v1`, `http://localhost:8080/api/v1`).

### 2.3 Downstream packages

| Package | Generator | Input | Output | Consumers |
|---|---|---|---|---|
| `@repo/api-types-ts` | openapi-typescript | `dist/openapi.yaml` | `src/index.ts` (`paths` + `components`) | `lazuar-ops`, `lazuar-admin`, `lazuar-portal` |
| `@repo/api-types-dotnet` | NSwag (`nswag.json`) | `dist/openapi.yaml` | **`Lazuar.ApiContracts.cs` only** (csproj excludes other .cs) | All backend modules via `Lazuar.ApiTypes` |
| `@lazuar/lhdn-sdk` (TS) | Kiota | `dist/lhdn/openapi.yaml` | `src/generated/**` | External publish (runbook ADR 011) |
| `Lazuar.Lhdn.Sdk` (.NET) | Kiota | `dist/lhdn/openapi.yaml` | `src/Generated/**` | External publish |

**Dead sibling:** `packages/api-types-dotnet/Generated/Models.cs` — still on disk, still in CI `git diff` list, **not compiled** (`EnableDefaultCompileItems=false`; only `Lazuar.ApiContracts.cs` is included). Structure of Models.cs looks like an older NSwag style (constructor-injected POCOs) vs the live property-based file.

### 2.4 Pipeline (`Taskfile.yml`)

```text
task gen
  → gen:spec          (pnpm build in packages/api-spec)
  → gen:types-ts      (openapi-typescript)
  → gen:types-dotnet  (dotnet nswag run nswag.json)
  → gen:sdk-lhdn      (kiota TS + C# from dist/lhdn/openapi.yaml)
```

**Sources / generates (task cache):**

```yaml
sources:
  - "packages/api-spec/**/*.tsp"
  - "packages/api-spec/tspconfig.yaml"
generates:
  - "packages/api-spec/dist/openapi.yaml"
  - "packages/api-types-ts/src/index.ts"
  - "packages/api-types-dotnet/Lazuar.ApiContracts.cs"
```

Note: `generates` does **not** list LHDN SDK trees or product-scoped `dist/*/openapi.yaml`. Cache invalidation for pure docs-entry changes may still rebuild via sources glob (good). SDK outputs are not part of Task’s `generates` list (minor hygiene).

### 2.5 CI honesty gate (Phase C.8)

`.github/workflows/ci.yml` job `contracts`:

```text
task gen --force
git diff --exit-code -- \
  packages/api-types-ts/src \
  packages/api-types-dotnet/Generated \
  packages/api-types-dotnet/Lazuar.ApiContracts.cs \
  packages/lhdn-sdk-ts/src/generated \
  packages/lhdn-sdk-dotnet/src/Generated
```

**What it proves:** committed clients match TypeSpec after a clean gen.  
**What it does not prove:** Minimal API routes match OpenAPI paths; auth policies match `@useAuth`; response shapes match runtime error formats.

`packages/api-spec/dist/` is gitignored (`dist/` in root `.gitignore`). Developers hub Docker image **rebuilds** TypeSpec at image build time and copies `dist` → `/app/openapi-specs` (`OPENAPI_SPEC_ROOT`). Local Scalar routes read monorepo `packages/api-spec/dist/<module>/openapi.yaml`.

---

## 3. Architectural intent vs reality

### 3.1 ADR 005 — TypeSpec as SSoT

| Claim | Reality |
|---|---|
| TypeSpec → OpenAPI → TS + C# | **True** |
| Never edit generated files | **Policy true**; live file is `Lazuar.ApiContracts.cs` |
| Commit generated clients | **True** (TS + C# + Kiota) |
| `dist/` transient | **True** (gitignored); Docker/local hub depend on rebuild |
| Output path `Generated/Models.cs` | **False** — live output is `Lazuar.ApiContracts.cs`; ADR text stale |

### 3.2 ADR 006 — External vs internal contracts

| Policy | Reality |
|---|---|
| TypeSpec DTOs = HTTP edge only | **Mostly true** for modules that bind `Lazuar.ApiTypes.*` |
| Module `Contracts/` = MediatR only | **True** for Commands/Events |
| Endpoints as ACL | **True** when mapping DTO → Command |
| No integration events in TypeSpec | **True** |

**Violations / gray areas (current):**

1. **Local edge DTOs that duplicate TypeSpec**
   - `CreateManualSubscriberRequest`, `GenerateCustomerPortalRequest/Response`, `RecordSubscriberPaymentRequest` in `SubscriberEndpoints.cs` while TypeSpec defines `CreateManualSubscriberDto`, `GenerateCustomerPortalRequestDto/ResponseDto`, `RecordPaymentRequestDto`.
   - `CreateIntegrationCheckoutRequest` + `IntegrationCheckoutResponseDto` in `IntegrationEndpoints.cs` while TypeSpec defines `CreateIntegrationCheckoutRequestDto` / `IntegrationCheckoutResponseDto` (also: TypeSpec `float64` vs runtime `decimal` for amount).
2. **Internal command bound as HTTP body**
   - Messaging `POST /messaging/notify` binds `SendTenantNotificationCommand` directly (ADR 006 anti-pattern). OrgAdmin-gated, not product-doc’d.
3. **Module-local response DTOs for admin UX**
   - `BroadcastStatusDto`, `BroadcastCostPreviewDto` in `Modules.Communications.Contracts` with hand-rolled shapes — not in TypeSpec, not in generated clients.
4. **CRM models in TypeSpec with zero HTTP surface**
   - `ClientProfileDto` etc. emit into monolith OpenAPI components; CRM has no Minimal API endpoints (command handlers only). Premature edge contracts.
5. **Query services returning `Lazuar.ApiTypes`**
   - e.g. communications/one query services — acceptable if mapping stays near infra, but edge types leak inward.

### 3.3 ADR 007 — Product-scoped references

| Product | docs entry | build | dist | Scalar route | Hub card | Scope purity |
|---|---|---|---|---|---|---|
| One | `docs-one.tsp` | yes | yes | `/one` | yes | **Clean** (One only) |
| Ops | `docs-ops.tsp` | yes | yes | `/ops` | yes (Internal) | **Clean** (Ops only) |
| Billing | `docs-billing.tsp` | yes | yes | `/billing` | yes | **Clean** |
| LHDN | `docs-lhdn.tsp` | yes | yes | `/lhdn` | yes (Primary) | **Clean** (+ One API key model aliases by design) |
| Commerce | `docs-commerce.tsp` | yes | yes | `/commerce` | yes | **Bundles Communications admin** intentionally |
| Payments | `docs-payments.tsp` | yes | yes | `/payments` | yes (Cashier) | **Clean** (integration checkouts + webhook payload schema) |
| Platform | — | only via main | monolith only | no | no | Superadmin-only; intentional thin product docs |
| Messaging | — | no | — | no | no | Internal |
| CRM | models in main | no routes | schemas only | no | no | Noise |

**Commerce docs bundling Communications** is a product decision (CaaS console couples templates/broadcasts with catalog). It is **not** pure “Commerce bounded context.” If Communications grows a public compliance surface, consider `docs-communications.tsp` or keep public routes out of product docs and document them as operational.

### 3.4 Path honesty document (`docs/contracts/openapi-vs-minimal-api.md`)

This is the living honesty map. It correctly states:

- Host prefix `/api/v1`; clients use relative paths.
- Intentional non-OpenAPI allowlist: gateway inbound webhooks, messaging notify/logs, host health.
- Frontend dark-matter (MVP-hide ops chat, billing profile) per ADR 022/023.
- Residual: platform TypeSpec thin; communications public may lag; CSV export may use raw fetch.

This report **extends** that document with concrete residual diffs found on 2026-08-09 (below).

---

## 4. Module-by-module TypeSpec quality

### 4.1 Common (`common/models.tsp`)

**Contents:** `ProblemDetails`, `ValidationProblemDetails`, `IdResponse`, `StatusResponse`, `PaginatedResponse<T>`, `LinkedCheckoutDto`, `ProblemDetailsResponse` error model.

**Strengths:** Shared error union pattern used consistently on routes. Pagination generic is correct for TypeSpec → OpenAPI.

**Issues:**

- `LinkedCheckoutDto` appears unused by any route/model reference chain that maps to operations — still emitted in monolith OpenAPI as `Core.LinkedCheckoutDto`. Orphan; remove or wire.
- Error model advertises 400|401|403|404|500 only — runtime also returns 402-ish semantics (LHDN credits as 400 body with Status 402 historically), 409 (payments idempotency), 202 Accepted (messaging notify). Spec is incomplete for non-200 success and conflict.

### 4.2 One (`modules/one/`)

**Routes:** Public register, auth (login/logout/forgot/reset/verify/resend), me/profile/password/entitlements, workspaces CRUD-ish, members/invites, apps toggle, webhooks multi-endpoint, storage presign, API keys CRUD, integrator provision.

**Alignment with `One/Infrastructure/Endpoints.cs`:** High. Comment correctly notes LHDN config lives under `/lhdn`, not One (phantom One lhdn-config removed).

**Notes:**

- Auth annotations: most ops `@useAuth(BearerAuth)`; browsers use cookie sessions — OpenAPI implies Bearer only. Known DX gap for Scalar “Try it” vs real ops console.
- Provision route documents special auth (provision key / SUPER_ADMIN) in `@doc` but not as a distinct security scheme.
- API key DTOs are owned by One and **aliased** into LHDN models — good ownership model.
- `GET /one/integrations/payments/checkouts/_scope-probe` exists in backend for scope probing — **not** in TypeSpec (internal probe; OK if never productized).

### 4.3 Ops (`modules/ops/`)

**Routes:** conversations list/messages, chat, **stream (SSE)**, system-message, execute-action, rename, delete, resolve UI request.

**Alignment:** High with `Ops/Infrastructure/Endpoints.cs`. Stream is documented as raw fetch / EventSource (correct for OpenAPI3 limits).

**Notes:**

- Auth: TypeSpec Bearer; impl roles `CLIENT`, `ADMIN` — stricter than “any bearer.”
- `ChatStreamChunkDto` is model-only in OpenAPI (stream returns `text/event-stream` string body) — intentional; model helps frontend types (`use-chat-stream.ts` imports components).

### 4.4 Billing (`modules/billing/`)

**Admin:** ledger (paginated + filters), document download URL, summary (with date query params), net-profit, credits, packages, top-up, profile get/put.  
**Public:** tenant profile, draft document (HMAC).

**Alignment:**

| Spec | Impl | Status |
|---|---|---|
| Admin surface above | Present | ✅ |
| Summary `from_date`/`to_date` | Passed to query service | ✅ (was drift; fixed) |
| net-profit | Present | ✅ |
| Public draft PDF | Present | ✅ |
| `GET /public/billing/{tenantSlug}/documents/{ledgerEntryId}?sig&exp` | Present (signed final PDF redirect) | **Impl-only** |

**Action:** Add public signed document route to TypeSpec (or document as intentional internal redirect-only if product never needs typed client).

### 4.5 LHDN (`modules/lhdn/`)

**Routes:** taxpayer validate, documents submit/get/cancel, webhooks CRUD, workspace lhdn-config get/put, certificate put, api-keys list/create/delete.

**Path prefix:** `@route("/lhdn")` — **correct** relative to server `/api/v1`. Kiota clients build under `/lhdn` (verified in `lhdn-sdk-ts/src/generated/lhdn/index.ts`).

**docs-lhdn.tsp strengths:**

- Best external product posture: dual `BearerAuth | ApiKeyAuth` for Scalar.
- Service description documents `sk_test_` / `sk_live_`, Idempotency-Key on submit.
- Imports One models only for API key DTO aliases.

**Residual:**

- Outbound LHDN webhook **payload** schemas not in TypeSpec (only registration APIs).
- Auth on individual ops is `@useAuth(BearerAuth)` even when runtime uses scoped integration policies (`IntegrationLhdnDocumentsWrite/Read` vs OrgAdmin for admin surfaces). Spec does not express scope matrix.
- SDK package version `0.1.0` independent of OpenAPI `1.0.0`.

### 4.6 Commerce (`modules/commerce/`)

**Admin:** products, dunning campaigns, payment-config, subscribers (+ export, portal-link, cancel, record-payment, dunning pause/resume), transactions/refund, coupons, stats, custom-checkouts, mark-paid.

**Public:** product by slug, validate-coupon, checkout, portal get/cancel, checkout status (tenant path + legacy), custom checkout get, arrears, update-payment.

**Alignment:** Paths largely match. Portal cancel is implemented. Portal-link is in TypeSpec.

**Dual DTO debt (high maintenance cost):**

```text
TypeSpec                          Endpoint binds
─────────────────────────────────────────────────────────────
CreateManualSubscriberDto         CreateManualSubscriberRequest (local record)
GenerateCustomerPortalRequestDto  GenerateCustomerPortalRequest (local)
GenerateCustomerPortalResponseDto GenerateCustomerPortalResponse (local)
RecordPaymentRequestDto           RecordSubscriberPaymentRequest (local)
```

Shapes are similar (snake_case fields), so runtime works, but **SSoT is split**. Changes require dual edits; CI will not catch divergence.

**Orphan model:** `PaymentRecordDto` in commerce models — no route uses it; still in generated clients.

**Public surface honesty:** Docs claim magic-link / billing-link history was cleaned; current TypeSpec public routes do **not** include magic-link or billing-link (good — cancel remains as the customer self-service cancel path).

### 4.7 Communications (`modules/communications/`)

**TypeSpec admin:** templates CRUD-ish, variables, preview, reminders/test, broadcasts POST, email-config get/put.

**Impl extras (not in TypeSpec):**

| Route | Location |
|---|---|
| `GET /admin/communications/broadcasts/preview` | BroadcastEndpoints |
| `GET /admin/communications/broadcasts/{id}` | BroadcastEndpoints |
| `DELETE /admin/communications/templates/legacy-cleanup` | TemplateEndpoints |
| `GET /public/communications/unsubscribe` | PublicComplianceEndpoints |
| `POST /public/communications/webhooks/resend` | PublicComplianceEndpoints |

**Shape drift — broadcast targeting:**

TypeSpec `CreateBroadcastRequestDto` includes:

- `target_plan_id?`
- `target_status?`
- `target_is_reminder_only?`

`SendBroadcastCommand` supports the same optional targeting parameters.  
`BroadcastEndpoints` maps only subject/bodies/channel — **drops targeting fields**. Frontend/OpenAPI advertise filters that silently do nothing.

**DTO ownership:** Broadcast status/preview live in module Contracts with PascalCase property names (`RecipientCount`, etc.) while platform convention is snake_case JSON via STJ policy / TypeSpec. Inconsistent wire style if ever exposed to typed clients.

### 4.8 Payments (`modules/payments/`)

**TypeSpec:**

- `POST /integrations/payments/checkouts/` (note trailing slash from `@route("/")` under group)
- `GET /integrations/payments/checkouts/{checkoutId}`
- Models: create request, response, `PaymentWebhookPayloadDto` (outbound envelope — schema only, no route)

**Impl:**

- `IntegrationEndpoints.cs` — same paths (POST `/`, GET `/{checkoutId:guid}`), scoped policies `IntegrationPaymentsCheckoutsWrite/Read`.
- Local DTOs instead of `Lazuar.ApiTypes` generated types.
- Amount: runtime `decimal`; TypeSpec `float64` → OpenAPI `number`/double → NSwag `double`. **Money should not be binary float long-term.** Prefer documenting decimal as string or using a consistent money type strategy.

**Inbound gateway webhooks:**

- Runtime: `POST /webhooks/payments/{gatewayType}/{tenantId}` in `Payments/Infrastructure/Endpoints.cs`.
- TypeSpec: **absent** (allowlisted as intentional in honesty doc). Correct for “not product API,” but integrators debugging gateway setup have no schema for expected gateway payloads (those are provider-specific anyway).

**docs-payments.tsp:** Excellent product description (what is / is not this product). **Missing:** `@useAuth` / security schemes — payments OpenAPI has **no** `security` / `securitySchemes` section (unlike LHDN). Scalar “Try it” and generated security metadata are weaker than LHDN.

**Trailing slash path:** OpenAPI path is `/integrations/payments/checkouts/` which is awkward for openapi-fetch path keys and docs. Prefer empty route on the interface or `@route("")` patterns that emit without trailing slash.

**Idempotency:** Documented in model/docs as header or body field, but TypeSpec route does **not** declare `@header("Idempotency-Key")` (LHDN submit does). Implementers must read prose, not schema.

### 4.9 Platform (`modules/platform/routes.tsp`)

Login/logout/me + system payment-config. Imports One + Commerce models (shared shapes). Present in monolith OpenAPI only. Matches superadmin endpoints under `/platform`. Coverage “thin” only in the sense of few ops — those ops are complete.

### 4.10 CRM (`modules/crm/models.tsp`)

Full DTO set (`ClientProfileDto`, create/update, billing address). **No routes.** Imported by `main.tsp` → schemas in monolith OpenAPI → NSwag + TS components. CRM backend is command/query only (no HTTP).  

**Recommendation:** Remove from `main.tsp` imports until CRM exposes HTTP, or mark as internal and exclude from OpenAPI emit if TypeSpec tooling allows. Do not invent admin CRUD in TypeSpec without product commitment.

### 4.11 Messaging (`modules/messaging/models.tsp`)

Intentionally blank with a comment about templates migrating to application modules (comment still says “Community” — stale).  

Runtime has OrgAdmin `POST /messaging/notify` (command binding) and `GET /messaging/delivery-logs` (local record DTO). Honesty doc marks these internal.  

**Recommendation:** Keep out of product docs. Either (a) introduce a minimal TypeSpec internal interface if console ever needs typed clients, or (b) leave as allowlisted impl-only and stop binding MediatR commands as the body.

---

## 5. Duplication between TypeSpec files

### 5.1 Intentional / healthy sharing

| Pattern | Where | Verdict |
|---|---|---|
| `common/models.tsp` shared by main + all docs | All entry points | **Good** |
| One API key models aliased in LHDN | `lhdn/models.tsp` | **Good** (single ownership) |
| Platform reuses One login + Commerce payment config models | `platform/routes.tsp` | **Good** |
| docs-commerce imports communications | Product bundling | **OK if deliberate** |

### 5.2 Repeated server/info boilerplate

Every `docs-*.tsp` and `main.tsp` repeats:

```tsp
@server("https://hub.lazuar.com/api/v1", "Production server")
@server("http://localhost:8080/api/v1", "Local development server")
@info(#{ version: "1.0.0" })
namespace LazuarApi;
```

**Maintenance risk:** Server URL renames require N+1 edits. Consider a shared `servers.tsp` / library file imported by all entry points (TypeSpec allows shared models; service decorators may need to stay per entry).

### 5.3 Dual HTTP DTOs (TypeSpec + C# local)

Already listed in §3.2 / §4.6 / §4.8. This is the **highest-cost duplication** because it is silent and CI does not detect it.

### 5.4 API keys on two route surfaces

- `GET/POST/DELETE /one/api-keys` (One)
- `GET/POST/DELETE /lhdn/api-keys` (LHDN façade)

Same DTOs (One ownership). Backend implements both (LHDN admin group + One orgAdmin). **Not harmful duplication** if façades stay thin; document that keys are platform-owned.

### 5.5 Commerce payment-config vs Platform payment-config

Same TypeSpec models (`PaymentConfigDto`, `SavePaymentConfigRequestDto`) on:

- `/admin/commerce/payment-config` (tenant BYOK)
- `/platform/payment-config` (system superadmin)

**Correct reuse.** Runtime must keep semantics distinct (tenant vs system store).

---

## 6. Naming consistency with backend modules

### 6.1 Folder / namespace map

| Backend `Modules/` | TypeSpec `modules/` | Namespace | HTTP prefix(es) |
|---|---|---|---|
| One | one | `LazuarApi.One` | `/one` |
| Ops | ops | `LazuarApi.Ops` | `/ops` |
| Billing | billing | `LazuarApi.Billing` | `/admin/billing`, `/public/billing` |
| Lhdn | lhdn | `LazuarApi.Lhdn` | `/lhdn` |
| Commerce | commerce | `LazuarApi.Commerce` | `/admin/commerce`, `/public/commerce` |
| Communications | communications | `LazuarApi.Communications` | `/admin/communications` (+ public impl-only) |
| Payments | payments | `LazuarApi.Payments` | `/integrations/payments/*` (webhook prefix separate) |
| CRM | crm | `LazuarApi.Crm` | (none) |
| Messaging | messaging | `LazuarApi.Messaging` | (impl `/messaging`) |
| (host) Platform | platform | `LazuarApi.Platform` | `/platform` |

**Consistency grade: high** for active products. Naming uses lowercase folders matching product language (not always exact C# folder casing: `Lhdn` vs `lhdn` — fine for package paths).

### 6.2 Route file split conventions

| Module | Pattern |
|---|---|
| one, ops, billing, lhdn, payments | single `routes.tsp` |
| commerce | `admin-routes.tsp` + `public-routes.tsp` |
| communications | `admin-routes.tsp` only (no public TypeSpec yet) |
| platform | routes only (no models file) |

**Recommendation:** When Communications public compliance is productized, add `public-routes.tsp` (mirror commerce). Do not dump public unsubscribe/resend into admin-routes.

### 6.3 OperationId / interface naming

Interfaces: `OneOperations`, `OpsOperations`, `AdminBillingOperations`, `PublicBillingOperations`, `LhdnOperations`, `AdminCommerceOperations`, `PublicCommerceOperations`, `AdminCommunicationsOperations`, `IntegrationCheckoutOperations`, `PlatformOperations`.

OpenAPI `operationId`s become `AdminBillingOperations_getLedgerEntries` etc. → openapi-typescript `operations[...]`. Readable enough; slightly verbose.

### 6.4 Snake_case wire format

TypeSpec fields use snake_case (`amount_myr`, `is_email_verified`). NSwag CamelCase property name generator + `JsonPropertyName` yields C# `Amount_myr` — ugly but stable with ASP.NET snake_case JSON. Frontend TS paths use snake_case in JSON body types. **Do not rename without a versioned break.**

---

## 7. What should be split / merged

### 7.1 Split (recommended)

| Item | Why |
|---|---|
| Communications public routes → `public-routes.tsp` (when documented) | Mirror commerce; keep admin vs public clear |
| Optional `docs-communications.tsp` | If public compliance or M2M messaging becomes a product surface separate from Commerce console |
| Payments auth into TypeSpec security schemes | Split “prose docs” from machine-readable auth (currently only LHDN does dual auth well) |
| Shared `servers.tsp` / `version.tsp` | Reduce boilerplate across docs entry points |

### 7.2 Merge / consolidate (recommended)

| Item | Why |
|---|---|
| Delete or stop generating `Generated/Models.cs` | Dead twin of `Lazuar.ApiContracts.cs` |
| Drop CRM models from `main.tsp` until HTTP exists | Reduce OpenAPI/NSwag noise |
| Remove `LinkedCheckoutDto`, `PaymentRecordDto` if unused | Same |
| Collapse local Commerce/Payments request records into generated DTOs | Restore SSoT |
| Messaging blank models file | Either delete import from main or add a one-line package comment only (importing empty namespace is noise) |

### 7.3 Do **not** merge

| Anti-merge | Why |
|---|---|
| TypeSpec DTOs into MediatR Contracts | ADR 006 — keep edge vs internal separate |
| Product docs into one mega Scalar | ADR 007 — audience isolation works; hub already multi-card |
| LHDN into main-only (drop product docs) | External SDK depends on `dist/lhdn` |
| Inbound gateway webhooks into public product OpenAPI | Different audience; allowlist is correct |

### 7.4 Build script maintainability

`package.json` `build` is a single long `&&` chain of seven `tsp compile` invocations.  

**Improvements:**

- Script array / `for` loop over entry points to avoid missing a new `docs-*.tsp` again.
- Or a small `scripts/build-specs.mjs` that discovers `docs-*.tsp` + `main.tsp`.
- Fail fast if `dist/<product>/openapi.yaml` missing when developers hub expects it.

---

## 8. Generated client hygiene

### 8.1 `@repo/api-types-ts`

**Pros:**

- Path-based `paths` interface ideal for openapi-fetch.
- Single monolith file keeps ops/admin/portal imports simple.
- CI-gated.

**Cons:**

- Monolith includes admin + platform + internal-ish schemas (CRM orphans) — frontends can accidentally type paths they should not call.
- Phantom fields (broadcast targets) give false compile-time confidence.
- Trailing-slash checkout path is an awkward key: `"/integrations/payments/checkouts/"`.
- Huge single `index.ts` (thousands of lines) — acceptable for monorepo, painful to review diffs.

**No product-scoped TS packages** — only LHDN has a separate SDK. Payments M2M integrators outside the monorepo have OpenAPI YAML only (no published payments SDK).

### 8.2 `@repo/api-types-dotnet`

**Pros:**

- DTO-only generation (`generateClientClasses: false`) matches ADR 006 edge POCOs.
- Explicit csproj include of one file prevents accidental compile of junk.

**Cons:**

- Property names `Amount_myr`, `Is_email_verified` — readability tax across all modules.
- `AdditionalProperties` dictionaries on many types — can hide typos at runtime if STJ extension data swallows unknowns inconsistently.
- `Generated/Models.cs` dead; CI still diffs it (if someone deletes it without regenerating both, or if gen stops writing it, CI behavior depends on whether NSwag still emits it — currently nswag only writes `Lazuar.ApiContracts.cs`, so **Models.cs is frozen stale forever** unless hand-deleted; CI will not update it on gen, only fail if someone edits it… actually `git diff` after gen only fails if gen **changes** tracked files. Stale Models.cs never changes → CI green while dead code sits. **Hygiene fail.**
- Money as `double` for amounts.

### 8.3 LHDN Kiota SDKs

**Pros:**

- Dedicated product client; path prefix fixed.
- Clean request builders under `/lhdn`.
- Published package story (ADR 011).

**Cons:**

- Auth: Bearer vs raw ApiKey header dual scheme — runtime expects `Authorization: Bearer sk_…`. SDK consumers must format correctly.
- Version `0.1.0` vs OpenAPI `1.0.0`.
- Outbound webhook verification not in SDK (only HTTP API).
- Kiota preview dependencies (`1.0.0-preview.*`) — supply-chain / stability note for publish.

### 8.4 OpenAPI intermediate (`dist/`)

- Gitignored correctly.
- Docker rebuilds for hub — good.
- Local hub needs `task gen:spec` (or full `task gen`) before Scalar works — document in developers README if not already.
- Product purity restored for one/ops/billing/lhdn/payments; commerce includes communications.

---

## 9. Gaps vs Minimal API (honesty table)

Legend: **OK** aligned · **Spec-only** TypeSpec without impl · **Impl-only** impl without TypeSpec · **Dual** both exist but different DTO source · **Shape** both exist, mapping incomplete · **Allowlist** intentional omit

### 9.1 By module

#### One

| Item | Status |
|---|---|
| Core auth/workspace/webhook/api-keys/provision/storage | OK |
| `_scope-probe` | Impl-only (internal) |
| Cookie vs Bearer | Shape (auth modeling) |

#### Ops

| Item | Status |
|---|---|
| Chat CRUD + stream + system-message + execute-action | OK |
| Role restrictions stricter than OpenAPI | Shape (auth) |

#### Billing

| Item | Status |
|---|---|
| Admin ledger/summary/net-profit/credits/profile | OK |
| Public profile + draft PDF | OK |
| Public signed final document `…/documents/{ledgerEntryId}` | **Impl-only** |

#### LHDN

| Item | Status |
|---|---|
| Documents, cancel, validate, webhooks, config, cert, api-keys | OK |
| Outbound webhook payload schema | Missing (docs-only / event catalog elsewhere) |
| Scope-specific auth schemes | Shape |

#### Commerce

| Item | Status |
|---|---|
| Admin product/dunning/payment-config/subscribers/transactions/coupons/stats/custom-checkouts | Paths OK |
| Create subscriber / portal-link / record-payment DTOs | **Dual** |
| Public checkout/portal/cancel/status/arrears | OK |
| `PaymentRecordDto` | Spec model orphan |

#### Communications

| Item | Status |
|---|---|
| Templates, preview, email-config, reminders/test, broadcast POST | Paths OK |
| Broadcast targeting fields | **Shape** (dead fields) |
| Broadcast preview/status GET | **Impl-only** |
| Templates legacy-cleanup | **Impl-only** (ops utility) |
| Public unsubscribe + Resend webhook | **Impl-only** |

#### Payments

| Item | Status |
|---|---|
| Integration checkouts POST/GET | Paths OK; **Dual** DTOs; trailing slash in OpenAPI |
| Idempotency-Key header in schema | Missing (prose only) |
| Security schemes on product OpenAPI | Missing |
| Outbound `PaymentWebhookPayloadDto` | Schema-only (good for docs; no route) |
| Inbound `POST /webhooks/payments/{gateway}/{tenantId}` | **Allowlist** |

#### Platform

| Item | Status |
|---|---|
| auth + payment-config | OK |

#### Messaging

| Item | Status |
|---|---|
| notify + delivery-logs | **Allowlist** / wrong boundary (command body) |

#### CRM

| Item | Status |
|---|---|
| Models only | Spec noise |

### 9.2 Cross-cutting error contract

- TypeSpec almost always returns `T | ProblemDetailsResponse`.
- Many endpoints return `BadRequest<string>`, raw strings, or `StatusResponse` with error message in `status` field (e.g. cancel subscriber).
- Payments integration uses `ProblemDetails` with extension `code` — better pattern; not fully reflected as typed error unions in TypeSpec.
- API key middleware historically returns `{ error: "…" }` — may still diverge from RFC7807.

### 9.3 Auth contract

| Reality | OpenAPI |
|---|---|
| Cookie JWT for browser apps | Mostly Bearer only |
| `sk_test_` / `sk_live_` Bearer for M2M | Documented well for LHDN; weak for Payments OpenAPI security block |
| OrgAdmin / SUPER_ADMIN / role policies | Not modeled as OAuth scopes or distinct schemes |
| Provision key header | Prose only |

---

## 10. Product docs & developers hub

| Concern | Status |
|---|---|
| Hub cards for LHDN, Payments, One, Commerce, Billing, Ops | Present |
| Guides: quickstart, payments-cashier, auth, webhooks | Present |
| Scalar routes for all built product specs | Present |
| Dockerfile rebuilds TypeSpec | Present |
| Ops marked Internal on landing | Present |
| Platform superadmin docs | Absent (OK) |
| Communications as standalone product card | Absent (bundled under Commerce OpenAPI) |
| Event catalog page vs TypeSpec webhook payload models | Split: webhooks page + `PaymentWebhookPayloadDto` schema; Commerce lifecycle events not all in TypeSpec as payload models |

**docs-commerce** description is high quality (integrator vs console-only). Admin routes still appear in Commerce Scalar — intentional “full module reference” but can confuse pure integrators who should only use public + webhooks. Consider a **public-only** emit (`docs-commerce-public.tsp`) later if DX suffers.

---

## 11. Improvement opportunities (prioritized)

### P0 — Correctness / SSoT integrity

1. **Eliminate dual request DTOs**
   - Commerce: bind `CreateManualSubscriberDto`, `GenerateCustomerPortalRequestDto/ResponseDto`, `RecordPaymentRequestDto` from `Lazuar.ApiTypes`.
   - Payments: bind generated integration checkout DTOs (or regenerate after fixing decimal strategy).
2. **Map or remove broadcast targeting fields**
   - Pass `target_plan_id` / `target_status` / `target_is_reminder_only` into `SendBroadcastCommand`, **or** delete them from TypeSpec.
3. **Fix payments OpenAPI path trailing slash**
   - Adjust TypeSpec so path is `/integrations/payments/checkouts` not `…/checkouts/`.
4. **Declare `Idempotency-Key` header** on payments create (mirror LHDN).

### P1 — Completeness for existing UI/ops features

5. Add TypeSpec for:
   - Billing public signed document route.
   - Communications `GET broadcasts/preview`, `GET broadcasts/{id}` (+ models).
   - Optionally public communications compliance routes (or explicit allowlist note in honesty doc with “never type against these from portal”).
6. Add security schemes to `docs-payments.tsp` (Bearer for `sk_*`) so Scalar matches LHDN quality.
7. Delete dead `packages/api-types-dotnet/Generated/Models.cs`; remove from CI diff list; update ADR 005.
8. Remove orphan models: `LinkedCheckoutDto`, `PaymentRecordDto`, CRM models from main emit.

### P2 — Hygiene & docs

9. Rewrite `packages/api-spec/README.md` to current modules (drop auth/community; document docs-*.tsp + payments + commerce split).
10. Update ADR 007 examples (Commerce/Payments, not Community/Vault; paths `lazuar-developers`).
11. Annotate or rewrite `docs/001-gaps/13-typespec-api-contracts.md` as historical, or replace with pointer to this maintenance plan.
12. Expand Taskfile `generates` to include LHDN SDK outputs and product OpenAPI paths if Task caching is used operationally.
13. Consider money type policy (decimal as string vs double) for payments + commerce amounts — document in `docs/api-versioning.md` if left as number for v1.

### P2 — Automated honesty

14. **Contract test:** enumerate Minimal API endpoints (reflection or source generators) vs OpenAPI paths; allowlist internal routes from honesty doc. Fail CI on unexpected drift.
15. Optional: spectral/lint OpenAPI for operationIds, security, 4xx responses.
16. Optional: per-product TS type packages for external integrators (payments, commerce-public) — only after public surfaces stabilize.

### P3 — Product strategy

17. Publish Payments SDK (Kiota) only after P0 path/auth/idempotency cleanups.
18. Public-only Commerce OpenAPI for integrators vs full admin OpenAPI for console.
19. Outbound webhook payload models for commerce lifecycle events (align with Developers hub event catalog).
20. Cookie/session security scheme documentation for browser apps (or explicit note that Scalar uses Bearer simulation only).

---

## 12. File-by-file notes (TypeSpec sources)

### Orchestration

| File | Assessment |
|---|---|
| `main.tsp` | Clean import-only orchestrator. Includes CRM + empty messaging. Service title “Lazuar Platform API” + version 1.0.0. **Do not add models here.** |
| `tspconfig.yaml` | Minimal; adequate. |
| `package.json` | Build complete for all products; brittle long command. Watch script only compiles `main.tsp` (docs not watched). |
| `README.md` | **Stale** structure and Community examples; golden rules still valid. |
| `docs-one.tsp` | Clean One-only. |
| `docs-ops.tsp` | Clean Ops-only; marked Internal in hub. |
| `docs-billing.tsp` | Clean. |
| `docs-lhdn.tsp` | Best external entry; dual auth; imports One models for keys. |
| `docs-commerce.tsp` | Strong product description; includes communications admin. |
| `docs-payments.tsp` | Strong product description; missing security decorator. |

### Common

| File | Assessment |
|---|---|
| `common/models.tsp` | Solid core. Orphan `LinkedCheckoutDto`. Error status set incomplete vs real API. |

### Modules

| File | Assessment |
|---|---|
| `one/models.tsp` | Large, well-commented (webhooks, provision, API keys). Good. |
| `one/routes.tsp` | Broad and aligned; provision auth is prose-only. |
| `ops/models.tsp` | Stream chunk + UI request models ready. |
| `ops/routes.tsp` | Stream + system-message present — good. |
| `billing/models.tsp` | Includes NetProfitDto (used). |
| `billing/routes.tsp` | Missing public signed document. |
| `lhdn/models.tsp` | Strong enums (IdType, TaxType, DocumentType, StateCode). Alias pattern for API keys. |
| `lhdn/routes.tsp` | Correct `/lhdn` prefix; Idempotency-Key on submit. |
| `commerce/models.tsp` | Large; unused `PaymentRecordDto`; dual-DTO risk on subscriber ops. |
| `commerce/admin-routes.tsp` | Complete admin surface including portal-link + export. |
| `commerce/public-routes.tsp` | Integrator-focused; cancel present; no phantom magic-link. |
| `communications/models.tsp` | Targeting fields need mapping or removal. |
| `communications/admin-routes.tsp` | Incomplete vs broadcast status/preview. |
| `payments/models.tsp` | Good cashier + webhook payload docs. float64 money. |
| `payments/routes.tsp` | Trailing slash; no @useAuth; no Idempotency header. |
| `platform/routes.tsp` | Small, correct. |
| `crm/models.tsp` | Premature; no routes. |
| `messaging/models.tsp` | Empty; stale Community comment. |

### Generated / clients (hygiene)

| Artifact | Assessment |
|---|---|
| `dist/openapi.yaml` | Full platform; version 1.0.0; includes CRM orphans + trailing slash checkout. |
| `dist/*/openapi.yaml` | Product purity OK (commerce+comms). |
| `api-types-ts/src/index.ts` | Live; CI-gated; monolith. |
| `api-types-dotnet/Lazuar.ApiContracts.cs` | Live (~6.5k lines); snake_underscore property names. |
| `api-types-dotnet/Generated/Models.cs` | **Dead stale.** |
| `api-types-dotnet/nswag.json` | Correct output path; DTO-only. |
| `lhdn-sdk-*/**` | Path-correct Kiota clients. |

### Implementation endpoints (reference for drift)

| File | Assessment |
|---|---|
| `Modules/One/Infrastructure/Endpoints.cs` | Rich; aligns with TypeSpec. |
| `Modules/Ops/Infrastructure/Endpoints.cs` | Aligns including stream. |
| `Modules/Billing/Infrastructure/Endpoints.cs` | Extra signed document route. |
| `Modules/Lhdn/Infrastructure/Endpoints.cs` | Aligns; scoped auth groups. |
| `Modules/Commerce/Infrastructure/Endpoints*.cs` | Local DTOs on subscriber ops. |
| `Modules/Communications/Infrastructure/Endpoints*.cs` | Extra routes; targeting drop. |
| `Modules/Payments/Infrastructure/IntegrationEndpoints.cs` | Local DTOs; decimal amounts. |
| `Modules/Payments/Infrastructure/Endpoints.cs` | Inbound webhooks only. |
| `Modules/Messaging/Infrastructure/Endpoints.cs` | Command binding; allowlist. |

---

## 13. Drift heat map (current)

```text
                    Spec completeness   Impl completeness   Product docs purity
One                 High                High                Clean
Ops                 High                High                Clean (Internal)
Billing             High (-signed doc)  High                Clean
LHDN                High                High                Clean (+ dual auth)
Commerce            High                High (dual DTOs)    Clean (+ comms bundle)
Communications      Med                 High                Via commerce docs
Payments M2M        Med (auth/idem)     High (dual DTOs)    Clean prose / weak security block
Platform            High                High                Not productized
Messaging           Empty               Present (internal)  N/A allowlist
Inbound pay webhooks Empty              Present             Allowlist
CRM                 Models only         No HTTP             Noise in monolith
```

---

## 14. Concrete residual examples (quick reference)

1. **CreateManualSubscriber:** TypeSpec DTO vs local `CreateManualSubscriberRequest`.
2. **Portal-link / record-payment:** TypeSpec DTOs vs local records.
3. **Integration checkout:** TypeSpec DTOs vs local classes; amount float64 vs decimal.
4. **OpenAPI path** `/integrations/payments/checkouts/` trailing slash.
5. **Broadcast targeting fields** advertised, not mapped in endpoint.
6. **Broadcast preview/status** impl-only; DTOs outside TypeSpec.
7. **Billing signed final PDF** impl-only.
8. **Communications public compliance** impl-only.
9. **Messaging notify** MediatR command as body.
10. **CRM / LinkedCheckout / PaymentRecord** schemas without routes.
11. **Payments product OpenAPI** has no security schemes.
12. **Payments create** Idempotency-Key not in parameters schema.
13. **Dead `Generated/Models.cs`** still tracked; ADR 005 outdated.
14. **api-spec README** documents non-existent Community/auth layout.
15. **No CI path honesty test** — only client regeneration gate.
16. **Error responses** often string BadRequest vs ProblemDetails in practice.
17. **Cookie session auth** not modeled in OpenAPI for browser apps.
18. **docs-commerce** exposes full admin surface to integrator-facing Scalar (DX risk).

---

## 15. Suggested maintenance backlog (actionable checklist)

Use this as a sequenced maintenance track under plan `004-maintenance` (no implementation performed by this analysis).

### Wave A — SSoT repair (1–2 days)

- [ ] Replace local Commerce subscriber DTOs with `Lazuar.ApiTypes` generated types.
- [ ] Replace Payments integration local DTOs with generated types (decide decimal/double).
- [ ] Fix payments TypeSpec route trailing slash; regenerate clients.
- [ ] Add `@header("Idempotency-Key")` optional/required policy on payments create.
- [ ] Map or delete broadcast targeting fields.
- [ ] Delete `Generated/Models.cs`; fix CI paths; patch ADR 005.

### Wave B — Spec completeness (2–3 days)

- [ ] TypeSpec: billing signed document route.
- [ ] TypeSpec: communications broadcast preview/status + models.
- [ ] docs-payments: `@useAuth(BearerAuth)` (+ document sk_ scopes in description).
- [ ] Remove orphan CRM/LinkedCheckout/PaymentRecord from emit.
- [ ] Refresh `packages/api-spec/README.md`.

### Wave C — Honesty automation (2–4 days)

- [ ] Minimal API vs OpenAPI path inventory test with allowlist from `docs/contracts/openapi-vs-minimal-api.md`.
- [ ] Optionally fail on OpenAPI paths with no matching endpoint and vice versa (except allowlist).
- [ ] Document cookie vs Bearer for browser clients in honesty doc + TypeSpec notes.

### Wave D — Product DX (optional)

- [ ] Public-only Commerce OpenAPI for integrators.
- [ ] Payments Kiota SDK package (after Wave A).
- [ ] Outbound webhook payload models for commerce lifecycle events.

---

## 16. Conclusion

The TypeSpec system in lazuar-pay is **architecturally sound and substantially healthier than the 001-gaps snapshot**. Product-scoped docs, LHDN path correctness, portal cancel, ops streaming contracts, payments M2M surface, and CI client regeneration form a credible contract-first spine.

Remaining work is **maintenance quality**, not greenfield design:

1. Stop dual-maintaining edge DTOs in C#.
2. Close residual impl-only / shape gaps on admin UX routes.
3. Delete orphans and dead generated artifacts.
4. Teach CI to prove **path honesty**, not only client regeneration.
5. Keep product docs pure and security metadata complete for every external surface.

Until Wave A lands, treat TypeSpec as **mostly SSoT with known dual-write exceptions** on Commerce subscribers and Payments integration checkouts — document those exceptions in PR review checklists if implementation is deferred.

---

*End of uncondensed report.*
