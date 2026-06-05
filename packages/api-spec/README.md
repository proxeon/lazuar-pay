
# Lazuar API Specification (TypeSpec)

This package acts as the **Single Source of Truth** for the Lazuar Platform's API contracts. 

We use [TypeSpec](https://typespec.io/) to define our API shapes, which are then compiled down into an OpenAPI 3.0 specification (`dist/openapi.yaml`). From there, our build pipeline automatically generates strictly-typed TypeScript clients (for Next.js/React) and C# DTOs (for the .NET Backend).

## Directory Structure

Our TypeSpec definitions mirror our backend's **Modular Monolith / Domain-Driven Design** architecture.

```text
packages/api-spec/
├── common/                  # Domain-agnostic, shared core types
│   └── models.tsp           # ProblemDetails, StatusResponse, IdResponse
├── modules/                 # Business verticals (matches backend Modules/)
│   ├── auth/                # Auth-specific models and routes
│   ├── community/           # Community-specific models and routes
│   └── messaging/           # Messaging-specific models and routes
│       ├── models.tsp       # DTOs, Requests, Responses
│       └── routes.tsp       # Route interfaces and endpoints
├── main.tsp                 # The Orchestrator (Root configuration & Imports ONLY)
├── package.json
└── tspconfig.yaml
```

---

## 🛠 How to Add a New Type/Endpoint to an Existing Module

If you are adding a new feature to an existing module (e.g., `Community`), follow these steps:

### 1. Define the Model (DTO)
Open the relevant models file, e.g., `modules/community/models.tsp`. 
Ensure you are inside the `LazuarApi.Community` namespace, and define your model.

```typescript
// modules/community/models.tsp
namespace LazuarApi.Community;

model RefundRequestDto {
  subscription_id: string; // GUID
  reason: string;
}
```

### 2. Define the Endpoint
Open the relevant routes file, e.g., `modules/community/admin-routes.tsp`. 
Locate the appropriate `interface` block, and add your operation (`op`).

*Note: Always return standard errors using `LazuarApi.Core.ProblemDetailsResponse`.*

```typescript
// modules/community/admin-routes.tsp
namespace LazuarApi.Community;

@useAuth(BearerAuth)
@route("/admin/community")
interface AdminCommunityOperations {
  // ... existing routes ...

  @post
  @route("/subscribers/{id}/refund")
  processRefund(
    @path id: string,
    @body body: RefundRequestDto
  ): LazuarApi.Core.StatusResponse | LazuarApi.Core.ProblemDetailsResponse;
}
```

### 3. Generate the Code
From the **root of the monorepo**, run the generation pipeline:

```bash
task gen
```
*This will instantly update the C# classes in `@repo/api-types-dotnet` and the TypeScript interfaces in `@repo/api-types-ts`.*

---

## 🚀 How to Add a Completely New Module

If you are building a brand new module in the backend (e.g., `Billing`), you must scaffold it in TypeSpec.

### 1. Create the Folder and Files
Create a new directory inside `modules/` and create two files: `models.tsp` and `routes.tsp`.

```bash
mkdir -p packages/api-spec/modules/billing
touch packages/api-spec/modules/billing/models.tsp
touch packages/api-spec/modules/billing/routes.tsp
```

### 2. Define your Models
In `models.tsp`, declare your namespace and models.

```typescript
// modules/billing/models.tsp
namespace LazuarApi.Billing;

model InvoiceDto {
  id: string;
  amount: float64;
  status: string;
}
```

### 3. Define your Routes
In `routes.tsp`, import your models and the core models. Wrap your endpoints inside an `interface` to group them cleanly in the generated OpenAPI doc.

```typescript
// modules/billing/routes.tsp
import "@typespec/http";
import "../../common/models.tsp";
import "./models.tsp";

using TypeSpec.Http;

namespace LazuarApi.Billing;

@useAuth(BearerAuth)
@route("/admin/billing")
interface AdminBillingOperations {
  @get
  @route("/invoices")
  getInvoices(): InvoiceDto[] | LazuarApi.Core.ProblemDetailsResponse;
}
```

### 4. Register the Module in `main.tsp`
Open `packages/api-spec/main.tsp` and add the import statements for your new module.

```typescript
// main.tsp
// ... existing imports

// 5. Billing Module
import "./modules/billing/models.tsp";
import "./modules/billing/routes.tsp";
```

### 5. Generate and Consume
Run `task gen`. You can now use `InvoiceDto` in your C# Controllers/Endpoints and your React frontend!

---

## 📖 Golden Rules & Best Practices

1. **`main.tsp` is for imports only.** Never define `model`, `op`, or `namespace` logic directly inside `main.tsp`.
2. **Use Interfaces for Routes.** Always wrap your routes in an `interface` block (e.g., `interface AdminOperations { ... }`). This creates clean OpenAPI operation groupings.
3. **Avoid Deep Nesting.** Keep namespaces flat (e.g., `LazuarApi.Community`). Do not nest deeply (e.g., avoid `LazuarApi.Modules.Community.Admin.Responses`).
4. **Standardized Responses.** Rely on `LazuarApi.Core.IdResponse`, `LazuarApi.Core.StatusResponse`, and `LazuarApi.Core.ProblemDetailsResponse` rather than reinventing standard HTTP shapes.
5. **GUIDs are Strings.** TypeSpec does not have a native `Guid` type. Use `string` and document it with a comment `// GUID`. NSwag on the C# side will handle it as a string, which can easily be parsed via `Guid.Parse()`.
