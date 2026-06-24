
# ADR 018: Marketplace Architecture & Structured Content Strategy

**Date:** 2026-06-25
**Status:** Accepted
**Context:** Future-proofing for Network Effects, SEO Aggregation, and Content Management

## Context & Problem Statement

The long-term vision of the Lazuar Platform involves transitioning from a pure SaaS tool to a **Network** (a marketplace aggregating the products, events, and communities of our creators). 

However, building a marketplace introduces a conflict with **ADR 015 (Avoiding the CMS Trap)**. Marketplaces are inherently content discovery engines requiring product descriptions, thumbnails, and value propositions. If we allow creators to design their marketplace listings natively, we risk falling back into the trap of building a WYSIWYG website builder, bloating our database, and introducing friction to the engineering cycle.

We need a strategy to build a massive SEO-driven discovery engine while maintaining "Solo Founder Scale" and keeping our core transactional engine (`portal-page`) completely blind and deterministic.

## Decision

We will implement the Marketplace using a **Strict Metadata Aggregation Model**, completely isolating it from the transactional checkout flow.

### 1. Domain & Codebase Strategy
The marketplace is a platform-wide discovery engine, not a tenant-specific application. 
- **Domain:** It will live on our root domain under a subpath, e.g., `lazuar.com/discover` or `marketplace.lazuar.com`.
- **Codebase:** It will be built entirely inside the **`storefront-page` (Astro)** codebase using SSR/Hybrid mode. Astro is optimized for SEO, content catalogs, and read-heavy/execution-light workloads.

### 2. The Structured Content Model (The "Anti-CMS")
We will **not** build a rich-text CMS for the marketplace. Content will be treated as structured metadata.
When a creator chooses to "Publish to Marketplace" from their `ops-page` (Vite) dashboard, they must adhere to a rigid, standardized form:
- **Product Hunt Model:** Thumbnail image, Title (60 chars), Elevator Pitch (160 chars), and Tags.
- **Gumroad Model (Optional Detail Page):** If a detail page is required on the marketplace, the description will be accepted **strictly in Markdown format**. No custom fonts, colors, or HTML will be supported. The marketplace controls the presentation; the creator only provides the raw text.

### 3. The Buyer Journey (Separation of Discovery and Execution)
Even with the marketplace, the transaction flow remains heavily guarded.
1. **Discovery:** Buyer searches Google, lands on `lazuar.com/discover/al-quran` (Astro). They read the strict markdown pitch.
2. **Handoff:** They click "Buy Now".
3. **Execution:** They are routed to `portal.lazuar.com/akmal/community/al-quran/checkout` (Next.js/Remix). The markdown content *does not carry over*. The checkout remains "blind," lightning-fast, and entirely frictionless.

### 4. Backend CQRS Projection
Querying highly-secure, isolated tenant tables across 10,000 creators to generate a marketplace feed will cripple database performance. 
- When a creator publishes a product, `.NET` will publish an event (e.g., `ProductPublishedToMarketplaceIntegrationEvent`).
- A background worker will project this metadata into a flat, read-optimized "Global Catalog" table (or search index like Typesense) specifically for Astro to query.

## Consequences

### Positive
- **SEO Dominance:** By utilizing Astro for the marketplace, we build a highly performant, indexable catalog capable of capturing massive organic search traffic.
- **Engineering Protection:** By enforcing Markdown/Strict Forms, we avoid the hundreds of hours required to build and maintain a visual page builder inside our platform.
- **Checkout Integrity:** The Next.js/Remix `portal-page` remains purely transactional and blazing fast.
- **Barbell Strategy:** We execute the "Come for the tool, stay for the network" playbook. We do not need the marketplace to survive today (Phase 1), but the architecture seamlessly supports turning on the network effect (Phase 2) without rewriting any frontends.

### Trade-offs & Mitigations
- *Trade-off:* Creators lose the ability to heavily brand their marketplace listing.
- *Mitigation:* This is an intended feature to maintain high buyer trust (uniformity across the marketplace). If a creator wants total visual control, they are encouraged to build their own Astro/Webflow landing page (`creator.com`) and link directly to our `portal-page` checkout.

## Implementation Map

```text
[CREATOR] -> ops-page (Vite) -> Enters Markdown / Metadata
                                      |
                                      v
[BACKEND] -> .NET API -> Projects to Global Read Model
                                      |
                                      v
[BUYER]   <- storefront-page (Astro) <- Reads Global Catalog (SEO)
                                      |
                                      v (Clicks Buy)
[BUYER]   -> portal-page (SSR) -> Executes Transaction (Blind Checkout)
```
