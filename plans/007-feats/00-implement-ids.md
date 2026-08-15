# 00 — Feature IDs to implement

**Date:** 16 August 2026  
**Source:** [00-checklist-tracker.md](./00-checklist-tracker.md)  
**Judgment:** [00-evaluation.md](./00-evaluation.md)

Implement the rows that close money loops and make CaaS + LHDN sellable. Skip anything marked refuse. Treat Wave 4 as wrap-later, not build-now.

Already shipped (`Y` / `W`) is not on this list.

---

## Do implement — Wave 0 (close loops first)

These are partial or missing today and block every honest sale.

| ID | Feature |
|----|---------|
| LP-024 | Success page only after payment truth |
| LP-047 | Honest vault / off-session (Stripe/CHIP vs Billplz reminder-only) |
| LP-052 | Automatic renewal actually runs |
| LP-071 | Failed renewal enters PAST_DUE |
| LP-072 | Off-session AUTO_CHARGE retry |
| LP-073 | Email recovery sequence sends |
| LP-077 | Recovered-revenue metrics |
| LP-078 | Terminal suspend / cancel after dunning |
| LP-079 | Campaign snapshot (don’t mutate in-flight) |
| LP-090 | Inbound webhook verify + business-key idempotency |
| LP-132 | Outbound webhooks that don’t silent-drop |
| LP-133 | Signed delivery + retry + redrive |
| LP-151 | Receipt / failed-pay / magic-link email |
| LP-153 | Template variables actually resolve |

---

## Do implement — Wave 1 (sellable CaaS)

| ID | Feature |
|----|---------|
| LP-004 | Real SaaS fee (not GMV take-rate) |
| LP-005 | Prepaid utility credits (only once LHDN/WhatsApp meter something real) |
| LP-006 | Public signup + pricing page |
| LP-014 | Quantity on checkout |
| LP-020 | BM / EN |
| LP-021 | Mobile-first checkout |
| LP-025 | Checkout branding |
| LP-037 | Apple Pay / Google Pay **via Stripe** (wrap, don’t rebuild) |
| LP-053 | Reminder-only / send-link-each-cycle (Billplz-honest) |
| LP-056 | Cancel at period end |
| LP-065 | Offline / manual payment subscription |
| LP-091 | Full refund |
| LP-092 | Partial refund |
| LP-093 | Refund UI in ops |
| LP-097 | CSV reconciliation export |
| LP-123 | PDPA delete / anonymize (finish the existing path) |
| LP-131 | Scoped API keys |
| LP-135 | Versioned event catalog in docs |
| LP-137 | M2M subscription admin API |
| LP-142 | Idempotency-Key on POST |
| LP-144 | Integration guides (not Scalar dump) |
| LP-154 | Bounce / complaint suppression |
| LP-173 | Portal: update payment method |
| LP-182 | Sandbox + test keys that match live/test |
| LP-183 | Time-to-first-checkout |
| LP-184 | Self-serve workspace create |

---

## Do implement — Wave 2 (the moat)

Backend already exists for most of these. Un-hide and close the UI loop.

| ID | Feature |
|----|---------|
| LP-022 | Company + TIN on checkout |
| LP-101 | Sequential document numbers |
| LP-102 | Quotes / proforma / custom checkout |
| LP-103 | Tax invoice |
| LP-104 | Credit / debit / refund notes |
| LP-106 | Buyer document download |
| LP-107 | PDF branding |
| LP-110 | MyInvois submit |
| LP-111 | VALID / INVALID poll |
| LP-112 | TIN validation |
| LP-113 | LHDN QR on invoice |
| LP-114 | B2C monthly consolidation |
| LP-116 | Cancel / reject per IRBM rules |
| LP-117 | XAdES V1.1 signing (when you have a `.p12`) |
| LP-118 | SST line codes |
| LP-122 | Merchant legal profile |
| LP-175 | Portal invoice / receipt history |

---

