
# Lazuar Platform (Checkout-as-a-Service)

**Focused Pay (this repo’s new cashier)** lives on **8081** — `apps/lazuar-pay`, merchant `:5178`, checkout `:5179`. Identity is sibling **lazuar-one** on **8080**. Hub `lazuar-api` / ops `:3003` / portal `:3004` in this tree is **museum**. Integrators: [`apps/lazuar-pay/README.md`](apps/lazuar-pay/README.md) and [`examples/pay-node`](examples/pay-node). Hub sample `examples/hub-cashier-next` is **not** Pay.

> **A Sovereign Checkout, Billing, and Compliance Engine for Asian Creators and B2B SaaS Founders.**

Lazuar is an API-first, Headless Commerce platform built on a strict .NET 10 Modular Monolith. We provide checkout, subscriptions, a double-entry ledger, email recovery, and Malaysia MyInvois (LHDN) e-invoicing for tenants who configure it.

We actively avoid the "CMS Trap." We don't build website builders, and we don't force you to migrate your domains. You build your beautiful landing pages on Framer, Webflow, Astro, or custom Next.js apps. We simply power the "Buy Now" button.

### Product truth watermark (read before ADR archaeology)

| Layer | Source of truth | Notes |
|-------|-----------------|--------|
| **Shipping product (MVP)** | **ADR 021** (Compliance CaaS pivot) + **ADR 023** (Pure CaaS UI lobotomy) | Ops UI ships checkout/commerce/dunning/credits first; LHDN B2B UX is intentionally unrouted until Phase D.3. Community/Vault modules removed (**ADR 022**). |
| **Historical ambition** | ADR 014 (apps catalog), ADR 020 (integration roadmap) | Useful roadmap context only. Do not implement “15 apps,” community DRM, or link-in-bio from those docs without an explicit reverse of ADR 021/023. |
| **API contracts** | `packages/api-spec` + `task gen` | OpenAPI clients must match Minimal API; see `docs/contracts/openapi-vs-minimal-api.md`. |

**Honest capability today:** BYOK Stripe / Billplz / CHIP / Razorpay / **Xendit** + commerce subscriptions + double-entry billing ledger + **email** dunning + LHDN submit/poll when configured. Billplz / Razorpay / Xendit renewals = emailed hosted link, not silent charge. Official Receipts (`RCPT-`) are payment receipts, **not** MyInvois tax invoices. WhatsApp dunning and Xero/QuickBooks sync are **not** shipping. Xendit is a hosted-invoice wrap (reminder-only). No FPX e-mandate. No proven sandbox MyInvois `VALID`.

---

## 🏗 Headless Architecture

```txt
[ YOUR FRONTEND ]              [ LAZUAR CAAS ENGINE ]               [ COMPLIANCE & FULFILLMENT ]
(Framer, WP, SaaS)             (portal.lazuar.com)                  (Post-Purchase Automation)

  ┌─────────────┐                ┌───────────────────┐                ┌───────────────────┐
  │ Landing Page│                │ 1. BYOK Gateways  │                │ 🏛 LHDN / GSTN    │
  │ UI / Sales  │── Buy Link ───▶│    (Stripe/FPX)   │── Success ───▶ │    (Govt e-Invoice) │
  │ Copy        │                │                   │                │                   │
  └─────────────┘                │ 2. Universal      │                │ 📦 Vault / SaaS   │
                                 │    Ledger         │── Success ───▶ │    (Secure R2 PDF / │
  ┌─────────────┐                │                   │                │     App Webhooks)   │
  │ SaaS Pricing│── Buy Link ───▶│ 3. Automated      │                │                   │
  │ Table       │                │    Compliance     │── Failure ───▶ │ ✉️ Email          │
  └─────────────┘                │                   │                │    (dunning)        │
                                 │ 4. Dunning Engine │                └───────────────────┘
  ┌─────────────┐                │    & Retries      │
  │ Custom      │── API Call ───▶└───────────────────┘
  │ Next.js App │
  └─────────────┘
```

**Key Separation:**
- **`lazuar-ops` (Admin):** The AWS-style superapp. Internal staff use this to configure products, view the financial ledger, construct Dunning campaigns, and manage operations.
- **`lazuar-portal` (Checkout):** The headless cash register. Highly optimized, distraction-free SSR Next.js app that processes transactions and grants access.
- **`lazuar-admin` (Platform):** Global control plane for tenants/workspaces.
- **`lazuar-developers` (API docs):** Scalar OpenAPI hub (local port 3002; prod path `/docs`).

---

## ⚡ Core Principles

### 1. Headless Commerce (Centralized Checkout Engine)
Marketing and presentation layers are fully decoupled from our transactional core. All payment orchestration, double-entry ledgers, and automated dunning are managed centrally inside the `Commerce` and `Billing` cores.

