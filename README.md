# Lazuar Platform

> **A sovereign operating system for solo founders and creator businesses. An API-first, deterministic AWS-style superapp.**

Lazuar is a modular monolith platform designed to power a multi-product creator business (events, communities, courses, digital products) from a single, deterministic backend. It is built on the principle that business-critical operations require strict data integrity, dense functional UI, and full ownership of the revenue engine.

We build applications, not infrastructure. We buy our payment rails (Stripe) and compute (Hetzner), but we build our own creator workflows because we are the customer.

---

## Architecture Overview

```txt
                     +---------------------------------------+
                     |          OPS-PAGE SUPERAPP            |
                     |  (Deterministic UI + Data Tables)     |
                     +-------------------+-------------------+
                                         |
                                         | API Calls (TypeSpec)
                                         v
       +---------------------------------+---------------------------------+
       |                                 |                                 |
+------v------+                   +------v------+                   +------v------+
| ACQUISITION |                   | FULFILLMENT |                   |  RETENTION  |
|  - Bio      | (Linktree)        |  - Vault    | (Gumroad)         | - Community | (Skool)
|  - Form     | (Typeform)        |  - Funnel   | (ClickFunnels)    | - Broadcast | (Mailchimp)
|  - Event    | (Eventbrite)      |  - Academy  | (Kajabi)          | - Affiliate | (Rewardful)
|  - Consult  | (Calendly)        |  - Invoice  |                   | - Sponsor   | (Passionfroot)
|  - Giveaway | (Kingsumo)        |  - Pipeline |                   | - Support   | 
+-------------+                   +-------------+                   +-------------+
```

**Key Separation:**
- **`ops-page` (Admin):** The AWS-style superapp. Internal staff use this to manage all 15 modules. Simple, functional, fast.
- **Public Pages (`community-page`, `vault-page`, etc.):** Customer-facing checkout and portal pages. Kept separate for clean UX.

---

## Playbook

```txt
True Wealth =
((Value × Ψ) / Friction × Distribution)
× (Code × Math × Automations)
× (Timing × Focus × Retention)

Where:
- (Value × Ψ) / Friction × Distribution = The Conversion Engine
- (Code × Math × Automations) = The Sovereign Infrastructure (Solo Founder Scale)
- (Timing × Focus × Retention) = The Asymmetric Compounders
```

**Formulas:**
$$True\ Wealth = \left( \frac{Value \times \Psi}{Friction \times Distribution} \right) \times (Code \times Math \times Automations) \times (Timing \times Focus \times Retention)$$

$$Distribution = Content \times Cadence \times Channel\ Fit$$

---

## Core Principles

### 1. Founder as Power User
We build the API and Admin Console for ourselves first. We use our own 15 modules to run our education/media business. **We are Default Alive from Day 1** because the business revenue funds the platform, not vice versa.

### 2. API-First, Frontend-Optional
All domain logic lives in clean, decoupled APIs (defined via TypeSpec). The strict REST APIs orchestrate these systems deterministically. External frontends are built only when paying customers demand them, not before.

### 3. The Barbell Strategy
- **Safe Bet (Now):** Use our own modules to generate revenue (Docker bootcamps, communities, digital products).
- **High Upside (Later):** Productize the platform and sell access to external creators and developers.

### 4. Zero-Friction Principles
Avoid VCs, external funding, and grants. Let revenue from our own usage fund the infrastructure.

### 5. Acquisition, Fulfillment, and Retention as First-Class Citizens
Every module must map to a stage of the customer journey. No orphan features.

### 6. The Elon Musk Master Plan (Adapted)
- **Step 1:** Use our own platform to generate revenue (Default Alive).
- **Step 2:** Use revenue to refine the API architecture and data-dense dashboards.
- **Step 3:** Sell frontends to external users who want to replicate our success.
- **Step 4:** Expose APIs/MCP to developers.

### 7. Deterministic Core First (AI in Hibernation)
All business-critical operations (billing, refunds, tax submissions) are executed through **deterministic UI** (buttons, forms, tables). LLMs are probabilistic; they should not execute $1,000 mutations autonomously. 

While the platform possesses an architecture for an AI agent, it is currently in hibernation. The pattern is: LLM proposes action → `UiRequestCard` requests approval → Human clicks → Deterministic API executes.

### 8. AWS-Style Superapp (For Applications)
All admin functionality lives in a single `ops-page` application. Each app follows the same pattern: `Dashboard`, `List`, `Detail`, `Settings`. We build our Layer 4 applications; we buy our Layer 2 infrastructure (Stripe, Cloudflare, Resend). **Beauty is in the backend, not the frontend.**

---

## Project Structure

```md
apps/
├── lazuar-api/       # The Brain (.NET Modular Monolith) -> api.lazuar.com
│
├── ops-page/         # The Back-Office (Vite CSR)        -> ops.lazuar.com
│
├── portal-page/      # The Cash Register (Next.js SSR)   -> portal.lazuar.com
│
└── storefront-page/  # The Window Display (Astro SSG)    -> creator.com / *.lazuar.site
```

