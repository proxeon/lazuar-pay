# TypeSpec Wave B (FW-6) — Implementation Analysis

**Date:** 2026-08-09  
**Repo:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay`  
**Workstream:** FW-6 / checklist F09 (`plans/004-maintenance/checklists-future/phase-f09-typespec-wave-b.md`)  
**Prior art (must read):**

| Document | Role |
|----------|------|
| `plans/004-maintenance/phase-05-done.md` | Wave A P0 closed; Wave B deferrals listed |
| `plans/004-maintenance/phase-05-analysis.md` | Dual DTO mapping pattern that worked |
| `plans/004-maintenance/FUTURE-WORK.md` § FW-6 | Target end-state + outline |
| `plans/004-maintenance/05-typespec-contracts.md` | Full package inventory (Wave A residual tables) |
| `plans/004-maintenance/phase-14-done.md` | Orphan cleanup already done (PaymentRecord, LinkedCheckout) |
| `docs/architecture-decision-log/005-typespec-api-contract-generation.md` | Pipeline ADR |
| `docs/architecture-decision-log/006-separation-of-external-and-internal-contracts.md` | Edge DTO vs MediatR |
| `docs/architecture-decision-log/007-product-scoped-api-references.md` | docs-\*.tsp |
| `docs/contracts/openapi-vs-minimal-api.md` | Honesty allowlist |

**Nature:** Uncondensed analysis of **how** to implement Wave B. No app code changes in this document. Read-only against the tree as of 2026-08-09.

---

## 0. Executive summary

Phase 05 (Wave A) closed the **P0 honesty** surfaces:

1. Commerce **subscriber** dual DTOs → `Lazuar.ApiTypes`
2. Payments **integration checkout** dual DTOs → `Lazuar.ApiTypes`
3. Payments path trailing slash → canonical `/integrations/payments/checkouts`
4. Broadcast **targeting phantoms** removed from TypeSpec + command
5. `task gen` + focused ModuleTests green

**Wave B (FW-6)** is the residual product/DX contract work. It is **not** blocked by calendar gates (unlike FW-1 keys) or product reopen gates (unlike FW-2 webhooks / FW-5 extracts). It **can parallel** Keys/SQL/BB tracks.

Wave B done means:

| Criterion | Meaning |
|-----------|---------|
| No dual edge DTO pairs on **shipping admin/public** surfaces targeted this wave | Local C# request records that mirror OpenAPI deleted; endpoints bind generated types |
| Impl-only / TSP-only gaps decided and executed | Each gap: **add to TSP**, **document as internal (allowlist)**, or **remove** |
| Payments product OpenAPI security schemes honest | Scalar / OpenAPI `security` + `securitySchemes` match M2M reality (mirror LHDN) |
| Optional CI path honesty gate | Automated check that OpenAPI paths and Minimal API maps do not silently diverge (beyond client-regen diff) |
| Gen pipeline green | `task gen` clean; committed clients match |

**Do not** re-open money-as-string redesign, broadcast targeting productization, CRM HTTP surface, or Messaging as product docs unless product explicitly asks. Those remain policy notes or allowlist entries.

---

## 1. What Wave A already fixed (do not re-do)

### 1.1 Dual DTOs eliminated (Phase 05)

| Was local (deleted) | Generated `Lazuar.ApiTypes` | Endpoint file |
|---------------------|----------------------------|---------------|
| `CreateManualSubscriberRequest` | `CreateManualSubscriberDto` | `Modules/Commerce/Infrastructure/Endpoints/SubscriberEndpoints.cs` |
| `GenerateCustomerPortalRequest` | `GenerateCustomerPortalRequestDto` | same |
| `GenerateCustomerPortalResponse` | `GenerateCustomerPortalResponseDto` | same |
| `RecordSubscriberPaymentRequest` | `RecordPaymentRequestDto` | same |
| `CreateIntegrationCheckoutRequest` | `CreateIntegrationCheckoutRequestDto` | `Modules/Payments/Infrastructure/IntegrationEndpoints.cs` |
| local `IntegrationCheckoutResponseDto` | `IntegrationCheckoutResponseDto` | same |

**Pattern that worked (repeat for Wave B dual DTOs):**

1. Confirm TypeSpec model field parity with local record (snake_case JSON names).
2. Switch endpoint parameter types to generated POCOs (`using Lazuar.ApiTypes`).
3. Cast money at ACL: generated `double` → command `decimal` (v1 policy; do not change money representation in Wave B).
4. Delete local record types.
5. `task gen` if TSP changed; `dotnet build`; focused ModuleTests.
6. Resolve CS0104 on `ProblemDetails` with fully-qualified `Microsoft.AspNetCore.Mvc.ProblemDetails` when both namespaces collide.

### 1.2 Broadcast targeting honesty (Phase 05)

- Removed `target_plan_id` / `target_status` / `target_is_reminder_only` from `CreateBroadcastRequestDto`.
- Dropped unused optional params from `SendBroadcastCommand`.
- Comment in `packages/api-spec/modules/communications/models.tsp` documents re-add condition: query filters + Broadcast storage + fan-out end-to-end.

### 1.3 Orphans already resolved (Phase 14 — not Wave B work)

| Model | Resolution |
|-------|------------|
| `Core.LinkedCheckoutDto` | Removed from `common/models.tsp` |
| `Commerce.PaymentRecordDto` | Removed from commerce models |

CRM models-only and Messaging intentionally-thin are **documented and accepted** in `packages/api-spec/README.md`. Do **not** delete CRM models without replacing `Lazuar.ApiTypes` consumers (`ICrmQueryService`, handlers/tests).

### 1.4 Path slash + Idempotency-Key (Phase 05)

- OpenAPI path key: `/integrations/payments/checkouts` (no trailing slash).
- Optional `@header("Idempotency-Key")` on create checkout (also body `idempotency_key`).

---

## 2. Remaining dual DTO inventory (local C# vs `Lazuar.ApiTypes`)

### 2.1 Definition of “dual DTO” for Wave B

A **dual DTO** is a hand-written C# type used as an **HTTP edge** bind/return shape that **mirrors** a TypeSpec model already emitted into `Lazuar.ApiTypes` / `@repo/api-types-ts`.

**Not dual DTOs** (do not convert under dual-DTO workstream without separate design):

| Category | Examples | Why excluded |
|----------|----------|--------------|
| Private query-service raw rows | `RawProductDto`, `RawSubDto`, etc. in `CommerceQueryService.*.cs` | Infra SQL projection; not HTTP edge |
| MediatR commands/events | `CreateProductCommand`, integration events | ADR 006 internal contracts |
| Module Contracts used only between modules | most of `Modules/*/Contracts` | Internal boundary |
| Impl-only edge DTOs with **no** TypeSpec twin yet | `BroadcastStatusDto`, `BroadcastCostPreviewDto`, `MessageDeliveryLogDto` | These are **impl-only gaps**, not duals — handle under §3 |

### 2.2 Confirmed dual pairs (shipping admin surfaces)

#### A. Commerce products — **primary Wave B dual target**

| Item | Path |
|------|------|
| Local request | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints/ProductEndpoints.cs` |
| Local types | `CreateProductRequest` (lines ~17–29), `UpdateProductRequest` (lines ~31–44) |
| TypeSpec | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/api-spec/modules/commerce/models/product.tsp` |
| TSP models | `CreateProductRequestDto`, `UpdateProductRequestDto`, `ProductDto`, `CheckoutConfigurationDto` |
| Generated C# | `packages/api-types-dotnet/Lazuar.ApiContracts.cs` — `CreateProductRequestDto`, `UpdateProductRequestDto` |
| Generated TS | `packages/api-types-ts/src/index.ts` — `Commerce.CreateProductRequestDto`, `Commerce.UpdateProductRequestDto` |
| Routes (TSP) | `packages/api-spec/modules/commerce/admin-routes.tsp` — `POST/PUT /admin/commerce/products` |
| Routes (impl) | same file `ProductEndpoints` under group `/admin/commerce` |
| Frontend consumers (already generated) | `apps/lazuar-ops/src/modules/commerce/components/CreateProductModal.tsx`, `ProductDetailPanel.tsx` |

**Field mapping (local → generated):**

| Local (`CreateProductRequest`) | Generated (`CreateProductRequestDto`) | Wire JSON | Notes |
|--------------------------------|----------------------------------------|-----------|-------|
| `Name` | `Name` | `name` | |
| `Slug` | `Slug` | `slug` | |
| `Price` (`decimal`) | `Price` (`double`) | `price` | Cast `(decimal)req.Price` at ACL |
| `Pricing_model` | `Pricing_model` | `pricing_model` | |
| `Minimum_price` (`decimal`) | `Minimum_price` (`double`) | `minimum_price` | Cast |
| `Currency` | `Currency` | `currency` | |
| `Interval` | `Interval` | `interval` | |
| `Gateway_name` | `Gateway_name` | `gateway_name` | |
| `Requires_address` | `Requires_address` | `requires_address` | |
| `Requires_tax_id` | `Requires_tax_id` | `requires_tax_id` | |
| `Requires_phone` | `Requires_phone` | `requires_phone` | |
| `Fulfillment_targets` | `Fulfillment_targets` | `fulfillment_targets` | Null-coalesce to empty list |

| Local (`UpdateProductRequest`) | Generated (`UpdateProductRequestDto`) | Extra |
|--------------------------------|----------------------------------------|-------|
| All create fields + `Is_active` | All create fields + `Is_active` | same cast pattern |

**GET responses already use generated `ProductDto`** — only POST/PUT request bodies are dual.

**Implementation steps (PR-B1):**

1. No TypeSpec change required if shapes already match (they do).
2. In `ProductEndpoints.cs`:
   - Change `CreateProductRequest req` → `CreateProductRequestDto req`
   - Change `UpdateProductRequest req` → `UpdateProductRequestDto req`
   - Cast doubles to decimals when constructing `CreateProductCommand` / `UpdateProductCommand`
3. Delete both local records.
4. Build + existing `CommerceProductCompletenessTests` (already imports `Lazuar.ApiTypes`).
5. No `task gen` required unless TSP was edited.

#### B. Commerce refunds — **second dual target**

| Item | Path |
|------|------|
| Local request | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints/TransactionEndpoints.cs` |
| Local type | `RecordRefundRequest` (lines ~16–20) |
| TypeSpec | `packages/api-spec/modules/commerce/models/subscriber.tsp` — `RecordRefundRequestDto` |
| Route TSP | `admin-routes.tsp` — `POST /admin/commerce/transactions/{id}/refund` body optional |
| Generated C# | `RecordRefundRequestDto` (`Amount?`, `Gateway_name?`, `Subscription_id?`, `Tax_amount?` all nullable doubles) |

**Shape nuance (important):**

| Field | Local | Generated | Risk |
|-------|-------|-----------|------|
| `amount` | `decimal?` | `double?` | Cast |
| `gateway_name` | `string?` | `string?` | OK |
| `subscription_id` | `string?` | `string?` | OK (parse Guid in endpoint) |
| `tax_amount` | `decimal Tax_amount = 0m` **non-null default 0** | `double? Tax_amount` **nullable** | Behavior: local always has 0 when omitted; generated may be null → keep `req?.Tax_amount ?? 0` / cast |

**Implementation steps (can share PR-B1 or PR-B1b):**

1. Bind `RecordRefundRequestDto? req`.
2. Map: `req?.Amount is double a ? (decimal)a : null`, same for tax defaulting to 0m.
3. Delete local `RecordRefundRequest`.
4. No TSP change.

### 2.3 Explicit non-duals scanned (endpoints tree)

Full greps of `**/Endpoints*.cs` and `**/Endpoints/**/*.cs` for public `record`/`class` edge types yielded **only**:

| File | Type | Classification |
|------|------|----------------|
| `ProductEndpoints.cs` | `CreateProductRequest`, `UpdateProductRequest` | **Dual** (§2.2 A) |
| `TransactionEndpoints.cs` | `RecordRefundRequest` | **Dual** (§2.2 B) |
| `Messaging/Infrastructure/Endpoints.cs` | `MessageDeliveryLogDto` | Impl-only / allowlist (§3.4) |
| `Payments/Infrastructure/PlatformEndpoints.cs` | private `GlobalUserDto` | Internal private projection, not dual |

All other admin/public modules already bind `Lazuar.ApiTypes` for request/response shapes on product routes (subscribers, coupons, templates, LHDN, One credentials, billing ledger DTOs, etc.).

### 2.4 Dual DTO residual risk after Wave B dual PR

After A+B:

- **No known dual pairs** on Commerce admin product/transaction write surfaces.
- Future dual reintroduction risk: any new endpoint that defines a local `record …Request` next to an existing `*RequestDto` in TSP. Review checklist item: “bind `Lazuar.ApiTypes` or add TSP first.”

---

## 3. TSP-only / impl-only gaps (honesty matrix)

For each gap: **implement into TypeSpec**, **document as internal (allowlist)**, or **remove endpoint**. Phase 05 marked most as “document internal / deferred Wave B execute.” Wave B **executes** those decisions.

### 3.1 Billing signed final PDF — **impl-only**

| Side | Artifact |
|------|----------|
| **Impl** | `GET /public/billing/{tenantSlug}/documents/{ledgerEntryId}` in `Modules/Billing/Infrastructure/Endpoints/PublicBillingEndpoints.cs` (HMAC `sig` + `exp`, R2 presigned redirect) |
| **TSP** | **Absent**. Public billing in `packages/api-spec/modules/billing/routes.tsp` has only: profile + **draft** `…/documents/draft/{sessionId}` |
| **OpenAPI** | `dist/openapi.yaml` / `dist/billing/openapi.yaml` contain draft path only |

**Draft PDF** is already specified (`getDraftDocument` returns `bytes | ProblemDetails`).

**Decision options for Wave B:**

| Option | When to pick | Work |
|--------|--------------|------|
| **B-PDF-Add** — Add route to TypeSpec | Product/docs/hub need typed client or Scalar honesty for final invoices | Add `getFinalDocument` under `PublicBillingOperations` with `@query sig`, `@query exp`; response is **redirect** (302) not PDF bytes — model carefully (see below) |
| **B-PDF-Allowlist** — Keep internal/undocumented | No typed client consumer; links are email/HMAC only | Update `docs/contracts/openapi-vs-minimal-api.md` allowlist with explicit row; note in billing README / phase done |

**Recommended default: B-PDF-Add** if developers hub bills public document links as product; otherwise **B-PDF-Allowlist** is honest and cheaper.

**TypeSpec modeling note (if adding):**

Runtime returns `TypedResults.Redirect(downloadUrl)` — OpenAPI typically documents this poorly as `bytes`. Options:

1. Document as `void` / 302 with description “redirect to time-limited R2 URL” (no body schema).
2. Or document response as `DocumentDownloadUrlDto`-style if product later changes to JSON `{ url }` (would be a **behavior change** — out of honesty-only scope).

Do **not** claim `bytes` PDF for the final path unless runtime changes to stream PDF (draft does stream PDF; final redirects).

**Suggested TSP sketch (honesty, non-breaking):**

```tsp
@get
@route("/{tenantSlug}/documents/{ledgerEntryId}")
@doc("Signed final ledger PDF link. Requires HMAC sig + exp (payload tenantSlug:final:ledgerEntryId:exp). Responds with redirect to R2 presigned URL.")
getFinalDocument(
  @path tenantSlug: string,
  @path ledgerEntryId: string,
  @query sig: string,
  @query exp: int64,
): LazuarApi.Core.ProblemDetailsResponse; // 302 success is outside typed body; document in @doc
```

(Exact return typing may need TypeSpec `@statusCode` / empty success model — implementer should match compiler constraints.)

### 3.2 Broadcast preview / status — **impl-only (module DTOs)**

| Side | Artifact |
|------|----------|
| **Impl POST** | `POST /admin/communications/broadcasts` — uses generated `CreateBroadcastRequestDto` ✅ |
| **Impl GET preview** | `GET /admin/communications/broadcasts/preview` → `BroadcastCostPreviewDto` |
| **Impl GET status** | `GET /admin/communications/broadcasts/{id}` → `BroadcastStatusDto` |
| **TSP** | Only `sendBroadcast` POST in `admin-routes.tsp` — **no GET routes** |
| **DTO ownership** | `Modules/Communications/Contracts/BroadcastDtos.cs` — PascalCase C# props + `[JsonPropertyName("snake_case")]` |

**Wire shapes (already snake_case JSON):**

`BroadcastStatusDto`:

- `id`, `status`, `total_recipients`, `sent_count`, `suppressed_count`, `failed_count`
- `credits_reserved`, `credits_used` (runtime **hardcoded 0** — free broadcasts)
- `created_at`, `completed_at?`, `failure_reason?`

`BroadcastCostPreviewDto`:

- `recipient_count`, `credits_per_recipient` (0), `total_credits` (0), `sufficient_credits` (true), `available_credits`

**Frontend:** No `lazuar-ops` openapi-fetch consumer for `/broadcasts/preview` or status GET found (template preview is separate). Still real admin API routes used by any future UI / manual ops.

**Decision options:**

| Option | Pros | Cons |
|--------|------|------|
| **B-BC-Add** — Add TSP models + GET routes; switch endpoints to generated types; delete module edge DTOs or keep as aliases | Full honesty; typed clients ready | Credits fields are zeros — document as reserved/compat; gen + binding work |
| **B-BC-Allowlist** — Document internal until UI productizes | Zero gen churn | Frontend cannot type paths; honesty doc stays partial |

**Recommended: B-BC-Add** — routes are real OrgAdmin product surface under `/admin/communications`, same interface that already has templates. Credits zeros should be **documented in `@doc`**, not removed (preserves DTO compatibility; product may reintroduce credit cost later).

**Implementation steps (PR-B2):**

1. Add to `packages/api-spec/modules/communications/models.tsp`:

```tsp
model BroadcastStatusDto {
  id: string;
  status: string;
  total_recipients: int32;
  sent_count: int32;
  suppressed_count: int32;
  failed_count: int32;
  /** Reserved; v1 broadcasts are free — always 0. */
  credits_reserved: int32;
  /** Reserved; v1 broadcasts are free — always 0. */
  credits_used: int32;
  created_at: utcDateTime;
  completed_at?: utcDateTime;
  failure_reason?: string;
}

