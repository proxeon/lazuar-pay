<!-- Source subagent: 019fc650-3513-7032-806d-65d39e3bfa86 -->
<!-- Full uncondensed subagent analysis — do not summarize -->

# API Contracts & TypeSpec Gap Analysis

**Scope:** `/Users/akmalfirdaus/Code/lazuar/lazuar-hub`  
**Sources:** `packages/api-spec/**`, `packages/api-types-*`, `packages/lhdn-sdk-*`, ADRs 005/006/007/011, `Modules/*/Infrastructure/Endpoints*`, `apps/developers-page`, `task gen` pipeline.

---

## Spec Inventory

### Entry points & emit config

| Artifact | Path | Role |
|---|---|---|
| Monolith orchestrator | `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/packages/api-spec/main.tsp` | Imports all modules; emits full OpenAPI for internal clients |
| Product docs entry | `docs-one.tsp`, `docs-ops.tsp`, `docs-billing.tsp`, `docs-lhdn.tsp` | Product-scoped OpenAPI for Scalar |
| Orphan docs entry | `docs-commerce.tsp` | **Exists but not in build script** |
| Config | `tspconfig.yaml` | Emits `@typespec/openapi3` → `openapi.yaml` |
| Build | `packages/api-spec/package.json` `build` | Compiles main + one/ops/billing/lhdn only |

**Build command reality:**
```text
tsp compile main.tsp → dist/openapi.yaml
tsp compile docs-one.tsp → dist/one/openapi.yaml
tsp compile docs-ops.tsp → dist/ops/openapi.yaml
tsp compile docs-billing.tsp → dist/billing/openapi.yaml
tsp compile docs-lhdn.tsp → dist/lhdn/openapi.yaml
# docs-commerce.tsp: NOT compiled
```

### TypeSpec source tree

```text
packages/api-spec/
├── common/models.tsp
├── main.tsp
├── docs-{one,ops,billing,lhdn,commerce}.tsp
└── modules/
    ├── one/{models,routes}.tsp
    ├── ops/{models,routes}.tsp
    ├── billing/{models,routes}.tsp
    ├── lhdn/{models,routes}.tsp
    ├── commerce/{models,admin-routes,public-routes}.tsp
    ├── communications/{models,admin-routes}.tsp
    ├── platform/routes.tsp
    ├── crm/models.tsp                    # models only, no routes
    └── messaging/models.tsp              # intentionally blank
```

### Generated OpenAPI (committed in workspace)

| File | Title | Paths approx |
|---|---|---|
| `dist/openapi.yaml` | Lazuar Platform API | Full monolith (~all modules) |
| `dist/one/openapi.yaml` | Lazuar Platform API (Core) | One **+ Billing** (leak) |
| `dist/ops/openapi.yaml` | Lazuar Ops API | Ops **+ Billing** (leak) |
| `dist/billing/openapi.yaml` | Lazuar Billing API | Billing only |
| `dist/lhdn/openapi.yaml` | Lazuar LHDN API | LHDN only |
| `dist/commerce/` | — | **Missing** |

All OpenAPI `info.version` values are **`0.0.0`**.

### Downstream packages

| Package | Generator | Input | Output |
|---|---|---|---|
| `@repo/api-types-ts` | openapi-typescript | `dist/openapi.yaml` | `src/index.ts` |
| `@repo/api-types-dotnet` | NSwag | `dist/openapi.yaml` | `Lazuar.ApiContracts.cs` (compiled); `Generated/Models.cs` is **stale sibling** |
| `@lazuar/lhdn-sdk` (TS) | Kiota | `dist/lhdn/openapi.yaml` | `src/generated/**` |
| `Lazuar.Lhdn.Sdk` (.NET) | Kiota | `dist/lhdn/openapi.yaml` | `src/Generated/**` |

### Pipeline (`Taskfile.yml`)

`task gen` → `gen:spec` → `gen:types-ts` → `gen:types-dotnet` → `gen:sdk-lhdn`.

