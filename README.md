
# Lazuar Platform

```txt

                     +---------------------------------------+
                     |             COGNITIVE CORE            |
                     |  Your Custom AI-Chat (via OpenRouter)  |
                     +-------------------+-------------------+
                                         |
                                         | Orfestrates / Optimizes
                                         v
       +---------------------------------+---------------------------------+
       |                                 |                                 |
+------v------+                   +------v------+                   +------v------+
| ACQUISITION |                   | FULFILLMENT |                   |  RETENTION  |
|  - Funnel   |                   |  - Vault    |                   | - Community |
|  - Event    |                   |  - Academy  |                   | - Broadcast |
|  - Consult  |                   |             |                   | - Affiliate |
+-------------+                   +-------------+                   +-------------+
```

## Playbook

```txt
True Wealth =
((Value × Ψ) / Friction × Distribution)
× (Code × Math)
× (Timing × Leverage × Retention)

Where:
- (Value × Ψ) / Friction × Distribution = The Conversion Engine
- (Code × Math) = The Sovereign Infrastructure
- (Timing × Leverage × Retention) = The Asymmetric Compounders
```
* Distribution = Content × Cadence × Channel Fit
* Treat `Acquisition, Fulfillment, and Retention` as first-class citizens
* Avoid VCs, external funding, and grants
* “Default Dead” vs. “Default Alive”
* Optimize teams with a code-first approach
* Follow zero-friction principles
* Follow `Elon Musk's Secret Master Plan`
* Follow `Barbell Strategy`

Open here `http://localhost:8000`.

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

