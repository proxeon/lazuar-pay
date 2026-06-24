
# ADR 015: Avoiding the "CMS Trap" (Headless Checkouts & Separation of Concerns)

**Date:** 2026-06-25
**Status:** Accepted
**Context:** Frontend Architecture & Conversion Rate Optimization (CRO)

## Context & Problem Statement

During the development and live-testing of our funnels and event modules, we observed a counter-intuitive metric: **when we introduced description fields (CMS-like rich text) to our lead generation and checkout pages, conversion rates and revenue dropped.**

Initially, the assumption was that providing more context natively would help users buy. The reality proved the opposite. By adding descriptions to the checkout phase, we accidentally forced the user out of "Execution Mode" and back into "Evaluation Mode." Cognitive load increased, friction was introduced, and users abandoned the transaction. 

Furthermore, from an engineering perspective, building a CMS is an infinite black hole. Supporting rich text (WYSIWYG), image uploads, CDN caching, and custom styling (fonts/colors) inside the deterministic Lazuar core distracts us from building high-leverage financial and operational infrastructure. 

## Decision

We are officially adopting a **"Headless Checkout / Payment Link" Strategy** and banning the development of CMS (Content Management System) features inside the Lazuar core apps.

1. **Separation of Persuasion and Execution:**
   - **The Pitch (Persuasion):** All marketing copy, long-form descriptions, FAQs, and imagery must live on external, static landing pages (e.g., Astro SSG, Webflow, Linktree, Framer). We will route custom creator domains (e.g., `creator.com`) directly to these static edge networks.
   - **The Transaction (Execution):** The Lazuar Next.js/Remix portal (`portal.lazuar.com/[tenantSlug]/...`) will act strictly as the "Cash Register." It will present the data deterministically: What is being bought, the price, and the secure form to capture data/payment.

2. **No WYSIWYG Editors:**
   - We will not implement libraries like TipTap, Quill, or Lexical for public-facing product descriptions.
   - We will not provide asset hosting (S3/CDN) for marketing images.

3. **Unified Checkout URLs:**
   - Creators will link their external static sites to our portal via standard href tags (e.g., `https://portal.lazuar.com/al-quran/checkout/standard-plan`).

## Consequences

### Positive
- **Higher Conversion Rates (Revenue):** Checkouts become "blind." By removing distracting marketing text at the point of sale, friction is minimized, mapping directly to our formula: `Decreased Friction = Increased True Wealth`.
- **Engineering Velocity:** We strip away ~50% of our frontend workload. We save hundreds of hours by not building or maintaining a website builder.
- **Performance:** Checkout pages will load in milliseconds because the `.NET` API no longer has to parse and serialize massive strings of HTML/blobs from the database.
- **Database Hygiene:** The database remains purely deterministic and lightweight.

### Trade-offs & Mitigations
- *Trade-off:* Creators cannot build their marketing pages inside `ops.lazuar.com`. They must use an external tool.
- *Mitigation:* We will provide highly optimized, open-source **Astro templates** that creators can deploy for free on Cloudflare Pages, pre-configured to link directly to their Lazuar portal. 

## Principles Applied
- **Deterministic Core First:** Business-critical operations require strict data entry, not probabilistic/messy content blocks.
- **Solo Founder Scale:** We build Layer 4 applications (Financial engines/Fulfillment). We offload Layer 2 presentation (Marketing pages) to purpose-built external tools.