model BroadcastCostPreviewDto {
  recipient_count: int32;
  /** Reserved; v1 free — always 0. */
  credits_per_recipient: int32;
  /** Reserved; v1 free — always 0. */
  total_credits: int32;
  sufficient_credits: boolean;
  available_credits: int32;
}
```

2. Add to `admin-routes.tsp` inside `AdminCommunicationsOperations`:

```tsp
@get
@route("/broadcasts/preview")
previewBroadcastCost(): BroadcastCostPreviewDto | LazuarApi.Core.ProblemDetailsResponse;

@get
@route("/broadcasts/{id}")
getBroadcastStatus(@path id: string):
  | BroadcastStatusDto
  | LazuarApi.Core.ProblemDetailsResponse;
```

3. `task gen` → commit clients.

4. Update `BroadcastEndpoints.cs` to construct generated types (`new BroadcastStatusDto { Id = …, Status = … }` with NSwag property names `Id`, `Status`, `Total_recipients`, …).

5. **Naming collision:** module Contracts already defines `BroadcastStatusDto` / `BroadcastCostPreviewDto`. After gen, either:
   - **Preferred:** delete Contracts edge DTOs; endpoints + any cross-module refs use `Lazuar.ApiTypes`; or
   - Rename module types and keep temporary wrappers (worse).

6. Grep for `Modules.Communications.Contracts.BroadcastStatusDto` / `BroadcastCostPreviewDto` usages before delete.

7. Tests: existing Broadcast\* ModuleTests still pass; add assertion on snake_case JSON if useful.

**Route order note:** Minimal API registers `GET /broadcasts/preview` before `GET /broadcasts/{id}` — keep that order so `preview` is not captured as a Guid. TypeSpec path order does not affect ASP.NET; keep endpoint registration order.

### 3.3 Communications public compliance routes — **impl-only**

| Route | File | Auth |
|-------|------|------|
| `GET /public/communications/unsubscribe` | `PublicComplianceEndpoints.cs` | None (HMAC `sig` query) |
| `POST /public/communications/webhooks/resend` | same | Svix/Resend signature headers |

**TSP:** no `public-routes.tsp` under communications; `docs-commerce` bundles admin communications only.

**Response types:** HTML for unsubscribe; opaque webhook 200. Poor fit for typed client codegen.

**Recommended: B-CC-Allowlist** (document as operational / machine endpoints).

Update:

- `docs/contracts/openapi-vs-minimal-api.md` — expand row from “Partial” to explicit allowlist table entries with reason.
- Optionally short `@doc` comment block in `communications/admin-routes.tsp` or `models.tsp` pointing to allowlist (do not invent HTML/OpenAPI for email clients).

**Only add to TSP if** product wants portal-hosted unsubscribe page typed against Hub — unlikely (HTML is server-rendered).

### 3.4 Messaging notify + delivery-logs — **allowlist (keep out)**

| Route | Binding | Status |
|-------|---------|--------|
| `POST /messaging/notify` | **MediatR command as body** (ADR 006 anti-pattern) | OrgAdmin internal |
| `GET /messaging/delivery-logs` | local `MessageDeliveryLogDto` | OrgAdmin internal |

`packages/api-spec/modules/messaging/models.tsp` is intentionally thin (no routes). **Wave B: leave allowlisted.** Optional later PR: introduce edge DTOs + TSP if console needs typed diagnostics — not FW-6 must.

### 3.5 Templates legacy-cleanup — **impl-only ops utility**

| Route | `DELETE /admin/communications/templates/legacy-cleanup` |
|-------|----------------------------------------------------------|
| File | `TemplateEndpoints.cs` |
| Purpose | One-shot orphan community-era template name purge |

**Recommended:** remain **impl-only / allowlist** until all tenants cleaned, then **delete endpoint** (see `01-removable-dead-code.md`). Do **not** add to product OpenAPI.

### 3.6 Payments inbound gateway webhooks — **allowlist**

| Route | `POST /webhooks/payments/{gatewayType}/{tenantId}` |
|-------|-----------------------------------------------------|
| Why | Provider-signed inbound; not integrator product API |

Keep allowlisted. Outbound envelope `PaymentWebhookPayloadDto` is correctly **schema-only** (docs for customers verifying signatures).

### 3.7 CRM models-only — **accepted orphan (not Wave B delete)**

Documented in README. Backend uses generated types without HTTP. Product docs do not import CRM. **No action** unless product adds CRM HTTP (then add routes in same PR as endpoints).

### 3.8 Summary honesty matrix

| Gap | Impl path(s) | TSP today | Wave B decision |
|-----|--------------|-----------|-----------------|
| Final signed PDF | `GET /public/billing/.../documents/{ledgerEntryId}` | Missing | **Add TSP** *or* **allowlist** (prefer add if public product; else allowlist) |
| Broadcast cost preview | `GET .../broadcasts/preview` | Missing | **Add TSP + switch to ApiTypes** |
| Broadcast status | `GET .../broadcasts/{id}` | Missing | **Add TSP + switch to ApiTypes** |
| Public unsubscribe | `GET /public/communications/unsubscribe` | Missing | **Allowlist** |
| Resend webhook | `POST /public/communications/webhooks/resend` | Missing | **Allowlist** |
| Template legacy-cleanup | `DELETE .../templates/legacy-cleanup` | Missing | **Allowlist** (temp ops) |
| Messaging notify/logs | `/messaging/*` | Intentionally thin | **Allowlist** |
| Gateway inbound webhooks | `/webhooks/payments/*` | Absent | **Allowlist** |
| Dual product/refund DTOs | Product + Transaction endpoints | Models exist | **Switch to ApiTypes** (§2) |
| Payments security schemes | routes auth real | docs-payments **no** `securitySchemes` | **Add @useAuth** (§4) |

### 3.9 TSP-only residual

After Wave A, no known **TSP-only phantom routes** remain on P0 surfaces (targeting fields removed). Re-verify after Wave B adds:

- Search generated OpenAPI paths vs `MapGet`/`MapPost` inventory.
- Any new model without a route must be justified (CRM pattern) or removed.

---

## 4. Security schemes (Payments product docs + related)

### 4.1 Current state

| Product OpenAPI | `@useAuth` | Emitted `security` / `securitySchemes` |
|-----------------|------------|----------------------------------------|
| **LHDN** `docs-lhdn.tsp` | `@useAuth(BearerAuth \| ApiKeyAuth<ApiKeyLocation.header, "Authorization">)` | **Yes** — Bearer + ApiKeyAuth |
| **Payments** `docs-payments.tsp` | **None** (prose in `@doc` only) | **No** — confirmed `dist/payments/openapi.yaml` has no `securitySchemes` block |
| **Payments** `modules/payments/routes.tsp` | Comment-only auth; **no** `@useAuth` on interface | Monolith OpenAPI paths for checkouts also lack per-operation security from this interface |
| Commerce / Billing / Communications admin | `@useAuth(BearerAuth)` on interface | Present for JWT-style Bearer |
| One | Per-operation `@useAuth(BearerAuth)` | Present |

Runtime payments integration uses scoped API key middleware (`Authorization: Bearer sk_test_|sk_live_` + scopes `payments.checkouts:write|read`) — same class of credential as LHDN M2M.

### 4.2 Target state (PR-B3)

Mirror LHDN DX for Payments:

1. **On `docs-payments.tsp` service namespace** (and preferably on the integration interface in `routes.tsp` so monolith OpenAPI is honest too):

```tsp
// docs-payments.tsp — after @server lines
@useAuth(BearerAuth | ApiKeyAuth<ApiKeyLocation.header, "Authorization">)
namespace LazuarApi;
```

And/or on `IntegrationCheckoutOperations`:

```tsp
@useAuth(BearerAuth | ApiKeyAuth<ApiKeyLocation.header, "Authorization">)
@route("/integrations/payments")
interface IntegrationCheckoutOperations { ... }
```

2. Keep prose scopes in `@doc` (OpenAPI cannot express custom scope allowlists without OAuth2 schemes; scopes stay documentation).

3. `task gen` → regenerate product `dist/payments/openapi.yaml` (local/gitignored) and ensure committed clients unchanged **or** only trivially changed (security is OpenAPI metadata; openapi-typescript/NSwag DTO surface usually unaffected).

4. Spot-check Scalar on developers hub after rebuild: Authorize button appears for Payments like LHDN.

### 4.3 Optional follow-ups (not blocking Wave B)

| Item | Note |
|------|------|
| OrgAdmin cookie vs Bearer | Browser apps use cookies; OpenAPI says Bearer — known DX lie for console products; same as Commerce/Billing today |
| Provision key | Still prose-only on One provision routes |
| OAuth2 scopes object for `payments.checkouts:*` | Nice-to-have; higher design cost |

### 4.4 Ops / One docs packages

Phase 05 checklist mentioned “Align ops/one docs packages if missing schemes.” Spot check:

- One routes already use `@useAuth(BearerAuth)` heavily.
- Ops routes use `@useAuth(BearerAuth)`.
- Priority remains **Payments product docs** (integrator-facing M2M). Ops/One only if Scalar shows missing Authorize after audit.

---

## 5. Generation pipeline (how Wave B must touch it)

### 5.1 Command graph

```text
task gen
  ├─ gen:spec          # packages/api-spec: pnpm build
  │    tsp compile main.tsp           → dist/openapi.yaml
  │    tsp compile docs-one.tsp       → dist/one/openapi.yaml
  │    tsp compile docs-ops.tsp       → dist/ops/openapi.yaml
  │    tsp compile docs-billing.tsp   → dist/billing/openapi.yaml
  │    tsp compile docs-lhdn.tsp      → dist/lhdn/openapi.yaml
  │    tsp compile docs-commerce.tsp  → dist/commerce/openapi.yaml  (+ communications admin)
  │    tsp compile docs-payments.tsp  → dist/payments/openapi.yaml
  ├─ gen:types-ts      # openapi-typescript dist/openapi.yaml → api-types-ts/src/index.ts
  ├─ gen:types-dotnet  # NSwag nswag.json → Lazuar.ApiContracts.cs (namespace Lazuar.ApiTypes)
  └─ gen:sdk-lhdn      # Kiota TS + C# from dist/lhdn/openapi.yaml
```

**Sources (Task cache):** `packages/api-spec/**/*.tsp`, `tspconfig.yaml`  
**Generates (Task cache):** `dist/openapi.yaml`, `api-types-ts/src/index.ts`, `Lazuar.ApiContracts.cs`  
**Note:** LHDN SDK outputs and product-scoped dist YAML are **not** listed under Task `generates` — still produced by `task gen`; CI diffs SDK trees.

### 5.2 Rules for implementers

1. **Never hand-edit** `Lazuar.ApiContracts.cs`, `api-types-ts/src/index.ts`, or Kiota `generated/**`.
2. **Always commit** regenerated clients after TSP changes (CI `contracts` job fails otherwise).
3. **`dist/` is gitignored** — do not commit OpenAPI YAML; Docker/developers hub rebuilds at image build.
4. Dual-DTO-only PRs with **no** TSP edits: **no** `task gen` required.
5. After broadcast/PDF TSP adds: run full `task gen` (or at least gen:spec + types); expect large client diffs for new models.

### 5.3 Package layout (Wave B touch points)

```text
packages/api-spec/
├── modules/commerce/models/product.tsp      # already has Create/Update DTOs
├── modules/commerce/models/subscriber.tsp   # already has RecordRefundRequestDto
├── modules/commerce/admin-routes.tsp        # no change for dual switch
├── modules/communications/models.tsp       # ADD broadcast status/preview models
├── modules/communications/admin-routes.tsp # ADD GET routes
├── modules/billing/routes.tsp              # OPTIONAL final document route
├── modules/payments/routes.tsp             # ADD @useAuth
├── docs-payments.tsp                       # ADD @useAuth
├── main.tsp                                # no structural change expected
└── package.json                            # build chain OK as-is
```

### 5.4 NSwag naming expectations

- Properties: JSON snake_case → C# `Pascal_snake` hybrid (`Total_recipients`, `Gateway_name`) — same as Phase 05.
- Money: `float64` → `double`.
- Nullable optionals become `T?` when `generateOptionalPropertiesAsNullable: true`.

### 5.5 CI that already exists (client honesty)

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

**Proves:** committed clients match TypeSpec after clean gen.  
**Does not prove:** Minimal API routes ⊆ OpenAPI paths, or vice versa.

---

## 6. CI honesty gate design (OpenAPI ↔ Minimal API paths)

### 6.1 Problem

Phase 05 left optional path honesty to Phase 06; Phase 06 shipped **Taskfile/CI alignment only**, not path comparison. Residual risk remains: a developer can add `MapPost` without TypeSpec (impl-only drift) or add TypeSpec without endpoint (phantom).

### 6.2 Goals

| Goal | Non-goal |
|------|----------|
| Detect **path-level** drift between OpenAPI and Minimal API | Full schema/body parity (harder; second gen) |
| Support intentional **allowlist** for internal routes | Force Messaging/webhooks into public OpenAPI |
| Run in CI `contracts` job or a sibling step | Block merge on every experimental host route without process |

### 6.3 Recommended architecture

#### Inputs

1. **OpenAPI paths:** After `task gen:spec` (or full `task gen`), parse `packages/api-spec/dist/openapi.yaml` `paths:` keys. Normalize:
   - Strip trailing slashes.
   - Normalize `{param}` names to `{*}` or keep template form consistently.
2. **Minimal API routes:** One of:

| Strategy | Pros | Cons |
|----------|------|------|
| **A. Static scrape** of `MapGroup`/`MapGet`/`MapPost`/`MapPut`/`MapDelete` string literals under `apps/lazuar-api/Modules/**/Infrastructure/**/*Endpoints*.cs` (+ host platform maps) | No runtime; pure CI | Regex fragility; miss dynamic maps; miss `MapMethods` |
| **B. Runtime export** endpoint: only in Testing host, dump `EndpointDataSource` routes | Accurate ASP.NET truth | Needs test host spin; env complexity |
| **C. Hybrid** — static scrape as default + periodic manual audit | Simple first gate | Same scrape limits |

**Recommended Wave B first ship: Strategy A (static scrape)** with a maintained allowlist file. Graduate to B later if false negatives appear.

#### Comparison modes

| Mode | Assertion | Catches |
|------|-----------|---------|
| **OpenAPI ⊆ Minimal** (default) | Every OpenAPI path has a Minimal counterpart | Phantoms in TypeSpec |
| **Minimal shipping ⊆ OpenAPI ∪ allowlist** | Every non-allowlisted Minimal route is in OpenAPI | Impl-only product drift |
| **Exact equality** | Too strict — avoid | Breaks on `{id}` vs `{id:guid}` and optional segments |

Path template normalization rules:

- `{id:guid}` → `{id}`
- `{checkoutId:guid}` → `{checkoutId}`
- Case-sensitive path segments as-is
- Host prefix: both sides relative to `/api/v1` (Program `apiGroup`)

#### Allowlist file (suggested)

`packages/api-spec/honesty-allowlist.yaml` (or `.json`):

```yaml
# Routes intentionally absent from TypeSpec product surface
impl_only:
  - method: GET
    path: /public/communications/unsubscribe
    reason: HTML email compliance link
  - method: POST
    path: /public/communications/webhooks/resend
    reason: Resend/Svix machine webhook
  - method: DELETE
    path: /admin/communications/templates/legacy-cleanup
    reason: Temporary ops utility
  - method: POST
    path: /messaging/notify
    reason: Internal OrgAdmin fan-in
  - method: GET
    path: /messaging/delivery-logs
    reason: Internal diagnostics
  - method: POST
    path: /webhooks/payments/{gatewayType}/{tenantId}
    reason: Gateway inbound; provider-specific
  # Final PDF if choosing allowlist instead of TSP:
  # - method: GET
  #   path: /public/billing/{tenantSlug}/documents/{ledgerEntryId}
  #   reason: HMAC email link; redirect response

