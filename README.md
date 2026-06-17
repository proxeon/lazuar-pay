
# Lazuar Platform

```txt
                     +---------------------------------------+
                     |             COGNITIVE CORE            |
                     |          Your Custom AI-Agent         |  <-- Used internally by Founder
                     +-------------------+-------------------+                                         
                                         | Orchestrates / Optimizes via API
                                         v
       +---------------------------------+---------------------------------+
       |                                 |                                 |
+------v------+                   +------v------+                   +------v------+
| ACQUISITION |                   | FULFILLMENT |                   |  RETENTION  |
|  - Bio      | (Linktree)        |  - Vault    | (Gumroad)         | - Community | (Skool)
|  - Form     | (Typeform)        |  - Editorial| (Substack)        | - Broadcast | (Mailchimp)
|  - Funnel   | (ClickFunnels)    |  - Academy  | (Kajabi)          | - Affiliate | (Rewardful)
|  - Event    | (Eventbrite)      |  - Invoice  |                   | - Sponsor   | (Passionfroot)
|  - Consult  | (Calendly)        |  - Pipeline |                   | - Support   | 
+-------------+                   +-------------+                   +-------------+
```


## Playbook

```txt
True Wealth =
((Value × Ψ) / Friction × Distribution)
× (Code × Math × AI Leverage)
× (Timing × Focus × Retention)

Where:
- (Value × Ψ) / Friction × Distribution = The Conversion Engine
- (Code × Math × AI Leverage) = The Sovereign Infrastructure (Solo Founder Scale)
- (Timing × Focus × Retention) = The Asymmetric Compounders
```

### Core Principles
1. **Founder as Power User:** We build the API and AI-agent (Cognitive Core) for ourselves first. We use our own 15 modules to run our education/media business. We are Default Alive from Day 1.
2. **API-First, Frontend-Optional:** All domain logic lives in clean, decoupled APIs. The AI agent orchestrates these APIs for us. External frontends are built only when paying customers demand them, not before.
3. **The Barbell Strategy:** 
   - Safe Bet (Now): Use our own modules to generate revenue (Docker bootcamps, communities, digital products).
   - High Upside (Later): Productize the platform and sell access to external creators and developers.
4. **Zero-Friction Principles:** Avoid VCs, external funding, and grants. Let revenue from our own usage fund the infrastructure.
5. **Treat `Acquisition, Fulfillment, and Retention` as first-class citizens:** Every module must map to a stage of the customer journey.
6. **The Elon Musk Master Plan (Adapted):** 
   - Step 1: Use our own platform to generate revenue (Default Alive).
   - Step 2: Use revenue to refine the API/AI-agent architecture.
   - Step 3: Sell frontends to external users who want to replicate our success.
   - Step 4: Expose APIs/MCP to developers.


---


> Open here `http://localhost:8000`

### Standardized Port Mapping (3000 to 3011)
1. **`one-admin`** (Vite) -> **`3000`**
2. **`one-page`** (Vite) -> **`3001`**
3. **`community-admin`** (Vite) -> **`3010`**
4. **`community-page`** (Next.js) -> **`3011`**

### Standalone Frontend Apps (Not Behind Caddy)

Some Next.js apps use domain-level routing (no `basePath`) and must be accessed directly on their dev port — **not** through the Caddy gateway at `:8000`.

| App | Port | Access URL | Reason |
|-----|------|-----------|--------|
| `vault-page` | 3012 | `http://localhost:3012` | Routes are `/{slug}`, `/download/{token}`, `/legal/*` — no path prefix |
| `event-page` | 3008 | `http://localhost:3008` | Routes are `/{slug}`, `/{slug}/receipt/{id}` — no path prefix |
| `funnel-page` | 3015 | `http://localhost:3015` | Routes are `/{slug}` — no path prefix |
| `community-page` | 3020 | `http://localhost:3020` | Routes are `/{slug}`, `/{slug}/checkout` — no path prefix |
| `consult-page` | 3022 | `http://localhost:3022` | Routes are `/{slug}`, `/{slug}/checkout`, `/{slug}/success` — no path prefix |

These apps are designed to run on their own subdomain in production (e.g., `vault.lazuar.com`, `event.lazuar.com`, `community.lazuar.com`, `consult.lazuar.com`). Adding them behind Caddy with a path prefix would require `basePath` configuration in Next.js, which would break all internal routing and link generation.

**In production**, each app gets its own Caddyfile on the server:

```text
vault.lazuar.com {
    reverse_proxy vault-page:3000
    handle /api* {
        reverse_proxy api:8080
    }
}
```

