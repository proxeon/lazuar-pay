
# Lazuar Platform (Checkout-as-a-Service)

> **A Sovereign Checkout, Billing, and Fulfillment Engine for Creators and SaaS Founders.**

Lazuar is an API-first, Headless Commerce platform built on a strict .NET 10 Modular Monolith. We provide the financial infrastructure—multi-gateway orchestration, double-entry ledgers, automated WhatsApp dunning, and LHDN tax e-Invoicing—so you don't have to.

We actively avoid the "CMS Trap" (ADR-015). We don't build website builders, and we don't force you to migrate your domains. You build your beautiful landing pages on Framer, Webflow, WordPress, or custom Next.js apps. We simply power the "Buy Now" button.

---

## 🏗 Headless Architecture

```txt
[ YOUR FRONTEND ]              [ LAZUAR CAAS ENGINE ]               [ THIN FULFILLMENT ]
(Framer, WP, SaaS)             (portal.lazuar.com)                  (Post-Purchase Hooks)

  ┌─────────────┐                ┌───────────────────┐                ┌───────────────────┐
  │ Landing Page│                │ 1. Multi-Gateway  │                │ 📦 Vault          │
  │ UI / Sales  │── Buy Link ───▶│    (Stripe/FPX)   │── Success ───▶ │    (Secure R2 PDF)│
  │ Copy        │                │                   │                │                   │
  └─────────────┘                │ 2. Universal      │                │ 👥 Community      │
                                 │    Ledger         │                │    (Telegram/Zoom)│
  ┌─────────────┐                │                   │                │                   │
  │ Linktree /  │── Buy Link ───▶│ 3. LHDN Tax       │── Success ───▶ │ ⚡ SaaS Webhooks  │
  │ Social Bio  │                │    e-Invoicing    │                │    (App Unlock)   │
  └─────────────┘                │                   │                │                   │
                                 │ 4. WhatsApp       │                │ 🎟️ Event          │
  ┌─────────────┐                │    Dunning & CRM  │                │    (Live Booking) │
  │ Custom      │── API Call ───▶└───────────────────┘                └───────────────────┘
  │ Next.js App │
  └─────────────┘
```

**Key Separation:**
- **`ops-page` (Admin):** The AWS-style superapp. Internal staff and creators use this to configure products, view the financial ledger, and manage Dunning schedules. 
- **`portal-page` (Checkout):** The headless cash register. Highly optimized, distraction-free SSR Next.js app that processes transactions and grants access.

---

## 📜 The Playbook

```txt
True Wealth =
((Value × Ψ) / Friction × Distribution)
× (Code × Math × Automations)
× (Timing × Focus × Retention)
```

**Where:**
- **Decreasing Friction:** Offloading UI to best-in-class tools (Framer) while we provide blazing-fast, 1-click checkout execution.
- **Code × Math × Automations:** The sovereign CaaS infrastructure (BYOK Payments + Double-Entry Ledger + WhatsApp Dunning).

---

## ⚡ Core Principles

### 1. Headless Commerce (Avoiding the CMS Trap)
We refuse to build WYSIWYG editors or page builders. All marketing copy and imagery live on external static edge networks (Astro, Webflow). Lazuar exclusively handles the deterministic financial transaction and fulfillment. 

### 2. Bring Your Own Key (BYOK)
Unlike Merchants of Record (MoRs) like Lemon Squeezy or Paddle, we do not take a 5–8% cut of your revenue, and we don't hold your funds. You plug in your own Stripe, Billplz, or CHIP API keys. Money flows instantly to you.

### 3. Prepaid Utility Wallet
Instead of penalizing your growth with transaction fees, we charge a flat SaaS fee for the checkout core. High-leverage automations (like auto-submitting LHDN XML e-Invoices or sending WhatsApp recovery messages) deduct micro-credits from a prepaid `TenantCreditBalance`.

### 4. Thin Fulfillment Wrappers (The 15 Apps)
Our "15 Apps" are not massive standalone software suites. They are lightweight metadata wrappers that execute *after* the Checkout Engine takes money. A "Vault App" is simply `CaaS + an R2 Download Link`. A "Community App" is `CaaS + a Telegram Redirect`. 

### 5. Developer-First Extensibility
SaaS founders shouldn't spend 3 weeks parsing Stripe and Billplz webhooks. Drop a Lazuar link into your app, and we will send a clean, HMAC-SHA256 signed `Payment_Success` outbound webhook to your server to unlock the user's account.

---

## 📂 Project Structure

```md
apps/
├── lazuar-api/       # The Brain (.NET Modular Monolith) -> api.lazuar.com
│
├── ops-page/         # The Back-Office (Vite CSR)        -> ops.lazuar.com
│
└── portal-page/      # The Cash Register (Next.js SSR)   -> portal.lazuar.com
```

### Module Pattern (Backend)

Every backend module is strictly decoupled:

```txt
Modules/{ModuleName}/
├── Application/         # Commands, queries, handlers, DTOs
├── Contracts/           # Public interfaces, integration events (The Boundary)
├── Domain/              # Aggregates, entities, value objects, rules
└── Infrastructure/      # EF Core, endpoints, background workers, gateways
```

---

## 🚀 Ecosystem Roadmap (Fulfillment Hooks)

Modules are built sequentially. We build the central CaaS Engine, then attach "Hooks" for different business verticals.

| Priority | Fulfillment Hook | Category | Core Function |
|---|---|---|---|
| 1 | **Core CaaS** | Infrastructure | Payments, Ledger, Taxes, Automations |
| 2 | **Webhooks** | Developers | B2B SaaS Account Unlocking |
| 3 | **Community** | Retention | Recurring Dunning, Telegram/Zoom routing |
| 4 | **Vault** | Fulfillment | Secure R2 Digital File Delivery |
| 5 | **Event** | Acquisition | Live Workshop Ticketing & Reminders |
| 6 | **Giveaway** | Acquisition | Viral Lead Generation Engine |
| 7 | **Broadcast** | Retention | Email/WhatsApp Nurture Sequences |
| 8 | **Affiliate** | Distribution | Partner Commission Ledgers |
| 9 | **Consult** | Acquisition | Calendar Booking Integration |

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

### Development Context
For AI context passing or searching codebase structures:

```sh
fd -t f --ignore-file ctx.ignore | ctx | hxn
```

```sh
cat ctx.include | ctx | hxn
```
