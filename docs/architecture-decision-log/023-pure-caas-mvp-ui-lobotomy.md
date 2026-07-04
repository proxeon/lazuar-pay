
# ADR 023: Phased Rollout Strategy (Pure CaaS MVP via "UI Lobotomy")

**Date:** July 2026  
**Status:** Accepted  
**Context:** Go-To-Market Strategy, Frontend Architecture, and Time-To-Market (TTM) Optimization

## Context & Problem Statement

In ADR 021, we established that our ultimate competitive moat is **Compliance CaaS**—automating LHDN e-Invoicing at the point of sale. 

However, achieving full B2B tax compliance introduces significant UX complexity (collecting TINs, managing Quotes, explaining Credit Notes to users). Waiting to perfect these enterprise-grade features delays our ability to launch the core product and validate our primary infrastructure (Stripe/Billplz integrations, automated WhatsApp dunning, and digital product delivery). 

Under the philosophy of "Solo Founder Scale," **Time-to-Market and early cash flow validation supersede launching with the ultimate moat.** We need to ship a streamlined, frictionless Checkout-as-a-Service (CaaS) product *today*, while preserving our enterprise tax engine for tomorrow.

## Decision

We are adopting a **Phased Rollout Strategy**. 

For Phase 1 (MVP), we will launch a **Pure CaaS** platform. We will execute a "UI Lobotomy" to completely hide all B2B and LHDN-specific features from the frontend.

### The "UI Lobotomy" Methodology
Instead of deleting the code or ripping out the backend architecture, we will simply sever the UI navigation pathways using the `// [MVP-HIDE]` comment marker.

1. **Tree-Shaking over Deletion:** By commenting out the `React Router` definitions and `Sidebar` links, modern bundlers (Vite/Next.js) will naturally tree-shake the orphaned components out of the production build. It costs zero performance overhead.
2. **Backend "Dark Matter":** We will change **absolutely nothing** in the `.NET` backend or the `TypeSpec` API contracts. The `Billing` module’s double-entry ledger will continue to silently balance the books in the background. The `GenerateAndStoreDocumentCommandHandler` will still silently generate PDF receipts. 

## Implementation Details

We have explicitly hidden the following touchpoints:

**1. Creator Dashboard (`ops-page`)**
*   Removed the entire `Invoicing` module from the Sidebar (Quotes, Tax Invoices, Credit Notes).
*   Removed the `Legal & Billing Profile` settings page.
*   Removed the `Requires Company Name & Tax ID (LHDN B2B)` toggle from the Product Creation/Edit forms.

**2. Buyer Checkout (`portal-page`)**
*   Removed the TIN and Company Name collection fields from the main checkout flow.
*   Blocked access to the Custom Quote checkout route (`/pay/[sessionId]`) by forcing a `notFound()`.
*   Removed the "Download Tax Invoice" button from the secure buyer portal to prevent false legal expectations.

## Consequences

### Positive
*   **Hyper-Focused MVP:** We are selling exactly one thing: High-converting checkout links with automated dunning. It is exceptionally easy to market and minimizes customer support debt.
*   **Zero-Friction Reactivation:** When we are ready to launch Phase 2 (LHDN Compliance), reactivating the features is as simple as removing the `[MVP-HIDE]` comments. No git-reverts, no merge conflicts, no backend rewrites.
*   **Battle-Tested Ledger:** While we validate the checkout UI in the real world, our double-entry ledger backend will be silently recording thousands of real transactions, proving its stability before we expose the UI.

### Trade-offs & Mitigations
*   *Trade-off:* We temporarily lose our primary "Compliance" differentiator, pitting us directly against established checkout builders.
*   *Mitigation:* This is an acceptable, temporary risk. We will compete on the localized combination of Billplz (FPX) + Automated WhatsApp Dunning (which Western competitors lack) until the LHDN moat is activated.
*   *Trade-off:* Unused code ("floating islands") remains in the repository.
*   *Mitigation:* The `[MVP-HIDE]` comment tag ensures developers know exactly why the code is disconnected and how to safely restore it.