### 2. Bring Your Own Key (BYOK)
We do not act as a Merchant of Record (MoR) and we do not take 8% transaction fees. You plug in your own Stripe, Billplz, or CHIP API keys. Money flows instantly to your merchant accounts. 

### 3. Absolute Financial Truth (Double-Entry Ledger)
To the Ledger, all money looks the same. Whether a customer pays via Stripe, FPX, or Bitcoin, the `Billing` module executes strict double-entry bookkeeping (`Cash` + `Fee` - `Gross Revenue` - `Tax` = `0`). Summary `net_revenue` is that P&L net (not bank cash).

### 4. Prepaid Utility Wallet
Live LHDN MyInvois submissions deduct micro-credits from a prepaid `TenantCreditBalance` wallet. Console/stub WhatsApp is **not** billed. We do not take a GMV cut.

---

## 🚀 Master Integration Roadmap

The platform is designed to replace the fragmented, manual workflows currently crippling Asian creators and B2B founders.

### Phase 1: The "Un-Fireable" Core (Current)
*   **Local Asian Gateways (BYOK):** Stripe, Billplz, CHIP, Razorpay/Curlec, Xendit (hosted invoice). Silent off-session is Stripe/CHIP only.
*   **Government Tax Compliance:** Malaysia MyInvois (LHDN) when the tenant configures keys. India GSTN / Indonesia Coretax are not scheduled.
*   **Official Receipt + tax invoice:** `RCPT-` payment receipts and `INV-` tax invoices are different documents. Receipts are not e-invoices.
*   **Not shipping:** Xero/QuickBooks sync, Meta Cloud WhatsApp dunning, homemade FPX e-mandate.

### Phase 2: High-Ticket & Asset Fulfillment
*   **Escrow for High-Ticket B2B:** Escrow.com API (Eliminates trust friction for 5-figure deals).
*   **Embedded E-Signatures:** PandaDoc / DocuSign API (Merge the legal contract with the payment link).
*   **Community "Bouncer" Bots:** Telegram / Discord APIs (Automated invite and kick based on billing status).
*   **Software Licensing (DRM):** Keygen.sh (Automated license key generation and revocation for developers).