ADR 005 claims `dist/` is gitignored/transient; **`dist/**/openapi.yaml` is present and used by developers-page**.

---

## Product-Scoped Docs vs Monolith Reality

### Intended model (ADR 007)

- `main.tsp` → internal DTO/client generation only  
- `docs-*.tsp` → product-scoped Scalar docs  
- Developers hub routes per product  

### Actual model

| Product | docs-*.tsp | package.json build | dist artifact | developers-page route | Landing card |
|---|---|---|---|---|---|
| One | ✅ | ✅ | ✅ | `/one` | ✅ |
| Ops | ✅ | ✅ | ✅ | `/ops` | ✅ |
| Billing | ✅ | ✅ | ✅ | `/billing` | ✅ |
| LHDN | ✅ | ✅ | ✅ | `/lhdn` | ✅ |
| Commerce | ✅ file | ❌ not built | ❌ | ❌ | ❌ |
| Communications | (bundled into commerce docs only) | — | — | ❌ | ❌ |
| Platform (superadmin) | only via `main.tsp` | — | in monolith only | ❌ | ❌ |
| Messaging | no routes | — | — | ❌ | ❌ |
| Payments webhooks | no TypeSpec | — | — | ❌ | ❌ |
| CRM | models only | — | — | ❌ | ❌ |

### Product scope contamination (high severity DX issue)

`docs-one.tsp` and `docs-ops.tsp` both import billing models **and** routes:

```7:8:/Users/akmalfirdaus/Code/lazuar/lazuar-hub/packages/api-spec/docs-one.tsp
import "./modules/billing/models.tsp";
import "./modules/billing/routes.tsp";
```

Result:
- `dist/one/openapi.yaml` includes `/admin/billing/*` and `/public/billing/*`
- `dist/ops/openapi.yaml` includes the same billing surface  

So product-scoped docs are **not** product-scoped. ADR 007’s audience-isolation goal is partially inverted.

### Docs vs ADR examples are stale

ADR 007 still references Community/Vault, `docs-community.tsp`, and old module names.  
README under `packages/api-spec` still documents `auth/`, `community/`, nested structure that no longer exists.

### Developers hub is partial

`/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/developers-page/app/page.tsx` exposes only One, Ops, Billing, LHDN. Commerce (the public checkout/subscription integration surface) is the **most external-facing** product and is absent from the hub despite `docs-commerce.tsp` existing.

---

## External vs Internal Contract Separation Status

### ADR 006 status: **mostly followed, with important leaks**

| Boundary | Policy | Reality |
|---|---|---|
| TypeSpec DTOs as HTTP edge only | Required | **Mostly true** for Commerce/One/Billing/Lhdn/Ops/Communications admin |
| Internal MediatR contracts separate | Required | **True** — module `Contracts/` hold Commands/Events |
| Endpoints as anti-corruption layer | Required | **True** for most mapped endpoints |
| No domain leaks into TypeSpec | Required | **Mostly true** |
| No TypeSpec for integration events | Required | **True** |

### Contraventions / gray areas

1. **Local request DTOs outside TypeSpec (implementation-only contracts)**  
   - `CreateManualSubscriberRequest` in `SubscriberEndpoints.cs` instead of generated `CreateManualSubscriberDto`  
   - `GenerateCustomerPortalRequest` / `GenerateCustomerPortalResponse` — entirely ad-hoc, not in TypeSpec  

2. **Internal contracts used as HTTP payloads**  
   - Messaging: `MapPost("/notify", … SendTenantNotificationCommand command …)` binds a **MediatR command** directly as the body.  
   - Ops execute-action: deserializes `ProposedActionDto.Command_payload` into **internal command types** at the edge.

3. **Module-local response DTOs not in TypeSpec**  
   - `BroadcastStatusDto`, `BroadcastCostPreviewDto` live in `Modules.Communications.Contracts` with hand-rolled `[JsonPropertyName]`, not in TypeSpec.

