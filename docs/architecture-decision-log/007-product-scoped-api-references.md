
# ADR 007: Product-Scoped API References (Developer Hub Segmentation)

**Status:** Accepted  
**Date:** June 2026  

## Context

As the Lazuar platform scales into a Modular Monolith with multiple distinct products (One, Community, Vault, Funnel, etc.), generating a single, massive `openapi.yaml` file creates significant Developer Experience (DX) issues:

1. **Audience Mismatch:** A developer integrating automated checkout flows (Community) does not need to see global ecosystem provisioning endpoints (One). Mixing them creates confusion and security concerns.
2. **UI Bloat:** A single Swagger or Scalar page containing hundreds of endpoints results in an unnavigable "Big Ball of Mud" sidebar.
3. **Domain Leakage:** A single monolithic API reference implies a monolithic architecture, obscuring the strict bounded contexts we enforce in the C# backend.

## Decision

We treat our API Documentation as a product. We will utilize **Product-Scoped API References**. 

Instead of rendering one global API page, we generate distinct OpenAPI artifacts for each business domain and serve them on isolated routes within our `developers-page` Next.js application (e.g., `developers.lazuar.com/one`, `developers.lazuar.com/community`).

## Implementation Guide: Adding a New Module to the Developer Hub

When introducing a new module (e.g., `Vault`) to the Lazuar platform, follow this strict 4-step checklist to expose its API documentation.

### Step 1: Create a Dedicated TypeSpec Entry Point
Do not use `main.tsp` for documentation generation (`main.tsp` is strictly for generating the internal C# DTOs and TS interfaces). Instead, create a product-specific entry point in the `packages/api-spec/` root.

**File:** `packages/api-spec/docs-vault.tsp`
```tsp
import "@typespec/http";
import "@typespec/openapi";
import "@typespec/openapi3";

import "./common/models.tsp";
import "./modules/vault/models.tsp";
import "./modules/vault/admin-routes.tsp";
import "./modules/vault/public-routes.tsp";

using TypeSpec.Http;

@service({
  title: "Lazuar Vault API",
  description: "Secure digital asset delivery, file streaming, and access logs."
})
@server("http://localhost:8080/api/v1", "Local development server")
namespace LazuarApi;
```

### Step 2: Update the TypeSpec Build Script
Update the `package.json` inside `packages/api-spec` so that running `task gen` compiles this new `.tsp` file into its own isolated subfolder.

**File:** `packages/api-spec/package.json`
```json
"scripts": {
  "build": "npx tsp compile main.tsp --output-dir dist && npx tsp compile docs-one.tsp --output-dir dist/one && npx tsp compile docs-community.tsp --output-dir dist/community && npx tsp compile docs-vault.tsp --output-dir dist/vault"
}
```
*Run `task gen` after this step to generate the `dist/vault/openapi.yaml` file.*

### Step 3: Create the Next.js Scalar Route
In the `developers-page` Next.js app, create an API route that reads the newly generated YAML file and renders the highly-optimized Scalar HTML engine.

**File:** `apps/developers-page/app/vault/route.ts`
```tsx
import fs from "fs";
import path from "path";
import { ApiReference } from "@scalar/nextjs-api-reference";

const specPath = path.join(process.cwd(), "../../packages/api-spec/dist/vault/openapi.yaml");
const openapiSpec = fs.readFileSync(specPath, "utf8");

export const GET = ApiReference({
  spec: {
    content: openapiSpec,
  },
  theme: "default",
  hideDownloadButton: true,
  metaData: {
    title: "Lazuar Vault API",
  },
});
```

### Step 4: Add to the Developer Hub Landing Page
Finally, link the new API reference route on the Developer Hub homepage so external developers can discover it.

**File:** `apps/developers-page/app/page.tsx`
```tsx
{/* Vault Link */}
<Link href="/vault" className="group flex flex-col bg-white border border-[#e5e5e5] p-6 transition-all hover:border-[#09090b] hover:shadow-[4px_4px_0px_0px_rgba(0,0,0,0.1)]">
  <div className="flex items-center justify-between mb-4">
    <h2 className="font-bold uppercase tracking-widest text-[12px]">Vault Module</h2>
    <span className="text-[10px] bg-[#f4f4f5] text-[#71717a] px-2 py-1 font-mono">v1.0.0</span>
  </div>
  <p className="text-[#71717a] text-[13px] leading-relaxed mb-6 flex-1">
    Secure digital asset delivery, file streaming, and access logs.
  </p>
  <span className="text-[11px] font-bold tracking-widest uppercase text-[#09090b] group-hover:underline">
    View Reference →
  </span>
</Link>
```

By following this pattern, the Lazuar API documentation remains strictly modular, infinitely scalable, and perfectly tailored to the developer's specific integration context.