### Phase 3: Borderless Scaling & Operations
*   **Mass Affiliate Payouts:** Wise MassPay / PayPal Payouts (Automate the creator's viral growth engine).
*   **B2B "Buy Now, Pay Later":** Capchase / Funding Societies (Offer 12-month terms to buyers, pay creators upfront).
*   **Bitcoin / Web3 Settlement:** Direct RPCs / BTCPay (Borderless, zero-chargeback crypto checkouts).
*   **National Digital KYC:** Singpass / MyDigital ID (Zero-fraud checkouts and instant B2B tax ID verification).

---

## 📂 Project Structure

```md
.
├── apps/
│   ├── lazuar-api/          # The Brain (.NET Modular Monolith) -> api.lazuar.com
│   ├── lazuar-ops/          # The Back-Office (Vite CSR)        -> ops.lazuar.com
│   ├── lazuar-portal/       # The Cash Register (Next.js SSR)   -> portal.lazuar.com
│   ├── lazuar-admin/        # The Global Control Plane          -> admin.lazuar.com
│   ├── lazuar-developers/   # Scalar OpenAPI hub                -> hub …/docs
│   └── lazuar-docs/         # VitePress product guides
│
├── examples/
│   └── hub-cashier-next/    # Integrator sample (Next.js) — port 3020 (optional)
│
├── packages/
│   ├── api-spec/            # TypeSpec definitions (Single Source of Truth)
│   ├── api-types-dotnet/    # Auto-generated C# Models
│   └── api-types-ts/        # Auto-generated TypeScript Interfaces
│
└── docs/                    # Architecture Decision Logs (ADR) + engineer quickstarts
```

**Monorepo app names:** `lazuar-ops`, `lazuar-portal`, `lazuar-admin`, `lazuar-developers`.  
**Optional sample:** `examples/hub-cashier-next` (port **3020**) — not started by default product turbo; see [`examples/README.md`](examples/README.md) and program notes in [`plans/006-sample/README.md`](plans/006-sample/README.md).
TypeSpec SSoT remains `packages/api-spec`. GHCR images remain `lazuar-hub-*`. Public hub paths unchanged (`/`, `/portal`, `/docs`, `/admin`).

### Module Pattern (Backend)
Every backend module is strictly decoupled to guarantee future microservice-readiness:

```txt
Modules/{ModuleName}/
├── Application/         # Commands, queries, handlers, DTOs
├── Contracts/           # Public interfaces, integration events (The Boundary)
├── Domain/              # Aggregates, entities, value objects, rules
└── Infrastructure/      # EF Core, endpoints, background workers, gateways
```

---

## 💻 Getting Started

Our monorepo utilizes `Taskfile`, `pnpm`, and `docker-compose`.

```bash
# 1. Start local Docker dependencies (PostgreSQL)
task infra:up

# 2. Run database migrations and start the .NET hot-reload API watcher
task dev

# 3. In a new terminal, launch the frontends via mprocs
task fe
```

**Dual-run next to Aura (hop A):** listen on **`:8090`** (Aura owns 8080). Billplz callbacks use **`App:ApiBaseUrl`**, which must be a **public `https://…/api/v1`**.

```sh
task tunnel:cf          # named tunnel: pay-local.lazuar.dev → :8090, aura-local → :8000
task tunnel:cf:url      # prints App__ApiBaseUrl=https://pay-local.lazuar.dev/api/v1
```

`http://localhost:8090` is not hop A — create will fail with `CALLBACK_BASE_NOT_PUBLIC` unless `App:AllowInsecureBillplzCallback=true`. Do not use a `*.lazuar.com` public base for sandbox (that host used to flip production Billplz). `task tunnel:api` is leftover ngrok on standalone `:8080`.

### Local demo accounts

Seeded on first Development boot (`task dev`) from `apps/lazuar-api/src/Lazuar.Api/appsettings.Development.json`. **Local only** — never reuse these in production.

| Role | App | URL | Email | Password |
|------|-----|-----|-------|----------|
| Superadmin | `lazuar-admin` | http://localhost:3005/ | `admin@lazuar.com` | `Password123!` |
| Tenant admin | `lazuar-ops` | http://localhost:3003/ | `founder@acme.test` | `Password123!` |

The tenant workspace slug is **`acme`**. Superadmin can also sign in to ops (system workspace). Override the superadmin seed with `PLATFORM_ADMIN_EMAILS` / `PLATFORM_ADMIN_PASSWORD`.

**Portal** (`lazuar-portal`, http://localhost:3004/) has no password login — buyers open a magic link after checkout.

### Standardized Port Mapping

| App | Port | Access URL | Description |
|-----|------|------------|-------------|
| `lazuar-api` | 8080 | `http://localhost:8080` | .NET 10 Modular Monolith |
| `lazuar-developers` | 3002 | `http://localhost:3002` | Scalar OpenAPI hub |
| `lazuar-ops` | 3003 | `http://localhost:3003` | Superapp Console (Admin) |
| `lazuar-portal` | 3004 | `http://localhost:3004` | Universal Checkout & Dashboard |
| `lazuar-admin` | 3005 | `http://localhost:3005` | Platform Infrastructure Admin |
| `@examples/hub-cashier-next` | **3020** | `http://localhost:3020` | Integrator sample (optional; `pnpm example:cashier`) |
| **Gateway (optional)** | **9080** | `http://localhost:9080` | Local Caddy edge — prod-like paths |

Vite apps pin ports with `strictPort: true` (ops **3003**, admin **3005**). If a port is already in use, dev fails loudly instead of stealing another app’s port — free the port and retry.

#### Local Caddy gateway (`task proxy`)

Optional single origin that mirrors production path routing. Uses **Docker** (`caddy:2-alpine`) — no host Caddy install. See [`deploy/dev/README.md`](deploy/dev/README.md) and [`docker-compose.dev-proxy.yml`](docker-compose.dev-proxy.yml).

| Gateway path | Upstream (host via `host.docker.internal`) |
|--------------|--------------------------------------------|
| `/health`, `/api/*` | API `:8080` |
| `/` | ops `:3003` |
| `/portal*` | portal `:3004` |
| `/docs*` | developers `:3002` |
| `/admin/` | admin `:3005` (`handle_path`) |

```bash
# Docker Desktop must be running (same as task infra:up)
# With API + frontends already on the host:
task proxy          # foreground
task proxy:up       # detached
task proxy:down
task proxy:validate # syntax check via caddy image
```

`task fe` / `mprocs-dev.yaml` sets base-path envs so path routing works:

| App | Via gateway | Direct (with mprocs envs) |
|-----|-------------|---------------------------|
| ops | `http://localhost:9080/` | `http://localhost:3003/` |
| portal | `http://localhost:9080/portal` | `http://localhost:3004/portal` |
| developers | `http://localhost:9080/docs` | `http://localhost:3002/docs` |
| admin | `http://localhost:9080/admin/` | `http://localhost:3005/admin/` |

Running `pnpm dev` **outside** mprocs does **not** set those envs (apps stay at `/` on their ports). See also [`deploy/dev/README.md`](deploy/dev/README.md).

If HMR is flaky through `:9080`, use direct app ports for day-to-day UI work; use the gateway for path smoke tests.

### Type Generation Pipeline
When modifying API endpoints or models, edit the TypeSpec files in `packages/api-spec/` and run:
```bash
task gen
```
This automatically updates both frontend TypeScript definitions and backend C# models.

### Development Context
For AI context passing or searching codebase structures:

```sh
fd -t f --ignore-file ctx.ignore | ctx | hxn
```

```sh
cat ctx.include | ctx | hxn
```