4. **CRM models in TypeSpec with zero HTTP surface**  
   - `ClientProfileDto` etc. exist only as external models; CRM has no endpoints. These are effectively unused edge contracts (or premature).

5. **Query services return `Lazuar.ApiTypes` DTOs**  
   - e.g. `IOneQueryService`, `ICommunicationsQueryService` return generated DTOs — acceptable ACL pattern if mapping stays at infra, but it means generated edge types are used deeper than pure Endpoints.cs.

### Separation that works well

- Commerce admin product/coupon/dunning flows: TypeSpec DTO → Command mapping.  
- LHDN documents/webhooks: TypeSpec DTO → application commands.  
- One auth/workspace: TypeSpec DTO → commands/DB mapping.  
- Integration events remain backend-only (good).

---

## Generated Client/Types Alignment

### Internal monorepo clients (`api-types-ts` / `api-types-dotnet`)

**Consumers of `@repo/api-types-ts`:**
- `apps/ops-page`
- `apps/superadmin-page`
- `apps/portal-page` (checkout + community portal)

**Not consumed by:** messaging UI (n/a), developers-page (reads YAML directly).

**Generation quality notes:**
- NSwag produces awkward C# names: `Amount_myr`, `Is_email_verified` (snake → Pascal with underscore). Works with STJ `[JsonPropertyName("…")]` + ASP.NET `SnakeCaseLower` policy.
- `Lazuar.ApiContracts.csproj` compiles **only** `Lazuar.ApiContracts.cs`.  
  `Generated/Models.cs` is a **dead/stale duplicate** (ADR 005 still mentions `Generated/Models.cs` as the output).
- TS types are path-based (`paths` + `components`) — good for openapi-fetch.

### External LHDN SDKs

| Issue | Detail |
|---|---|
| **Double `/api/v1` path** | TypeSpec `@route("/api/v1/lhdn")` + server `…/api/v1` → Kiota paths like `{+baseurl}/api/v1/lhdn/...`. If `baseUrl` is `https://api…/api/v1`, clients call **`/api/v1/api/v1/lhdn/...`**. Backend maps `/api/v1` + `/lhdn` only. |
| Spec vs runtime auth | Docs declare ApiKey + Bearer; middleware expects `Authorization: Bearer sk_live_|sk_test_…`. Raw ApiKey-without-Bearer may fail. |
| Spec vs published keys | Generate returns `plain_key`; not modeled whether key includes `Bearer ` prefix. |
| Missing list keys in impl | Spec/SDK: `GET /api-keys`; backend: only POST + DELETE. |
| Missing validate endpoint in impl | Spec/SDK: `POST …/taxpayer/validate`; backend has service/gateway only — **no HTTP endpoint**. |
| Version | SDK packages at `0.1.0`; OpenAPI version `0.0.0`. |

### Frontend typed calls vs missing backend

Portal UI calls typed paths that **do not exist** on the API:

```36:39:/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/portal-page/src/modules/community/components/CommunityPortalView.tsx
      const { error: apiError } = await browserClient.POST("/public/commerce/{tenantSlug}/portal/cancel", {
        params: { path: { tenantSlug }, query: { token } },
        body: { subscription_id: sub.id }
      });
```

TypeSpec + generated TS advertise these routes; `PublicEndpoints.cs` does **not** implement them. This is live drift causing production 404s for cancel (and likely magic-link / billing-link if called).

---

## Drift Between Spec and Implementation

Legend: **Spec-only** = in TypeSpec, not implemented · **Impl-only** = implemented, not in TypeSpec · **Shape drift** = both exist but differ.

### Cross-cutting path prefix

| Layer | Base |
|---|---|
| ASP.NET | `MapGroup("/api/v1")` then module groups |
| TypeSpec server | `http://localhost:8080/api/v1` |
| Most module routes | relative (`/one`, `/admin/commerce`, …) ✅ |
| **LHDN routes** | absolute `@route("/api/v1/lhdn")` ❌ double prefix in OpenAPI |

---

### One (`/one`)

