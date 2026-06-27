
# ADR 019: Pivot to Checkout-as-a-Service (CaaS) & Headless Commerce

**Date:** June 2026  
**Status:** Accepted  
**Context:** Product Strategy, Market Positioning, and Core System Architecture  

## Context & Problem Statement

Initially, the Lazuar platform was conceptualized as an "All-in-One" suite of 15 standalone B2C applications (Link-in-bio, Funnel Builder, Course Platform, etc.). However, during the implementation phase, two critical realities emerged:

1. **The UI/UX Exhaustion Trap:** Building 15 full-blown frontend applications puts us in direct competition with highly funded, best-in-class design tools (Framer, Webflow, Linktree, Kajabi). Building dragging-and-dropping UI interfaces is a low-leverage, infinite maintenance trap that violates "Solo Founder Scale."
2. **The True "Bleeding Neck" Problem:** Creators and Indie Developers do not struggle to build websites. They struggle with the **financial and fulfillment backend**. Specifically:
   * **Gateway Fragmentation:** Stripe is terrible for local Southeast Asian bank transfers (FPX). Billplz/CHIP are great for local payments but lack robust developer webhooks and subscription dunning (failed payment recovery).
   * **Tax Compliance (LHDN):** In Malaysia, e-Invoicing (UBL 2.1 XML) is becoming legally mandatory. No standard website builder does this natively. 
   * **Automated Fulfillment:** Native payment links (Stripe/Billplz) leave buyers on a generic "Thank You" page. They do not auto-generate secure portals, trigger WhatsApp welcome messages, or dispatch outbound webhooks to unlock SaaS products.

## Decision

We are officially pivoting the platform's core identity. **Lazuar is no longer a collection of 15 website builders; it is a Sovereign Checkout & Fulfillment Engine (Checkout-as-a-Service).**

We will act as an **Infrastructure Primitive**—the "Headless Commerce" backend for the internet. Creators and developers will design their landing pages on their preferred platforms (WordPress, Framer, Astro, custom Next.js apps) and simply link their "Buy Now" buttons to our deterministic checkout URLs.

## Implementation Principles

### 1. The "Thin App" Fulfillment Model
We will not abandon the 15 use-cases, but we will redefine them. Inside the `Commerce` module, an "App" is merely a **Thin Fulfillment Wrapper** applied *after* a successful checkout.
* **Vault App:** `Checkout Link` + `Cloudflare R2 Secure URL`.
* **Community App:** `Checkout Link` + `Telegram/Zoom Redirect` + `Recurring Dunning Engine`.
* **Developer SaaS:** `Checkout Link` + `Outbound Webhook Dispatch`.

### 2. Bring Your Own Key (BYOK) over Merchant of Record (MoR)
Unlike Lemon Squeezy or Paddle (which act as MoRs, taking 5-8% of revenue and holding funds), Lazuar operates strictly as BYOK software.
* Creators plug in their own Stripe/Billplz/CHIP API keys.
* Money flows instantly to the creator.
* Lazuar assumes zero financial liability for chargebacks or fraud.

### 3. The LHDN & WhatsApp Utility Moat
Because we do not take a percentage of transactions, we monetize the infrastructure through a **Prepaid Utility Wallet** (`TenantCreditBalance`). 
* The core checkout software is sold as a flat SaaS fee.
* High-value automated actions—such as submitting an XML e-Invoice to LHDN, or dispatching an automated WhatsApp Dunning message to save a failing subscription—deduct micro-credits from the tenant's wallet. This generates massive margins without penalizing the creator's gross sales volume.

### 4. Developer-First Extensibility
We will treat Indie Hackers and SaaS developers as a primary audience. By listening to internal `GatewayPaymentCompletedIntegrationEvent` messages, our `OutboundWebhookDispatcherJob` will fire HMAC-SHA256 signed payloads to external URLs, saving developers weeks of billing backend engineering.

## Consequences

### Positive
* **Infinite Scalability (Frontend Agnostic):** We tap into the entire internet's traffic without forcing users to migrate their domains or redesign their websites. 
* **High Leverage Engineering:** The engineering team focuses 100% on the deterministic backend (C#, SQL, XML cryptography, Ledger math), which perfectly aligns with our playbook: `(Code × Math × Automations)`.
* **Incredible B2B Moat:** The automated handling of local FPX payments combined with LHDN government tax submissions creates a legal and financial shield that global competitors (Stripe/Paddle) cannot easily replicate in our region.

### Trade-offs & Mitigations
* *Trade-off:* We lose the "All-in-One" marketing angle for non-technical users who genuinely want a website builder.
* *Mitigation:* We will provide open-source, pre-built Astro/Next.js "Storefront Templates" that users can deploy to Cloudflare Pages for free, pre-configured to link to their Lazuar CaaS URLs. We will also rely heavily on documentation and tutorials showing how to embed Lazuar links into Linktree and Framer.