# OpenAPI paths that may lack a 1:1 Map (rare; prefer fix TSP)
openapi_only_exceptions: []
```

When Wave B **adds** broadcast GETs and/or final PDF to TSP, remove those rows from allowlist.

#### Implementation sketch (script)

Location options:

1. `scripts/check-openapi-minimal-honesty.mjs` (Node; YAML parse via existing deps or pure JS)
2. `apps/lazuar-api/tests/Lazuar.ArchitectureTests/OpenApiPathHonestyTests.cs` (if scrape from filesystem)

Suggested Node flow (fits contracts job already on Node 22 + pnpm):

```text
1. Ensure dist/openapi.yaml exists (run gen:spec first)
2. Load paths from OpenAPI
3. Walk Endpoints*.cs with regex:
   MapGroup\("([^"]+)"\)  +  Map(Get|Post|Put|Delete)\("([^"]*)"
   (handle nested groups carefully — maintain stack of group prefixes)
4. Normalize + compare
5. Exit 1 with actionable diff listing
```

**Group prefix stacking** is the hard part: e.g. `MapGroup("/admin/communications")` then `MapGet("/broadcasts/preview")` → `/admin/communications/broadcasts/preview`. Endpoints files often nest via extension methods on `RouteGroupBuilder` — scrape needs to track `Map*Endpoints(this RouteGroupBuilder group)` assuming group already has prefix from parent `MapGroup`.

**Practical v1 simplification:** maintain a **checked-in expected path inventory** generated once by a richer tool, and CI only verifies OpenAPI paths ⊆ expected ∪ allowlist, plus a manual “regenerate inventory” task. Or start with **OpenAPI ⊆ Minimal only** (easier scrape if we also accept `dotnet` test host later).

### 6.4 CI wire-up

In `.github/workflows/ci.yml` `contracts` job, after `task gen --force`:

```yaml
- name: OpenAPI ↔ Minimal path honesty
  run: node scripts/check-openapi-minimal-honesty.mjs
```

Or Taskfile:

```yaml
contracts:honesty:
  desc: OpenAPI paths vs Minimal API allowlist check
  deps: [gen:spec]
  cmds:
    - node scripts/check-openapi-minimal-honesty.mjs
```

`task gen` should **not** necessarily depend on honesty (gen can succeed while honesty fails). CI runs both.

### 6.5 Failure message quality

Print:

```text
PHANTOM (in OpenAPI, not in Minimal, not exempted):
  GET /admin/communications/broadcasts/preview

UNDOCUMENTED (in Minimal, not in OpenAPI, not allowlisted):
  GET /public/billing/{tenantSlug}/documents/{ledgerEntryId}

Allowlist: packages/api-spec/honesty-allowlist.yaml
```

### 6.6 Phased delivery of the gate

| Sub-phase | Deliverable |
|-----------|-------------|
| **B4a** | Script + allowlist encoding today’s intentional gaps; OpenAPI ⊆ Minimal check green |
| **B4b** | Minimal ⊆ OpenAPI ∪ allowlist; tighten as Wave B adds TSP routes |
| **B4c** (optional) | Architecture test host dumps `EndpointDataSource` for accuracy |

### 6.7 What the gate still will not catch

- Body field dual DTOs (covered by dual-DTO PRs + review, not path gate)
- Auth policy mismatch (`OrgAdmin` vs public)
- Response shape `BadRequest<string>` vs `ProblemDetails`
- Trailing slash / redirect vs bytes response modeling

---

## 7. PR breakdown (recommended sequence)

Follow F09 checklist spirit: **one concern per PR** (or tightly related). Suggested ticket titles match `FUTURE-WORK.md`.

### PR-B0 — Analysis / tracking (this doc)

- [x] Uncondensed analysis at `plans/005-remaining/08-typespec-wave-b.md`
- No code

### PR-B1 — Dual DTOs: Commerce products + refunds

**Scope:**

- `ProductEndpoints.cs` → `CreateProductRequestDto` / `UpdateProductRequestDto`
- `TransactionEndpoints.cs` → `RecordRefundRequestDto`
- Delete local records
- Cast double→decimal at ACL
- Tests green; **no** `task gen` unless TSP edited

**Out of scope:** Broadcast, PDF, security, CI gate

**Risk:** Low — shapes already aligned; frontend already uses generated TS types for products.

### PR-B2 — Broadcast preview + status into TypeSpec

**Scope:**

- TSP models + routes
- `task gen` + commit clients
- `BroadcastEndpoints` bind generated types
- Remove/replace `Modules/Communications/Contracts/BroadcastDtos.cs` edge types
- Doc credits-as-zero
- Update honesty allowlist / `openapi-vs-minimal-api.md` when gate exists

**Out of scope:** Broadcast targeting productization; credit charging

**Risk:** Medium — naming collisions with Contracts DTOs; client regen noise; confirm no other modules import Contracts broadcast DTOs.

### PR-B3 — Payments security schemes

**Scope:**

- `@useAuth` on `docs-payments.tsp` and/or `payments/routes.tsp`
- `task gen` (likely metadata-only client impact)
- Spot-check Scalar Authorize on Payments product

**Out of scope:** Scope OAuth2 formalization

**Risk:** Low

### PR-B4 — Billing final signed PDF honesty

Pick one:

- **B4-add:** TSP route + docs for final document (redirect semantics)
- **B4-allowlist:** Document in `openapi-vs-minimal-api.md` + allowlist file only

**Risk:** Low (allowlist) / Medium (TSP modeling of redirect)

### PR-B5 — Communications public compliance + legacy-cleanup allowlist pass

**Scope:** docs only (`openapi-vs-minimal-api.md`, optional README notes). No TSP for HTML/Svix unless product asks.

**Risk:** None

### PR-B6 — CI path honesty gate

**Scope:**

- `honesty-allowlist.yaml`
- `scripts/check-openapi-minimal-honesty.mjs` (or ArchitectureTests)
- Wire CI contracts job
- Document in `docs/contracts/openapi-vs-minimal-api.md`

**Depends on:** Prefer after B2/B4 so allowlist is smaller; **can land earlier** with broader allowlist.

**Risk:** Medium (scrape false positives) — start OpenAPI ⊆ Minimal only if needed.

### PR-B7 (optional) — Admin-routes.tsp split

Only if file pain returns (Phase 14 deferred). Not required for honesty.

### Explicit non-goals for Wave B PRs

- Money string/decimal redesign
- Re-add broadcast targeting without fan-out product
- CRM HTTP surface
- Messaging product OpenAPI
- Hand-edit generated clients
- Mega-PR combining dual + broadcast + CI + security

---

## 8. Implementation playbooks (copy-paste checklists)

### 8.1 Dual DTO switch (products / refund)

- [ ] Read local record fields vs TSP model fields
- [ ] Confirm generated type exists in `Lazuar.ApiContracts.cs`
- [ ] Switch endpoint parameter types
- [ ] Cast money `double` → `decimal` for commands
- [ ] Null-coalesce collections / optional tax
- [ ] Delete local records
- [ ] `dotnet build apps/lazuar-api/src/Lazuar.Api/Lazuar.Api.csproj`
- [ ] Run focused Commerce ModuleTests
- [ ] Grep repo for old type names (must be zero)

### 8.2 Add impl-only route to TypeSpec

- [ ] Confirm path + method + auth on Minimal API
- [ ] Add models (snake_case) with `@doc` for quirks (zeros, redirects, HTML)
- [ ] Add operation to correct interface (`@useAuth` already on admin group)
- [ ] `task gen`
- [ ] Commit `api-types-ts` + `Lazuar.ApiContracts.cs` (+ Kiota if touched)
- [ ] Switch endpoint to generated types if local twin existed
- [ ] Remove from allowlist if gate exists
- [ ] Update `openapi-vs-minimal-api.md` residual section

### 8.3 Security schemes for product docs

- [ ] Copy LHDN `@useAuth` pattern to docs-payments / payments routes
- [ ] `pnpm --filter @repo/api-spec build` and inspect `dist/payments/openapi.yaml` for `securitySchemes`
- [ ] `task gen` if main.tsp graph affected
- [ ] Developers hub rebuild path documented

### 8.4 Honesty gate

- [ ] Author allowlist from §3 matrix
- [ ] Implement parser for OpenAPI paths
- [ ] Implement Minimal scrape or host dump
- [ ] Normalize templates
- [ ] Fail CI with clear diff
- [ ] Document runbook in `docs/contracts/openapi-vs-minimal-api.md`

---

## 9. Verification matrix (Wave B exit)

| Check | Command / evidence |
|-------|--------------------|
| Gen clean | `task gen --force` then clean `git status` on client paths |
| API builds | `dotnet build apps/lazuar-api/src/Lazuar.Api/Lazuar.Api.csproj` |
| Dual gone | `rg 'CreateProductRequest|UpdateProductRequest|RecordRefundRequest' apps/lazuar-api` → only command names if any, **no** endpoint local records |
| Broadcast in OpenAPI | OpenAPI paths include `/admin/communications/broadcasts/preview` and `/admin/communications/broadcasts/{id}` |
| Final PDF decided | Either OpenAPI path present **or** allowlist row present |
| Payments security | `dist/payments/openapi.yaml` has `components.securitySchemes` |
| CI | `contracts` job green; honesty script green if shipped |
| ModuleTests | Broadcast\*, product, refund, integration checkout suites green |

---

## 10. Mapping to F09 checklist

| F09 item | Analysis section | Suggested PR |
|----------|------------------|--------------|
| F09.1 Dual DTOs remaining | §2 | PR-B1 |
| F09.2 Billing signed PDF | §3.1 | PR-B4 |
| F09.2 Broadcast preview/status | §3.2 | PR-B2 |
| F09.2 Communications public compliance | §3.3 | PR-B5 |
| F09.2 Payments docs security schemes | §4 | PR-B3 |
| F09.3 CI honesty gate | §6 | PR-B6 |
| F09.4 Exit | §9 | After B1–B6 |

Update `plans/004-maintenance/FUTURE-WORK.md` § FW-6 to **Done** only when exit criteria met; link PRs.

---

## 11. File path index (quick navigation)

### TypeSpec

| Path | Wave B relevance |
|------|------------------|
| `packages/api-spec/main.tsp` | Monolith import graph |
| `packages/api-spec/docs-payments.tsp` | Security schemes |
| `packages/api-spec/docs-commerce.tsp` | Includes communications admin |
| `packages/api-spec/modules/commerce/models/product.tsp` | Product DTOs (already complete) |
| `packages/api-spec/modules/commerce/models/subscriber.tsp` | `RecordRefundRequestDto` |
| `packages/api-spec/modules/commerce/admin-routes.tsp` | Product/refund routes |
| `packages/api-spec/modules/communications/models.tsp` | Add broadcast status/preview |
| `packages/api-spec/modules/communications/admin-routes.tsp` | Add GET broadcast ops |
| `packages/api-spec/modules/billing/routes.tsp` | Optional final PDF |
| `packages/api-spec/modules/payments/routes.tsp` | `@useAuth` |
| `packages/api-spec/modules/messaging/models.tsp` | Stay thin |
| `packages/api-spec/modules/crm/models.tsp` | Models-only keep |

### Backend dual / impl-only

| Path | Role |
|------|------|
| `apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints/ProductEndpoints.cs` | Dual create/update |
| `apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints/TransactionEndpoints.cs` | Dual refund |
| `apps/lazuar-api/Modules/Communications/Infrastructure/Endpoints/BroadcastEndpoints.cs` | Impl GET preview/status |
| `apps/lazuar-api/Modules/Communications/Contracts/BroadcastDtos.cs` | Local edge DTOs to supersede |
| `apps/lazuar-api/Modules/Communications/Infrastructure/Endpoints/PublicComplianceEndpoints.cs` | Allowlist public |
| `apps/lazuar-api/Modules/Communications/Infrastructure/Endpoints/TemplateEndpoints.cs` | legacy-cleanup allowlist |
| `apps/lazuar-api/Modules/Billing/Infrastructure/Endpoints/PublicBillingEndpoints.cs` | Final PDF impl |
| `apps/lazuar-api/Modules/Messaging/Infrastructure/Endpoints.cs` | Allowlist messaging |
| `apps/lazuar-api/Modules/Payments/Infrastructure/IntegrationEndpoints.cs` | Already on ApiTypes (Wave A) |

### Generated / pipeline

| Path | Role |
|------|------|
| `packages/api-types-dotnet/Lazuar.ApiContracts.cs` | C# edge DTOs (`Lazuar.ApiTypes`) |
| `packages/api-types-dotnet/nswag.json` | NSwag config |
| `packages/api-types-ts/src/index.ts` | TS paths + schemas |
| `Taskfile.yml` `gen*` | Orchestration |
| `.github/workflows/ci.yml` `contracts` | Client cleanliness |

### Docs

| Path | Role |
|------|------|
| `docs/contracts/openapi-vs-minimal-api.md` | Allowlist / residual honesty |
| `packages/api-spec/README.md` | Package layout + models-only policy |

---

## 12. Risks and mitigations

| Risk | Mitigation |
|------|------------|
| NSwag property names differ from manual JsonPropertyName DTOs | Compare generated props before deleting Contracts DTOs; fix mapping in one PR |
| Frontend assumes free-form refund body | Keep optional body; `tax_amount` null → 0 |
| Honesty scrape false positives on `{id:guid}` | Normalize constraint suffixes |
| Bundle Wave B into one PR | Use PR-B1…B6 sequence |
| Re-introducing dual DTOs | Review checklist + optional architecture test forbidding local records next to generated twins (later polish) |
| Credits fields confuse product | `@doc` zeros; do not silently drop fields clients might poll |

---

## 13. Suggested commit / PR titles

1. `refactor(commerce): bind product and refund endpoints to Lazuar.ApiTypes (FW-6 dual DTOs)`
2. `feat(api-spec): document broadcast status and cost preview (FW-6 Wave B)`
3. `docs(api-spec): add payments OpenAPI security schemes (FW-6)`
4. `feat(api-spec): add public billing final document route` **or** `docs(contracts): allowlist signed final PDF`
5. `docs(contracts): communications public compliance allowlist honesty`
6. `ci: OpenAPI vs Minimal API path honesty gate (FW-6)`

---

## 14. Done definition (FW-6)

Wave B / FW-6 is **done** when:

1. No known dual edge DTO pairs on Commerce products or refunds (or any other shipping surface discovered during implementation).
2. Broadcast preview + status are either in TypeSpec + generated types **or** explicitly allowlisted with product sign-off to remain internal (default: **in TypeSpec**).
3. Billing final PDF is either in TypeSpec or allowlisted with documented reason.
4. Communications public compliance + messaging + gateway webhooks + legacy-cleanup remain honestly allowlisted.
5. Payments product OpenAPI emits security schemes usable in Scalar.
6. Optional but recommended: CI path honesty gate green with committed allowlist.
7. `task gen` clean; CI contracts green; relevant ModuleTests green.
8. `FUTURE-WORK.md` FW-6 marked Done with date + PR links; F09 checklist checked.

---

*End of TypeSpec Wave B (FW-6) uncondensed analysis. No application code was modified for this document.*