| Spec | Implementation | Status |
|---|---|---|
| Auth, workspaces, invites, apps, entitlements, webhooks, storage | Present in `One/Infrastructure/Endpoints.cs` | ✅ largely aligned |
| `GET/PUT /one/workspaces/{id}/lhdn-config` | **No endpoint** | **Spec-only** |
| Workspace list `GET /workspaces` auth: Bearer | Impl requires **OrgAdmin + system admin** | Auth drift |
| Apps toggle similarly super-admin gated | Spec only says Bearer | Auth drift |

---

### Billing (`/admin/billing`, `/public/billing`)

| Spec | Implementation | Status |
|---|---|---|
| Ledger, document URL, summary, credits, packages, top-up, profile | Present | ✅ |
| `GET /admin/billing/net-profit` | **No endpoint / no service** | **Spec-only** |
| `GET /summary` with `from_date`/`to_date` | Impl: **no date query params**; service is unfiltered | **Shape drift** |
| Public draft PDF | Present | ✅ |
| Public signed document `GET /{tenantSlug}/documents/{ledgerEntryId}?sig&exp` | Present | **Impl-only** |

---

### LHDN (`/lhdn` runtime)

| Spec path (as emitted) | Runtime path | Status |
|---|---|---|
| `/api/v1/lhdn/documents` (+ Idempotency-Key) | `/api/v1/lhdn/documents` | Route OK if client strips extra prefix; **SDK broken** if base includes `/api/v1` |
| `POST …/taxpayer/validate` | **Missing** | **Spec-only** (service exists internally) |
| `GET …/api-keys` | **Missing** | **Spec-only** |
| `POST/DELETE api-keys`, documents, cancel, webhooks, certificate | Present | ✅ |
| Auth: OrgAdmin / API key middleware | Spec Bearer; docs-lhdn also ApiKey | Partial |

---

### Commerce admin (`/admin/commerce`)

| Spec | Implementation | Status |
|---|---|---|
| Products, dunning, payment-config, subscribers, transactions, coupons, stats, custom-checkouts, mark-paid | Present | ✅ paths |
| `POST /subscribers` body `CreateManualSubscriberDto` | Uses local `CreateManualSubscriberRequest` | **DTO source drift** (shape similar) |
| — | `POST /subscribers/portal-link` + local request/response | **Impl-only** |

---

### Commerce public (`/public/commerce`) — critical

| Spec | Implementation | Status |
|---|---|---|
| `GET …/products/{slug}` | Yes | ✅ |
| `GET …/validate-coupon` | Yes | ✅ |
| `POST /checkout` | Yes | ✅ |
| `GET …/portal` | Yes | ✅ |
| `POST …/portal/magic-link` | **No** | **Spec-only** (portal may depend) |
| `POST …/portal/cancel` | **No** | **Spec-only** (portal **actively calls**) |
| `GET …/portal/billing-link` | **No** | **Spec-only** |
| `GET /checkout/{subId}/status` | Yes | ✅ |
| `GET …/custom-checkouts/{sessionId}` | Yes | ✅ |
| `GET /checkout/{subId}/arrears` | Yes | ✅ |
| `POST /checkout/{subId}/update-payment` | Yes | ✅ |

---

### Communications admin (`/admin/communications`)

| Spec | Implementation | Status |
|---|---|---|
| Templates CRUD-ish, variables, preview, reminders/test | Yes | ✅ |
| `POST /broadcasts` | Yes | ✅ path |
| Create broadcast fields `target_plan_id`, `target_status`, `target_is_reminder_only` | Endpoint **does not map** them into `SendBroadcastCommand` (command supports them) | **Shape drift / dead fields** |
| — | `GET /broadcasts/preview` | **Impl-only** |
| — | `GET /broadcasts/{id}` | **Impl-only** |
| — | `DELETE /templates/legacy-cleanup` | **Impl-only** (ops utility) |
| — | Public `GET /public/communications/unsubscribe` | **Impl-only** |
| — | Public `POST /public/communications/webhooks/resend` | **Impl-only** |

