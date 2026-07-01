
# ADR 021: Pivot to "Compliance CaaS" (The 3-Pillar Strategy)

**Date:** July 2026  
**Status:** Accepted  
**Context:** Product Strategy, Market Positioning, and Core System Architecture  

## Context & Problem Statement

In our pursuit to build a "Super App" for creators, we planned to build 15 distinct applications (Course hosting, Link-in-bio, Viral Giveaways, etc.). However, analyzing the market revealed a critical trap: building marketing and presentation software is a "Vitamin." It suffers from low barriers to entry (AI web generators), infinite feature creep (the CMS trap), and high customer churn.

Conversely, Southeast Asia (specifically Malaysia via LHDN) is undergoing a massive regulatory shift: **Mandatory Government e-Invoicing.** 
* Global payment processors (Stripe) do not generate local XML tax invoices.
* Local payment gateways (Billplz) are "dumb pipes" that do not handle compliance logic or subscription dunning.
* Accounting software (Xero) handles compliance, but does not own the checkout button.

There is a massive, highly profitable gap in the market: **Compliance at the Point of Sale.**

## Decision

We are explicitly abandoning the "Jack of all trades" feature factory. **Lazuar is exclusively a Compliance-First Checkout Engine (Compliance CaaS).**

Our entire product roadmap, marketing, and engineering effort is now restricted to owning the transaction and automatically filing the associated government taxes. If a feature does not directly facilitate a transaction or keep a business legally compliant, we will not build it.

## The 3-Pillar Implementation Strategy

We will dominate the market by solving compliance for the three distinct types of transactions:

### Pillar 1: Low-Ticket + Government Tax (The B2C Engine)
*   **The Scenario:** High-volume sales of ebooks, cheap subscriptions, or low-tier digital assets (RM50 - RM200).
*   **The Pain:** Generating thousands of individual LHDN invoices is impossible and clogs the tax authority's API. LHDN legally requires B2C sales to be consolidated at the end of the month.
*   **The Lazuar Implementation:** We provide a high-converting, localized checkout (Billplz/FPX). The `.NET` Billing module (`B2cConsolidationJob.cs`) silently batches all B2C transactions into the double-entry ledger. On the 28th of every month, it automatically generates and submits a single `ConsolidatedInvoice` XML to LHDN. The merchant does zero manual data entry.

### Pillar 2: High-Ticket + Government Tax (The B2B Engine)
*   **The Scenario:** Low-volume, high-trust enterprise sales, masterminds, or consulting retainers (RM5,000 - RM50,000).
*   **The Pain:** Corporate buyers demand strict Tax Identification Number (TIN) validation and an immediate, LHDN-validated Tax Invoice so they can claim business expenses. Furthermore, buyers hesitate to pay $10k via a standard checkout link without a contract or escrow.
*   **The Lazuar Implementation:** We merge legal and financial workflows. The checkout flow requires TIN validation against the LHDN API *before* payment. We offer Escrow and E-Signatures at checkout. Upon clearance, Lazuar instantly generates a mathematically perfect UBL 2.1 XML invoice, signs it, submits it, and returns the official LHDN QR code to the corporate buyer. 

### Pillar 3: Cross-Border + Government Tax (The Global Engine)
*   **The Scenario:** Selling digital goods globally (US, SG, UK) from a Southeast Asian base.
*   **The Pain:** High FX fees (Stripe cross-border fees) and complex "Export" tax compliance (e.g., Zero-Rated supplies under LHDN).
*   **The Lazuar Implementation:** Integration of borderless payment rails (USDC/Web3 RPCs or global card gateways). When a foreign transaction clears, Lazuar automatically classifies the ledger entry as an export, applying the correct zero-rated tax codes for government submission, ensuring the merchant remains globally competitive but locally compliant.

## The "Kill / Delay" List

To maintain "Solo Founder Scale" and execute these 3 pillars flawlessly, we are officially killing or delaying the following "Vitamin" features:

*   **🚫 Kill: Viral Giveaways & Lead Gen Forms.** We are not marketing software. Let them use KingSumo.
*   **🚫 Kill: Community DRM (Telegram/Discord Bouncers).** We are not an automation platform. Let them use Zapier.
*   **🚫 Kill: Website / Link-in-Bio Builders.** We are headless. Let them use Framer or Astro.
*   **✅ Keep: WhatsApp Dunning (Auto-retries).** A failed payment means no tax submission and lost revenue. Dunning protects the core transaction engine.
*   **✅ Keep: Xero / Cloud Accounting Sync.** This completes the CFO's compliance loop.

## Consequences

### Positive
*   **The Ultimate Moat:** Writing W3C Canonicalized XML, managing X.509 cryptography, and balancing double-entry ledgers is incredibly difficult. AI wrappers and offshore dev teams cannot easily clone this.
*   **Negative Churn:** Lazuar becomes "un-fireable." If a business cancels Lazuar, their cash flow stops, and they immediately violate government tax laws.
*   **High B2B Pricing Power:** We are not selling a $15/mo scheduling tool. We are replacing a $2,000/mo manual accounting and data-entry department. 

### Trade-offs & Mitigations
*   *Trade-off:* We lose the casual, beginner creator market who just wants a simple "Buy me a coffee" link without setting up tax profiles.
*   *Mitigation:* We explicitly accept this. Our Ideal Customer Profile (ICP) is the professional digital business, agency, or high-volume creator who feels the acute pain of operational and regulatory scaling.
