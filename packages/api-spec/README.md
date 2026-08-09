# Lazuar API Specification (TypeSpec)

This package is the **single source of truth** for Lazuar Platform API contracts.

We use [TypeSpec](https://typespec.io/) to define API shapes, compile them to OpenAPI 3.0 (`dist/openapi.yaml` and product-scoped `dist/*/openapi.yaml`), then generate TypeScript clients and C# DTOs for the monorepo.

> **Historical note:** Community and Vault modules were removed (ADR 022). Do not re-add `modules/community` or `modules/vault`. Checkout, subscriptions, and fulfillment live under **Commerce** (+ Communications for templates).

---

## Directory structure

Definitions mirror the backend modular monolith (`apps/lazuar-api/Modules/`).

```text
packages/api-spec/
├── common/
│   └── models.tsp           # ProblemDetails, StatusResponse, IdResponse, auth schemes
├── modules/                 # Business verticals
│   ├── one/
│   │   ├── models.tsp       # Barrel → models/{auth,workspace,webhook,storage,api-keys,provision}.tsp
│   │   ├── models/          # Subdomain DTOs (same namespace LazuarApi.One)
│   │   └── routes.tsp       # Single OneOperations interface (stable tags/operationIds)
│   ├── commerce/
│   │   ├── models.tsp       # Barrel → models/{product,checkout,portal,dunning,...}.tsp
│   │   ├── models/          # Subdomain DTOs (same namespace LazuarApi.Commerce)
│   │   ├── admin-routes.tsp
│   │   └── public-routes.tsp
│   ├── payments/            # Gateway cashier / integration checkouts
│   ├── billing/             # Ledger / financial truth
│   ├── lhdn/                # Malaysian e-invoicing
│   ├── ops/                 # Internal operator console
│   ├── communications/      # Templates, broadcasts (admin)
│   ├── crm/                 # Client profile models (models-only; see below)
│   ├── messaging/           # Intentionally thin (models-only; no routes)
│   └── platform/            # Cross-cutting platform routes
├── main.tsp                 # Full monorepo OpenAPI orchestrator (imports only)
├── docs-one.tsp             # Product-scoped docs (ADR 007)
├── docs-ops.tsp
├── docs-billing.tsp
├── docs-lhdn.tsp
├── docs-commerce.tsp
├── docs-payments.tsp
├── package.json
└── tspconfig.yaml
```

### Live modules (current)

| Module | Routes | Notes |
| :--- | :--- | :--- |
| **one** | barrel `models.tsp` + `routes.tsp` | Global identity & workspaces |
| **commerce** | barrel `models.tsp` + `admin-routes` / `public-routes` | Pure CaaS surface |
| **payments** | `models.tsp`, `routes.tsp` | M2M cashier / webhooks |
| **billing** | `models.tsp`, `routes.tsp` | Ledger & net-profit APIs |
| **lhdn** | `models.tsp`, `routes.tsp` | e-Invoice compliance |
| **ops** | `models.tsp`, `routes.tsp` | Internal ops console |
| **communications** | `models.tsp`, `admin-routes.tsp` | Templates & broadcasts |
| **crm** | `models.tsp` only | **Intentional orphans** — see below |
| **messaging** | `models.tsp` only | **Intentionally thin** — see below |
| **platform** | `routes.tsp` | Platform-level routes |

**Removed (do not restore):** `modules/auth`, `modules/community`, `modules/vault`.

---

## Models-only / intentional thin modules

| Module | Status | Why it stays |
| :--- | :--- | :--- |
| **CRM** | Models in monolith OpenAPI; **no TypeSpec routes** | Backend CRM is command/query only (no public HTTP). DTOs are consumed via generated `Lazuar.ApiTypes` (`ClientProfileDto`, create/update, `BillingAddressDto`) by CRM + Commerce tests/handlers. **Do not delete** without replacing the shared C# contracts. Product-scoped `docs-*.tsp` do **not** import CRM. |
| **Messaging** | Empty namespace + ownership note | Dispatch/outbox module; templates live under Communications. No public OpenAPI surface. Keep the file so `main.tsp` can import the module without phantom DTOs. |

### Orphans resolved (phase 14)

| Model | Resolution |
| :--- | :--- |
| `Core.LinkedCheckoutDto` | **Removed** — never referenced by routes or other models |
| `Commerce.PaymentRecordDto` | **Removed** — never used by any commerce route |

When adding models, prefer wiring them to a route in the same PR, or document them here if they are deliberately models-only.

---

## Product-scoped docs (`docs-*.tsp`) — ADR 007

`main.tsp` builds the full combined OpenAPI used for monorepo codegen.

Each `docs-*.tsp` file is a **product-scoped** entrypoint for the Developer Hub / Scalar (smaller surfaces, clearer titles/auth docs):

| Entrypoint | Output | Audience |
| :--- | :--- | :--- |
| `docs-one.tsp` | `dist/one/` | Platform core identity |
| `docs-ops.tsp` | `dist/ops/` | Internal operators |
| `docs-billing.tsp` | `dist/billing/` | Billing ledger |
| `docs-lhdn.tsp` | `dist/lhdn/` | e-Invoice integrators |
| `docs-commerce.tsp` | `dist/commerce/` | CaaS public + console |
| `docs-payments.tsp` | `dist/payments/` | Payments M2M cashier |

See `docs/architecture-decision-log/007-product-scoped-api-references.md`.

---

## Generate code

From the **monorepo root**:

```bash
task gen
```

Or TypeSpec-only:

```bash
pnpm --filter @repo/api-spec build
```

That runs `tsp compile` for `main.tsp` and all `docs-*.tsp` entrypoints (see `package.json` `build` script). Downstream tasks regenerate C# (`@repo/api-types-dotnet`) and TypeScript clients as configured in the root Taskfile.

---

## How to add a type/endpoint to an existing module

Example: extend **Commerce**.

### 1. Define the model

```typescript
// modules/commerce/models/<subdomain>.tsp  (or append to the nearest subdomain file)
namespace LazuarApi.Commerce;

model RefundRequestDto {
  subscription_id: string; // GUID
  reason: string;
}
```

If you add a new subdomain file, import it from `modules/commerce/models.tsp` (the barrel).

### 2. Define the endpoint

```typescript
// modules/commerce/admin-routes.tsp
namespace LazuarApi.Commerce;

@useAuth(BearerAuth)
@route("/admin/commerce")
interface AdminCommerceOperations {
  @post
  @route("/subscriptions/{id}/refund")
  processRefund(
    @path id: string,
    @body body: RefundRequestDto
  ): LazuarApi.Core.StatusResponse | LazuarApi.Core.ProblemDetailsResponse;
}
```

Return standard errors via `LazuarApi.Core.ProblemDetailsResponse`.

### 3. Generate

```bash
task gen
```

---

## How to add a new module

### 1. Scaffold files

```bash
mkdir -p packages/api-spec/modules/example
touch packages/api-spec/modules/example/models.tsp
touch packages/api-spec/modules/example/routes.tsp
```

### 2. Models

```typescript
// modules/example/models.tsp
namespace LazuarApi.Example;

model ExampleDto {
  id: string; // GUID
  name: string;
}
```

### 3. Routes

```typescript
// modules/example/routes.tsp
import "@typespec/http";
import "../../common/models.tsp";
import "./models.tsp";

using TypeSpec.Http;

namespace LazuarApi.Example;

@useAuth(BearerAuth)
@route("/admin/example")
interface AdminExampleOperations {
  @get
  @route("/")
  list(): ExampleDto[] | LazuarApi.Core.ProblemDetailsResponse;
}
```

### 4. Register in `main.tsp`

```typescript
import "./modules/example/models.tsp";
import "./modules/example/routes.tsp";
```

Optionally add a `docs-example.tsp` product entry and wire it into `package.json` `build` if the surface should appear in the Developer Hub.

### 5. Generate and consume

```bash
task gen
```

---

## Golden rules

1. **`main.tsp` is for imports + service metadata only.** Do not define domain models or ops there.
2. **Use interfaces for routes.** Group operations in an `interface` for clean OpenAPI tags.
3. **Keep namespaces flat.** Prefer `LazuarApi.Commerce` over deep nesting.
4. **Standardized responses.** Prefer `IdResponse`, `StatusResponse`, and `ProblemDetailsResponse` from `LazuarApi.Core`.
5. **GUIDs are strings.** Document with `// GUID`; parse on the C# side as needed.
6. **No Community/Vault.** Those modules and schemas are gone (ADR 022). Use Commerce / Communications / Payments instead.