```md
.
├── apps
│   ├── community-page/          # Public Next.js app (checkout, portal)
│   ├── lazuar-api/              # .NET 10 Modular Monolith
│   │   ├── BuildingBlocks/      # Shared kernel (CQRS, Outbox, Domain)
│   │   ├── Modules/             # Bounded contexts
│   │   │   ├── Billing/         # Double-entry ledger
│   │   │   ├── CRM/             # Client profiles
│   │   │   ├── Community/       # Subscriptions, plans, coupons
│   │   │   ├── Lhdn/            # Malaysia e-Invoice gateway
│   │   │   ├── Messaging/       # Notifications
│   │   │   ├── One/             # CIAM, workspaces
│   │   │   ├── Ops/             # AI agent orchestration (Hibernating)
│   │   │   └── Payments/        # Gateway adapters
│   │   └── src/Lazuar.Api/      # API entry point
│   └── ops-page/                # Admin superapp (React/Vite)
│       └── src/modules/
│           ├── community/       # Community app pages
│           └── core/            # Shared layout components
├── docs/
│   └── architecture-decision-log/
└── packages/
    └── api-spec/                # TypeSpec API contracts
```

### Module Pattern

Every backend module follows the same structure:

```txt
Modules/{ModuleName}/
├── Application/         # Commands, queries, handlers, DTOs
├── Contracts/           # Public interfaces, integration events
├── Domain/              # Aggregates, entities, value objects, rules
└── Infrastructure/      # EF Core, endpoints, workers, gateways
```

Every frontend app in `ops-page` follows the same structure:

```txt
ops-page/src/modules/{appName}/
├── pages/
│   ├── DashboardPage.tsx
│   ├── ListPage.tsx
│   ├── DetailPage.tsx
│   └── SettingsPage.tsx
└── components/
```

---

## Module Roadmap & Priority

Modules are built sequentially. A module must earn its keep before the next is built.

| Priority | Module | Category | Status |
|---|---|---|---|
| 1 | `Event` | Acquisition | ✅ Validated |
| 2 | `Giveaway` | Acquisition | 🔨 Next |
| 3 | `Vault` | Fulfillment | 📋 Planned |
| 4 | `Community` | Retention | 📋 Planned |
| 4 | `Academy` | Fulfillment | 📋 Planned |
| 5 | `Broadcast` | Retention | 📋 Planned |
| 6 | `Funnel` | Fulfillment | 📋 Planned |
| 7 | `Affiliate` | Retention | 📋 Planned |
| 8 | `Consult` | Acquisition | 📋 Planned |
| 9 | `CRM` | Core | ✅ Exists |
| 10 | `Form` | Acquisition | 📋 Planned |
| 11 | `Bio` | Acquisition | 📋 Planned |
| 12 | `Invoice` | Fulfillment | 📋 Planned |
| 13 | `Pipeline` | Fulfillment | 📋 Planned |
| 14 | `Sponsor` | Retention | 📋 Planned |
| 15 | `Support` | Retention | 📋 Planned |

---

## Standardized Port Mapping

### Behind Caddy Gateway (`:8000`)

1. **`one-admin`** (Vite) → **`3000`**
2. **`one-page`** (Vite) → **`3001`**
3. **`community-admin`** (Vite) → **`3010`**
4. **`community-page`** (Next.js) → **`3011`**

### Standalone Frontend Apps

Some Next.js apps use domain-level routing (no `basePath`) and must be accessed directly on their dev port.

| App | Port | Access URL | Reason |
|-----|------|------------|--------|
| `vault-page` | 3012 | `http://localhost:3012` | Routes are `/{slug}`, no path prefix |
| `event-page` | 3008 | `http://localhost:3008` | Routes are `/{slug}` |
| `funnel-page` | 3015 | `http://localhost:3015` | Routes are `/{slug}` |
| `community-page` | 3020 | `http://localhost:3020` | Routes are `/{slug}`, `/checkout` |
| `consult-page` | 3022 | `http://localhost:3022` | Routes are `/{slug}`, `/checkout` |

In production, each app gets its own Caddyfile on the server:

```text
vault.lazuar.com {
    reverse_proxy vault-page:3000
    handle /api* {
        reverse_proxy api:8080
    }
}
```

---

## The AI Agent Architecture (Hibernating)

The `ops-page` includes an AI agent architecture designed to act as a probabilistic assistant over the deterministic API backend. This system is currently decoupled and hibernating to prioritize the deterministic console.

**Key Components:**
- `OpsChatWorkspace.tsx` — Chat interface
- `ToolRegistry.cs` — Backend tool definitions
- `UiRequestCard.tsx` — Bridge between agent suggestion and deterministic UI
- `FormRegistry.ts` — Dynamic form rendering triggered by agent

---

## Getting Started

Open `http://localhost:8000` after running the dev environment.

```bash
# Start the development environment
task dev
```


### Development Context

```sh
fd -t f --ignore-file ctx.ignore | ctx | hxn
```

```sh
cat ctx.include | ctx | hxn
```