Broadcast response DTOs are module-local, not TypeSpec.

---

### Ops (`/ops`)

| Spec | Implementation | Status |
|---|---|---|
| conversations list/messages, chat, execute-action, rename, delete, resolve | Yes | ✅ |
| — | `POST /chat/stream` (SSE) | **Impl-only** (ops-page uses it) |
| — | `POST /chat/conversations/{id}/system-message` | **Impl-only** (ops-page uses it) |
| `ChatStreamChunkDto` model | Used by stream impl, **no route op** in TypeSpec | Model-only |
| Auth: Bearer | Impl: roles **CLIENT, ADMIN** | Stricter than spec |

---

### Platform (`/platform`)

| Spec | Implementation | Status |
|---|---|---|
| login/logout/me, payment-config | `Payments/Infrastructure/PlatformEndpoints.cs` under `/api/v1/platform` | ✅ |
| Not product-doc’d | Superadmin only | Docs gap |

---

### Messaging

| Spec | Implementation | Status |
|---|---|---|
| Empty models, no routes | `POST /messaging/notify` binds **internal command** | **Impl-only / wrong boundary** |

---

### Payments

| Spec | Implementation | Status |
|---|---|---|
| None | `POST /webhooks/payments/{gatewayType}/{tenantId}` | **Impl-only** (external gateway facing) |

---

### CRM

| Spec | Implementation | Status |
|---|---|---|
| Models only | **No HTTP endpoints** (command handlers only) | Models without surface |

---

### Models present in TypeSpec with little/no use

- `Core.LinkedCheckoutDto` — generated, no route references found in API  
- `Commerce.PaymentRecordDto` — unused in backend grep  
- `Ops.ChatStreamChunkDto` — model without operation  
- Full CRM model set — no routes  

---

### Error contract drift

- Spec advertises `ProblemDetails` / `ProblemDetailsResponse` for nearly everything.  
- Many endpoints return `BadRequest<string>`, raw strings, or `Results.BadRequest(ex.Message)`.  
- LHDN maps “402 insufficient credits” into **HTTP 400** with ProblemDetails `Status = 402` (status mismatch).  
- API key middleware returns `{ error: "…" }` not RFC7807.

---

## Missing Integration-Facing Contract Surfaces

These are external or partner-facing and either unspec’d or broken in the public contract:

1. **LHDN public integration surface (highest priority)**  
   - Path prefix bug in OpenAPI/SDK  
   - `taxpayer/validate` documented but not exposed  
   - `GET api-keys` documented but not exposed  
   - Webhook **outbound** payload schemas not in TypeSpec (only registration APIs)

2. **Commerce public portal lifecycle**  
   - magic-link, cancel, billing-link in contract + frontend, missing backend

3. **Payment provider webhooks**  
   - Live at `/webhooks/payments/{gateway}/{tenantId}` — no OpenAPI, no versioning, no signed-payload schema

4. **Communications compliance webhooks**  
   - Resend Svix webhook + unsubscribe link — operationally critical, not in TypeSpec (may be intentional “not public API”, but then should be labeled internal)

5. **Commerce product docs + SDK**  
   - `docs-commerce.tsp` incomplete pipeline; no Kiota/commerce SDK; checkout integrators only have monolith TS types

6. **Idempotency contract**  
   - Only LHDN document submit documents `Idempotency-Key`; billing top-up / checkout / broadcast lack formal idempotency headers in TypeSpec

7. **SSE streaming contract**  
   - Ops chat stream is primary UX path; not in OpenAPI (SSE is poorly modeled by OpenAPI3 — needs explicit docs extension)

8. **Messaging notify**  
   - If any external caller exists, it is unsafe (command binding); if internal-only, should not be a public route without auth documentation

---

## Versioning Strategy Gaps

