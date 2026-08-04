
# Lazuar Platform (Checkout-as-a-Service)

> **A Sovereign Checkout, Billing, and Compliance Engine for Asian Creators and B2B SaaS Founders.**

Lazuar is an API-first, Headless Commerce platform built on a strict .NET 10 Modular Monolith. We provide the enterprise-grade financial infrastructure—multi-gateway orchestration, double-entry ledgers, automated WhatsApp dunning, and LHDN tax e-Invoicing—so you don't have to.

We actively avoid the "CMS Trap." We don't build website builders, and we don't force you to migrate your domains. You build your beautiful landing pages on Framer, Webflow, Astro, or custom Next.js apps. We simply power the "Buy Now" button.

### Product truth watermark (read before ADR archaeology)

| Layer | Source of truth | Notes |
|-------|-----------------|--------|
| **Shipping product (MVP)** | **ADR 021** (Compliance CaaS pivot) + **ADR 023** (Pure CaaS UI lobotomy) | Ops UI ships checkout/commerce/dunning/credits first; LHDN B2B UX is intentionally unrouted until Phase D.3. Community/Vault modules removed (**ADR 022**). |
| **Historical ambition** | ADR 014 (apps catalog), ADR 020 (integration roadmap) | Useful roadmap context only. Do not implement “15 apps,” community DRM, or link-in-bio from those docs without an explicit reverse of ADR 021/023. |
| **API contracts** | `packages/api-spec` + `task gen` | OpenAPI clients must match Minimal API; see `docs/contracts/openapi-vs-minimal-api.md`. |

**Honest capability today:** BYOK gateways + commerce subscriptions + double-entry billing ledger + email dunning templates + LHDN **backend** pipeline. WhatsApp dunning and full compliance UI are roadmap (Phase D), not guaranteed demoable surfaces on every deploy.

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
  │ Table       │                │    Compliance     │── Failure ───▶ │ 💬 WhatsApp       │
  └─────────────┘                │                   │                │    (Smart Dunning)  │
                                 │ 4. Dunning Engine │                └───────────────────┘
  ┌─────────────┐                │    & Retries      │
  │ Custom      │── API Call ───▶└───────────────────┘
  │ Next.js App │
  └─────────────┘
```

**Key Separation:**
- **`ops-page` (Admin):** The AWS-style superapp. Internal staff use this to configure products, view the financial ledger, construct Dunning campaigns, and manage operations. 
- **`portal-page` (Checkout):** The headless cash register. Highly optimized, distraction-free SSR Next.js app that processes transactions and grants access.

---

## ⚡ Core Principles

### 1. Headless Commerce (Centralized Checkout Engine)
Marketing and presentation layers are fully decoupled from our transactional core. All payment orchestration, double-entry ledgers, and automated dunning are managed centrally inside the `Commerce` and `Billing` cores.

### 2. Bring Your Own Key (BYOK)
We do not act as a Merchant of Record (MoR) and we do not take 8% transaction fees. You plug in your own Stripe, Billplz, or CHIP API keys. Money flows instantly to your merchant accounts. 

### 3. Absolute Financial Truth (Double-Entry Ledger)
To the Ledger, all money looks the same. Whether a customer pays via Stripe, FPX, or Bitcoin, the `Billing` module executes strict double-entry bookkeeping (`Cash` + `Fee` - `Gross Revenue` - `Tax` = `0`). This isolates exact gateway fees and tax liabilities, giving founders a true "Net Cash in Bank" metric.

### 4. Prepaid Utility Wallet
Automated compliance tasks (LHDN XML e-Invoicing) and retention actions (WhatsApp dunning messages) deduct micro-credits from a prepaid `TenantCreditBalance` wallet. This allows Lazuar to monetize infrastructure usage heavily without taxing the creator's gross sales volume.

---

## 🚀 Master Integration Roadmap

The platform is designed to replace the fragmented, manual workflows currently crippling Asian creators and B2B founders.

### Phase 1: The "Un-Fireable" Core (Current)
*   **Local Asian Gateways (BYOK):** Billplz, Fiuu, CHIP, Xendit, Razorpay (Zero-fee, localized checkouts).
*   **Government Tax Compliance:** Malaysia LHDN, India GSTN, Indonesia Coretax (Automated legal survival).
*   **Global Cloud Accounting Sync:** Xero, QuickBooks (Internal CFO integration).
*   **Native WhatsApp Dunning:** Meta Cloud API (Automated revenue recovery engine via chat).

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
│   ├── lazuar-api/       # The Brain (.NET Modular Monolith) -> api.lazuar.com
│   ├── ops-page/         # The Back-Office (Vite CSR)        -> ops.lazuar.com
│   ├── portal-page/      # The Cash Register (Next.js SSR)   -> portal.lazuar.com
│   └── superadmin-page/  # The Global Control Plane          -> admin.lazuar.com
│
├── packages/
│   ├── api-spec/         # TypeSpec definitions (Single Source of Truth)
│   ├── api-types-dotnet/ # Auto-generated C# Models
│   └── api-types-ts/     # Auto-generated TypeScript Interfaces
│
└── docs/                 # Architecture Decision Logs (ADR)
```

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

### Standardized Port Mapping

| App | Port | Access URL | Description |
|-----|------|------------|-------------|
| `lazuar-api` | 8080 | `http://localhost:8080` | .NET 10 Modular Monolith |
| `ops-page` | 3003 | `http://localhost:3003` | Superapp Console (Admin) |
| `portal-page`| 3004 | `http://localhost:3004` | Universal Checkout & Dashboard |
| `superadmin` | 3005 | `http://localhost:3005` | Platform Infrastructure Admin |

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
