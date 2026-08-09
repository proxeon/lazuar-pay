
# ADR 017: Portal Frontend Architecture (Vertical Slice Design)

**Date:** 2026-06-25
**Status:** Accepted
**Context:** Frontend Codebase Organization (`portal-page`)

> **Path note (plan 002, 2026-08):** `apps/portal-page` → **`apps/lazuar-portal`**.
> Package / filter: `lazuar-portal`. GHCR image remains `lazuar-hub-portal`.
> Public path still `/portal`. Read every `apps/portal-page/...` as `apps/lazuar-portal/...`.

## Context & Problem Statement

As established in ADR 016, we are consolidating 15 public-facing applications (Community, Vault, Events, etc.) into a single transactional routing engine: `portal.lazuar.com` (built with Next.js or Remix). 

If we organize this unified frontend using traditional horizontal layers (grouping all components together, all hooks together, all API calls together), the codebase will quickly devolve into a "spaghetti" monolith. Navigating between the logic for 15 distinct apps will become impossible, significantly slowing down engineering velocity.

To maintain "Solo Founder Scale," the frontend architecture must mirror the strict discipline and boundaries of our `.NET` Modular Monolith.

## Decision

We are adopting a **Vertical Slice Architecture (Module-Driven Design)** for the `portal-page` codebase. 

The repository will be strictly divided into two distinct domains:
1. **The Routing Layer (`/app`):** Strictly responsible for URL resolution, layout nesting, and server-side data fetching.
2. **The Module Layer (`/modules`):** Strictly responsible for domain-specific UI, state, and API client interactions.

### Standardized Directory Structure

```text
apps/portal-page/
├── src/
│   ├── app/                                 <-- ROUTING & FETCHING LAYER
│   │   ├── (auth)/
│   │   │   └── login/page.tsx               <-- Magic Link entry
│   │   └── [tenantSlug]/                    <-- TENANT BOUNDARY
│   │       ├── layout.tsx                   <-- Fetches Tenant Theme/Colors
│   │       ├── page.tsx                     <-- Unified Buyer Dashboard
│   │       │
│   │       ├── community/                   <-- MODULE ROUTE
│   │       │   └── [resourceSlug]/
│   │       │       ├── layout.tsx           <-- Community Sidebar
│   │       │       ├── page.tsx             <-- Fulfillment / Feed
│   │       │       └── checkout/
│   │       │           ├── layout.tsx       <-- Blank layout (Focus mode)
│   │       │           └── page.tsx         <-- Transaction UI
│   │       │
│   │       └── vault/                       <-- MODULE ROUTE
│   │           └── [resourceSlug]/
│   │               ├── page.tsx             
│   │               └── checkout/page.tsx
│   │
│   └── modules/                             <-- VERTICAL SLICES (UI & Logic)
│       ├── core/                            <-- SHARED KERNEL
│       │   ├── components/                  <-- Shadcn UI (Buttons, Inputs)
│       │   ├── lib/                         <-- Base API client setup
│       │   └── hooks/                       <-- use-mobile.ts
│       │
│       ├── checkout/                        <-- SHARED DOMAIN
│       │   ├── components/                  <-- OrderSummary.tsx
│       │   └── stripe/                      <-- StripeElementsWrapper.tsx
│       │
│       ├── community/                       <-- BOUNDED CONTEXT
│       │   ├── components/                  <-- CommunityFeed.tsx
│       │   └── lib/                         <-- api.ts (TypeSpec wrappers)
│       │
│       └── vault/                           <-- BOUNDED CONTEXT
│           └── components/                  <-- FileDownloadCard.tsx
```

## The 3 Golden Rules of Portal Development

To prevent architectural degradation over time, all development must adhere to these three rules:

### Rule 1: The App Router is strictly for Routing & Data Fetching
`page.tsx` files must contain **almost no UI logic**. They act as Backend-For-Frontend (BFF) controllers. Their sole responsibility is to:
1. Read URL parameters (`tenantSlug`, `resourceSlug`).
2. Await deterministic data from the `.NET` API via TypeSpec clients.
3. Pass that data as props to a UI component imported from the `/modules` directory.

### Rule 2: Strict Module Boundaries (No Cross-Contamination)
A vertical slice in the frontend (`src/modules/community`) must map 1:1 with a module in the `.NET` backend (`BuildingBlocks/Modules/Community`) and the TypeSpec contracts (`packages/api-spec/modules/community`).
- `community` components **cannot** import `vault` components.
- If a component or function is shared across multiple apps (e.g., Checkout form, generic buttons), it must be promoted to `modules/core/` or `modules/checkout/`.

### Rule 3: Layout Isolation for Transactions
Checkout pages require a completely different psychological environment (no distractions) than fulfillment pages (which require navigation and sidebars).
- We will leverage Next.js Layouts to strip away standard navigation headers and sidebars whenever a user is inside a `/checkout/` subpath. This creates a "Blind Checkout" experience, mapping to ADR 015 (Reducing UX friction to increase True Wealth).

## Code Implementation Example

**1. The Data Fetching (Server Component):**
`src/app/[tenantSlug]/community/[resourceSlug]/page.tsx`
```tsx
import { getCommunityData } from '@/modules/community/lib/api';
import { CommunityFeed } from '@/modules/community/components/CommunityFeed';

export default async function CommunityPortalPage({ params }) {
  // Await deterministic state from the .NET core
  const data = await getCommunityData(params.tenantSlug, params.resourceSlug);

  // Delegate entirely to the Vertical Slice UI
  return <CommunityFeed data={data} />;
}
```

## Consequences

### Positive
- **Predictable Scaling:** Adding App #16 simply requires creating `app/[tenantSlug]/app16` and `modules/app16`. The rest of the platform remains untouched and safe.
- **API Alignment:** The frontend perfectly mirrors the backend modularity. A single feature ticket spans exactly one horizontal slice from Database -> `.NET` -> TypeSpec -> Frontend Module.
- **Reusability:** The Checkout engine is built exactly once in `modules/checkout` and utilized across all 15 applications.

### Trade-offs
- *Trade-off:* Developers must be disciplined enough not to put complex UI JSX directly into `page.tsx` files.
- *Mitigation:* We will enforce this via PR reviews and potentially custom ESLint rules to prevent importing sub-components directly into the `app/` router directory.