| Concern | Current state | Gap |
|---|---|---|
| URL version | Hard-coded `/api/v1` in host + some LHDN route | No TypeSpec `@versioned` / path template strategy |
| OpenAPI `info.version` | Always `0.0.0` | No release mapping; hub shows cosmetic `v1` |
| Package versions | api-spec `1.0.0`, LHDN SDKs `0.1.0` | Unrelated to OpenAPI version |
| Breaking change process | None encoded | No deprecation annotations, no sunset headers |
| Product vs monolith versioning | Independent docs, single runtime host | Cannot version One without versioning all under same host |
| Dist artifact policy | ADR: transient; repo: present | CI/deploy may depend on committed YAML; drift risk if `task gen` not run |
| Task sources filter | `packages/api-spec/*.tsp` only | Changes under `modules/**` may not invalidate Task cache correctly |
| Auth evolution | Cookie JWT (`lazuar_auth` / `lazuar_admin_auth`) vs Bearer in OpenAPI | Spec implies Bearer tokens; browsers use cookies — not modeled |
| Compatibility tests | No contract tests found tying OpenAPI paths to Minimal API map | Drift accumulates silently |

---

## Recommendations

### P0 — Fix user-visible breakage

1. **Implement or remove portal public routes**  
   - Implement `portal/magic-link`, `portal/cancel`, `portal/billing-link` **or** remove from TypeSpec + fix portal UI.  
   - Prefer implement: frontend already depends on cancel.

2. **Fix LHDN route prefix**  
   - Change TypeSpec to `@route("/lhdn")` (relative to server `/api/v1`).  
   - Regenerate OpenAPI + both SDKs; publish patch release.  
   - Add smoke test: Kiota path == runtime path.

3. **Wire or delete LHDN validate + list API keys**  
   - Either expose endpoints matching SDK or strip from TypeSpec before next publish.

### P1 — Restore ADR 007 product purity

4. Remove billing imports from `docs-one.tsp` and `docs-ops.tsp`.  
5. Finish Commerce productization: include `docs-commerce.tsp` in build, add `dist/commerce`, `app/commerce/route.ts`, landing card.  
6. Decide whether Communications ships inside Commerce docs or gets `docs-communications.tsp`.

### P1 — Contract completeness for implemented APIs

7. Add TypeSpec for:
   - Ops stream + system-message  
   - Communications broadcast status/preview  
   - Billing public signed document  
   - Commerce admin portal-link  
   - Payments webhook (even as “partner-facing, no auth in browser”)  

8. Use generated `CreateManualSubscriberDto` instead of local record.

9. Map broadcast targeting fields or remove them from the DTO.

### P2 — Separation hygiene (ADR 006)

10. Stop binding MediatR commands on HTTP for Messaging; introduce a TypeSpec request DTO or mark route internal/admin-only with auth.  
11. Keep `Broadcast*Dto` either fully in TypeSpec or fully internal — not half.  
12. Drop unused CRM/LinkedCheckout/PaymentRecord models from public OpenAPI or mark `@internal` if TypeSpec supports exclusion from docs emit.

### P2 — Versioning & quality gates

13. Set meaningful `info.version` (semver) per product docs entry.  
14. Expand Taskfile sources to `packages/api-spec/**/*.tsp`.  
15. CI step: `task gen` + `git diff --exit-code` on generated clients.  
16. Contract test: enumerate Minimal API endpoints vs OpenAPI paths (allowlist for intentionally internal routes).  
17. Document cookie-session auth as alternate security scheme in TypeSpec for browser apps.  
18. Resolve dual NSwag outputs: delete or stop generating `Generated/Models.cs`; update ADR 005.  
19. Align README + ADR 007 examples with current modules (Commerce, not Community/Vault).

### P3 — Product strategy

20. Publish Commerce integration SDK only after public routes stabilize.  
21. Treat LHDN as the gold standard external surface; bring path/auth/error parity to that bar for Commerce public checkout.

---

## File-by-File Notes

### Orchestration & config