## Implement later — Wave 3 (10% of Chargebee)

Do these after Waves 0–2. Prefer “change at next renewal” before full proration if you have to pick one.

| ID | Feature |
|----|---------|
| LP-054 | Free trial |
| LP-057 | Pause / resume as a product action |
| LP-058 | Plan change |
| LP-059 | Proration (or next-renewal-only) |
| LP-060 | Quantity / seats |
| LP-063 | Multiple prices per product |
| LP-076 | Hard vs soft decline |
| LP-094 | Disputes as first-class |
| LP-105 | Payment terms / AR reminders |
| LP-161 | Ledger-based MRR / ARR |
| LP-166 | Staff roles beyond admin |
| LP-167 | Audit log |
| LP-174 | Portal plan change |

**Skip for now even in Wave 3:** LP-015 (order bump), LP-016 (abandoned cart), LP-017 (custom domain), LP-018 (overlay checkout), LP-062 (setup fees). Conversion polish, not the wedge.

---

## Implement later — Wave 4 (wrap rails, don’t become a gateway)

| ID | Feature | Note |
|----|---------|------|
| LP-032 | FPX e-mandate | Via Xendit or Curlec, not homemade |
| LP-044 | Finish Razorpay / Curlec adapter | Same rail |
| LP-045 | Xendit adapter | Best single wrap for SEA + wallets |
| LP-033 | DuitNow QR | Only if Xendit/CHIP expose them |
| LP-034 | Touch ’n Go eWallet | Only if Xendit/CHIP expose them |
| LP-035 | GrabPay | Only if Xendit/CHIP expose them |
| LP-036 | ShopeePay / Boost | Only if Xendit/CHIP expose them |
| LP-074 | WhatsApp recovery sequence | Real Meta Cloud **or delete the claim** |
| LP-155 | WhatsApp via Meta Cloud | Pair with LP-074 |
| LP-121 | Xero sync | After LHDN UI is live |
| LP-100 | Commercial receipt honesty | Tighten with Wave 2 docs |

**Do not schedule unless a paying tenant forces it:** LP-038 PayPal, LP-061 usage billing, LP-064 import, LP-096 FX, LP-115 self-billed, LP-119 export zero-rate, LP-138 SDK, LP-141 test clocks, LP-165 cohorts, LP-181 status page, LP-208 escrow, LP-209 GSTN/Coretax, LP-210 affiliates.

---

## Do not implement

| ID | Why |
|----|-----|
| LP-002 | Merchant of Record |
| LP-003 | Licensed acquirer / holds settlement |
| LP-007 | KYC for acquiring |
| LP-039 | BNPL |
| LP-095 | Settlement / payout reports |
| LP-120 | Global tax engines (wrong XML) |
| LP-156 | SMS |
| LP-157 | Marketing blasts |
| LP-168 | Grow CRM into HubSpot |
| LP-200 | Website / online store builder |
| LP-201 | Link-in-bio / funnel builder |
| LP-202 | POS / tap-to-pay / hardware |
| LP-203 | Marketplace / multi-vendor split |
| LP-204 | Community DRM / Telegram bouncer |
| LP-205 | Course / membership CMS |
| LP-206 | Full accounting / GL replacement |
| LP-207 | Crypto / USDC settlement |

---

## Short version

Implement first:

`LP-024`, `LP-047`, `LP-052`, `LP-071`, `LP-072`, `LP-073`, `LP-077`, `LP-078`, `LP-079`, `LP-090`, `LP-132`, `LP-133`, `LP-151`, `LP-153`

Then Wave 1 (refunds, portal, DX, Billplz-honest renewals).

Then Wave 2:

`LP-022`, `LP-101`, `LP-102`, `LP-103`, `LP-104`, `LP-106`, `LP-107`, `LP-110`, `LP-111`, `LP-112`, `LP-113`, `LP-114`, `LP-116`, `LP-117`, `LP-118`, `LP-122`, `LP-175`

That is the intersection this product can own.
