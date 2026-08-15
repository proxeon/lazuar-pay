# 007 — Competitor features vs Lazuar Pay

Identify **who we compete with** (Malaysia / SEA and global), **which of their features we lack**, and **what to implement later** — without condensing the research.

This folder is the living tracker. Implementation happens later, in separate programs. Do not treat a row as a commitment to ship.

Written 16 August 2026. Twenty subagents; full text kept (do not condense `01`–`20`).

**Product in scope:** this repo — Checkout-as-a-Service / Compliance CaaS, public host `hub.lazuar.com`.

## How to read

| File | What it is |
|------|------------|
| [00-evaluation.md](./00-evaluation.md) | Parent evaluation — who we compete with, what to implement, what to refuse |
| [00-checklist-tracker.md](./00-checklist-tracker.md) | Full feature × competitor checklist (rows × columns) |
| [00-implement-ids.md](./00-implement-ids.md) | Recommended IDs to implement (and refuse), by wave |
| [01–20](./01-lazuar-feature-inventory.md) | Uncondensed subagent analyses — do not summarize them here |

## Subagent analyses (full text)

| # | File | Scope |
|---|------|--------|
| 01 | [01-lazuar-feature-inventory.md](./01-lazuar-feature-inventory.md) | Lazuar Pay ground truth from this repo |
| 02 | [02-local-sea-competitor-landscape.md](./02-local-sea-competitor-landscape.md) | Malaysia / SEA competitors |
| 03 | [03-global-competitor-landscape.md](./03-global-competitor-landscape.md) | Global market map |
| 04 | [04-stripe.md](./04-stripe.md) | Stripe as rail and rival |
| 05 | [05-malaysia-gateways.md](./05-malaysia-gateways.md) | Billplz, CHIP, ToyyibPay, SenangPay, Fiuu, iPay88, GHL, Curlec |
| 06 | [06-sea-fintech-platforms.md](./06-sea-fintech-platforms.md) | Xendit, HitPay, Midtrans, PayMongo, 2C2P, Airwallex |
| 07 | [07-merchant-of-record.md](./07-merchant-of-record.md) | Paddle, Lemon Squeezy, Polar, FastSpring |
| 08 | [08-subscription-billing-engines.md](./08-subscription-billing-engines.md) | Chargebee, Recurly, Maxio, Lago, Stripe Billing |
| 09 | [09-checkout-and-payment-links.md](./09-checkout-and-payment-links.md) | Hosted checkout / payment-link UX |
| 10 | [10-lhdn-einvoice-competitors.md](./10-lhdn-einvoice-competitors.md) | MyInvois, StoreHub, AutoCount, Xero, tax APIs |
| 11 | [11-subscriptions-lifecycle.md](./11-subscriptions-lifecycle.md) | Plans, renewals, proration, portal |
| 12 | [12-dunning-and-recovery.md](./12-dunning-and-recovery.md) | Failed-payment recovery |
| 13 | [13-payments-refunds-rails.md](./13-payments-refunds-rails.md) | Rails, refunds, disputes, webhooks |
| 14 | [14-developer-dx-api-webhooks.md](./14-developer-dx-api-webhooks.md) | Keys, docs, SDKs, outbound webhooks |
| 15 | [15-invoicing-quotes-receipts.md](./15-invoicing-quotes-receipts.md) | Quotes, tax invoices, receipts |
| 16 | [16-communications-whatsapp-email.md](./16-communications-whatsapp-email.md) | Email / WhatsApp honesty |
| 17 | [17-merchant-dashboard-analytics.md](./17-merchant-dashboard-analytics.md) | Ops dashboard, MRR, CRM |
| 18 | [18-pricing-onboarding-trust.md](./18-pricing-onboarding-trust.md) | Pricing, KYC, PDPA, signup |
| 19 | [19-refuse-list-and-adjacents.md](./19-refuse-list-and-adjacents.md) | What we will not copy |
| 20 | [20-sequencing-and-tracker-schema.md](./20-sequencing-and-tracker-schema.md) | Waves + tracker schema |

## Standing constraints (do not contradict)

- Lazuar Pay is **BYOK software**, not a Merchant of Record and not an acquiring bank.
- Buyer money on Billplz / Stripe / CHIP (tenant keys) is not Lazuar's SaaS fee.
- Do not sell WhatsApp dunning or LHDN e-invoice as live product until those loops are closed and (for LHDN) un-hidden.
- Do not become a website builder, marketplace, POS, or ERP to “match competitors.”
- Wrap rails (Stripe, Billplz, CHIP, later Xendit) — do not rebuild acquiring.
- Aura (salon) is a **customer** of Hub, not a competitor. System A (Paddle SaaS) and System B (Hub guest money) stay separate.
