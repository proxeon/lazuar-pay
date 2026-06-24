
# ADR 016: Three-Tier Domain & Routing Strategy (Platform & Gateway Pattern)

**Date:** 2026-06-25
**Status:** Accepted
**Context:** Infrastructure, DNS, Reverse Proxy Management, and URL Architecture

## Context & Problem Statement

Previously, our routing strategy was "App-Centric". We attempted to host each of the 15 modules on its own subdomain (e.g., `vault.lazuar.com`, `community.lazuar.com`) with its own reverse proxy configuration and Docker container. 

Furthermore, we naively attempted to map creator custom domains (e.g., `creator.com`) directly to our dynamic SSR applications. 

This created several severe bottlenecks for Solo Founder Scale:
1. **Infrastructure Bloat:** Every new module required changes to our Caddyfile/AWS routing, SSL generation, and CI/CD pipelines.
2. **The Wildcard SSL Trap:** Managing dynamic custom domains directly on our Next.js/Remix servers required complex wildcard SSL handshakes and dynamic host-header parsing, causing unnecessary DevOps overhead.
3. **Namespace Collisions:** If a creator had an ebook and a community both named "Al-Quran", flat URL paths caused database lookup collisions.
4. **Fragmented Buyer UX:** A buyer who bought a course and joined a community had to navigate across different subdomains, rather than having a single, unified creator dashboard.

## Decision

We are adopting a **Three-Tier "Platform & Gateway" Domain Strategy**. We will physically separate the static marketing layer from the dynamic transactional layer. 

Routing will be **Creator-Centric (Tenant First)** and follow a strict, RESTful hierarchy across all 15 modules.

### The Domain Mapping

1. **The Core API (.NET):**
   - `api.lazuar.com` -> Maps strictly to the backend. No frontend rendering.
   
2. **The Superapp Console (Vite CSR):**
   - `ops.lazuar.com` -> The AWS-style internal admin dashboard for creators and staff. Behind a login, zero SEO requirements.

3. **The Transactional Portal (Next.js/Remix SSR):**
   - `portal.lazuar.com` -> The single, unified cash register and fulfillment hub for all 15 apps.
   - **Strict Path Schema:** `portal.lazuar.com/[tenantSlug]/[moduleName]/[resourceSlug]/[action]`
   - *Example Checkout (Community):* `portal.lazuar.com/akmal/community/al-quran/checkout?plan=monthly`
   - *Example Checkout (Vault):* `portal.lazuar.com/akmal/vault/al-quran/checkout`
   - *Example Fulfillment:* `portal.lazuar.com/akmal/community/al-quran` (Accessed after successful checkout)

4. **The Creator Gateway (Astro SSG / External CDN):**
   - `creator.com` or `[tenantSlug].lazuar.site` -> Hosted entirely on Edge CDNs (e.g., Cloudflare Pages). These serve the static marketing pages and Linktrees, resolving to "Buy" buttons that link to the specific `portal.lazuar.com` checkout paths.

## Consequences

### Positive
- **Drastically Simplified DevOps:** Our reverse proxy (Caddy/Nginx) configuration shrinks from 15+ complex blocks down to just three fixed blocks (`api`, `ops`, `portal`). Adding a 16th module requires zero infrastructure changes.
- **Elimination of Namespace Collisions:** By explicitly declaring the `[moduleName]` in the URL, the `.NET` backend always knows exactly which table/context to query, even if the creator uses the same `[resourceSlug]` across different modules.
- **Native Frontend Routing Alignment:** This URL structure maps flawlessly to modern file-system routers (Next.js App Router or Remix), allowing for clean layout nesting (e.g., stripping away sidebars specifically for the `/checkout` leaf nodes).
- **Unified Buyer Dashboard:** Buyers have a single login and a single URL (`portal.lazuar.com/[tenantSlug]`) to access all products, communities, and events from a specific creator.
- **The Barbell Risk Strategy:** Viral traffic spikes only hit the Cloudflare Edge network (Astro static pages). Our Next.js servers and `.NET` database only process high-intent buyers.

### Trade-offs & Mitigations
- *Trade-off:* Pricing variants (e.g., monthly vs. yearly) add complexity to the URL structure.
- *Mitigation:* We will rely on query parameters (`?plan=monthly`) for checkout variants rather than adding another URL segment. The resource being purchased remains the same; the query parameter merely sets the default Stripe Price ID on the checkout UI.

## Code Implication (Caddyfile Example)
The infrastructure layer is permanently locked and simplified:

```text
ops.lazuar.com {
    reverse_proxy ops-page:3000
}

portal.lazuar.com {
    reverse_proxy portal-page:3000
}

api.lazuar.com {
    reverse_proxy api:8080
}
```