| File | Notes |
|---|---|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/packages/api-spec/main.tsp` | Full monolith import graph; correct as internal SSoT. Includes platform + communications + commerce. |
| `tspconfig.yaml` | Minimal; no multi-file versioning, no openapi version override. |
| `package.json` | Missing `docs-commerce` compile; version `1.0.0` unrelated to API. |
| `README.md` | Stale (auth/community layout); golden rules still useful. |
| `docs-one.tsp` | **Billing leakage**; good title/description otherwise. |
| `docs-ops.tsp` | **Billing leakage**. |
| `docs-billing.tsp` | Clean, scoped correctly. |
| `docs-lhdn.tsp` | Best external posture (prod server + dual auth); undermined by route prefix. |
| `docs-commerce.tsp` | Correct imports (commerce + communications); **not wired**. |
| `common/models.tsp` | Solid core; `LinkedCheckoutDto` appears orphaned. |

### Module TypeSpec

| File | Notes |
|---|---|
| `modules/one/routes.tsp` | Broad, mostly matches impl; **lhdn-config** phantom; auth annotations weaker than real policies. |
| `modules/one/models.tsp` | Includes `WorkspaceLhdnConfigDto` for phantom routes. |
| `modules/billing/routes.tsp` | **net-profit** phantom; summary query params over-specified; missing public signed document route. |
| `modules/billing/models.tsp` | `NetProfitDto` unused by runtime. |
| `modules/lhdn/routes.tsp` | **Critical:** `@route("/api/v1/lhdn")`; validate + list keys missing in API. |
| `modules/lhdn/models.tsp` | Strong enums (IdType, TaxType, DocumentType) — good for SDK. |
| `modules/ops/routes.tsp` | Missing stream + system-message; model `ChatStreamChunkDto` unused by ops. |
| `modules/ops/models.tsp` | Stream chunk DTO ready but not operated. |
| `modules/commerce/admin-routes.tsp` | Generally complete; missing portal-link. |
| `modules/commerce/public-routes.tsp` | **Three portal lifecycle ops without backend**. |
| `modules/commerce/models.tsp` | Large; some models unused (`PaymentRecordDto`). |
| `modules/communications/admin-routes.tsp` | Incomplete vs broadcast status/preview; no public compliance routes. |
| `modules/communications/models.tsp` | Targeting fields on broadcast not honored by endpoint mapping. |
| `modules/platform/routes.tsp` | Matches platform endpoints; not product-doc’d. |
| `modules/crm/models.tsp` | Spec-only models; no routes. |
| `modules/messaging/models.tsp` | Empty; honest comment. |

### Generated / clients

| File | Notes |
|---|---|
| `dist/openapi.yaml` | Monolith SSoT for NSwag + openapi-typescript; version 0.0.0; LHDN paths double-prefixed. |
| `dist/one|ops/openapi.yaml` | Contaminated with billing. |
| `dist/lhdn/openapi.yaml` | Feeds Kiota; path + server combo is wrong for real base URLs. |
| `packages/api-types-ts/src/index.ts` | Tracks monolith; includes phantom portal + LHDN paths. |
| `packages/api-types-dotnet/Lazuar.ApiContracts.cs` | Live compile input; underscore property names. |
| `packages/api-types-dotnet/Generated/Models.cs` | Dead; ADR/docs still mention it. |
| `packages/api-types-dotnet/nswag.json` | Generates DTOs only (good); CamelCase property names from snake JSON. |
| `packages/lhdn-sdk-ts/src/index.ts` | ApiKey provider on `Authorization` header; may not match `Bearer sk_*` middleware without careful key formatting. |
| `packages/lhdn-sdk-*/**/taxpayer/validate/**` | Generated for non-existent endpoint. |

### Implementation endpoints

| File | Notes |
|---|---|
| `Modules/One/Infrastructure/Endpoints.cs` | Rich; no lhdn-config; cookie auth not in OpenAPI. |
| `Modules/Billing/Infrastructure/Endpoints.cs` | Extra public signed document; no net-profit; summary ignores dates. |
| `Modules/Lhdn/Infrastructure/Endpoints.cs` | No validate, no list keys; path group `/lhdn` correct under `/api/v1`. |
| `Modules/Commerce/Infrastructure/Endpoints.cs` | Aggregates admin + public groups. |
| `…/Endpoints/PublicEndpoints.cs` | Missing three portal ops present in TypeSpec/frontend. |
| `…/Endpoints/SubscriberEndpoints.cs` | Local DTOs; portal-link impl-only. |
| `Modules/Communications/Infrastructure/Endpoints*.cs` | Extra public compliance + broadcast status/preview; targeting fields dropped. |
| `Modules/Ops/Infrastructure/Endpoints.cs` | Stream + system-message impl-only. |
| `Modules/Messaging/Infrastructure/Endpoints.cs` | Command-as-body anti-pattern. |
| `Modules/Payments/Infrastructure/Endpoints.cs` | Unspec’d webhook. |
| `Modules/Payments/Infrastructure/PlatformEndpoints.cs` | Matches platform TypeSpec. |
| `Lazuar.Api/Program.cs` | Single `/api/v1` host; platform isolated group + SUPER_ADMIN. |

### ADRs & docs apps

| File | Notes |
|---|---|
| `docs/.../005-typespec-api-contract-generation.md` | Pipeline correct; paths for Models.cs / dist-ignore outdated. |
| `docs/.../006-separation-of-external-and-internal-contracts.md` | Directionally correct; Messaging/local DTOs violate spirit. |
| `docs/.../007-product-scoped-api-references.md` | Intent right; examples Community/Vault obsolete; implementation incomplete + contaminated. |
| `docs/.../011-sdk-publishing-runbook.md` | Assumes `task gen` correctness; doesn’t warn about path double-prefix. |
| `apps/developers-page/**` | Solid loader pattern; missing commerce; trusts dist YAML. |

---

## Drift heat map (summary)

```text
                    Spec completeness   Impl completeness   Product docs purity
One                 High                High (-lhdn-config) Contaminated (+billing)
Ops                 Med (-stream)       High                Contaminated (+billing)
Billing             Med (+net-profit)   High (+signed doc)  Clean
LHDN                High (+extras)      Med (-validate/list)Clean docs / BROKEN paths
Commerce            High (+portal ops)  Med (-portal ops)   docs file orphaned
Communications      Med                 High (+public ops)  Only via commerce docs
Platform            High                High                Not productized
Messaging           Empty               Present (internal)  N/A
Payments webhooks   Empty               Present             N/A
CRM                 Models only         No HTTP             N/A
```

---

## Highest-impact concrete examples (quick reference)

1. **Portal cancel:** TypeSpec + `@repo/api-types-ts` + portal UI → **no Minimal API**.  
2. **LHDN SDK paths:** `{base}/api/v1` + path `/api/v1/lhdn/...` → **double prefix**.  
3. **LHDN validate:** OpenAPI + Kiota clients → **no backend route**.  
4. **GET LHDN api-keys:** Spec/SDK → **no backend route**.  
5. **One lhdn-config GET/PUT:** Spec only.  
6. **Billing net-profit:** Spec only.  
7. **Billing summary date filters:** Spec only; service ignores filters.  
8. **Ops chat/stream & system-message:** Impl + frontend only.  
9. **Communications broadcast status/preview:** Impl only; DTOs outside TypeSpec.  
10. **docs-one/docs-ops:** Ship billing APIs to non-billing audiences.  
11. **docs-commerce:** Written but never built or hosted.  
12. **Messaging notify:** Internal command on public map group.  
13. **Payment webhooks:** External integration surface with zero contract.  
14. **OpenAPI version `0.0.0`:** No versioning signal to integrators.  
15. **CreateManualSubscriber:** Spec DTO vs local request type dual definition.

---

This analysis is read-only against the tree as of the scan date; regenerating OpenAPI without fixing TypeSpec will **re-emit** the same LHDN path and phantom-route problems into SDKs.
