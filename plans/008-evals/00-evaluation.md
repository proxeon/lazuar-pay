# 00 — Parent evaluation: Lazuar Pay after Waves 0–4

**Date:** 16 August 2026  
**Branch:** `feat/007-waves-1-4-implement` (`4624070`)  
**This file is the parent judgment.** The ten reports `01`–`10` are the uncondensed evidence. Do not treat this file as a substitute for those reports.

`plans/007-feats` is the August 16 competitor inventory and implement-ID program. It is **archaeology**. Several tracker cells and the 007 parent eval are stale versus this tree. Re-check code, or read 008, before staffing a 007 backlog item.

---

## 1. Verdict

Lazuar Pay is a **real Compliance CaaS engine**. It is no longer a scaffold with a hidden LHDN backend.

It is sellable as a **closed beta for a solo merchant**:

> Hosted checkout on the merchant’s own Billplz / Stripe / CHIP keys. Subscriptions that either auto-debit a saved card (Stripe/CHIP) or email a pay link (Billplz). Email recovery. Official Receipts that are not e-invoices. Scoped API keys and signed webhooks.

It is **not** sellable as Chargebee, HitPay, Stripe Billing, or “we file MyInvois.”

The August 16 parent eval said only the first pillar was honest and LHDN had no merchant nav. That is outdated. Invoicing is remounted. The remaining problem is not “missing features.” It is **trust**: a few money/security bugs from shipping Waves 3–4 fast, and documents that still describe the old product.

---

## 2. What we actually are

BYOK headless checkout + subscription state machine + double-entry ledger + email dunning + commercial paper + a MyInvois **pipeline**. Money settles on the tenant’s processor. We sell software and (when priced) Hub SaaS + LHDN credits. We do not acquire. We are not Merchant of Record.

Aura is a Hub customer. Do not mix salon booking into this product.

| App | Job after Waves 0–4 |
|-----|---------------------|
| `lazuar-ops` | Merchant console: products, subscribers, refunds, disputes, quotes, sales documents, legal profile, Team, Audit, MRR |
| `lazuar-portal` | Hosted checkout, `/pay/{id}` quotes, magic-link portal, plan change, documents |
| `lazuar-admin` | Platform control plane (gateway vault, superadmin) |
| `lazuar-developers` | Scalar OpenAPI (schema, not SSoT) |
| `lazuar-docs` | VitePress integrator guides (events.md is the outbound catalog) |
| `lazuar-api` | .NET 10 modular monolith, nine modules |

`[MVP-HIDE]` remains only on **ops chat**. ADR 023’s invoicing lobotomy is reversed in the routes. The ADR text was not updated.

---

## 3. Evidence map

| Report | Slice | Lines | One-line take |
|--------|-------|------:|---------------|
| [01](./01-commerce-subscriptions-checkout.md) | Commerce | 775 | Real lifecycle. Pause reclaim and public GUID are P0. Trial cannot be canceled. |
| [02](./02-payments-adapters-rails.md) | Payments | 751 | Five adapters. Auto-debit is Stripe/CHIP only. CHIP EventId collision. Xendit UI inoperable. |
| [03](./03-ledger-refunds-disputes-credits.md) | Money | 910 | Operator refunds work. Disputes are booked as refunds. AmountMyr=0. |
| [04](./04-lhdn-invoicing-documents.md) | Invoicing | 1021 | Paper is demoable. VALID is not proven. Quote B2B identity is wrong. |
| [05](./05-identity-roles-keys-audit.md) | One | 1026 | Signup + keys are sellable. Team invite has no accept page. |
| [06](./06-communications-email-whatsapp.md) | Mail | 798 | Resend is live and gates checkout. WhatsApp is a stub. Email amounts ignore seats. |
| [07](./07-ops-portal-admin-frontend.md) | UI | 822 | Merchant can click the new surfaces. Viewer chrome lies. Xendit has no fields. |
| [08](./08-contracts-webhooks-dx.md) | DX | 742 | VitePress catalog is honest. OpenAPI vs Minimal gate fails (160 vs 149). |
| [09](./09-architecture-tenancy-tests.md) | Arch | 1075 | Boundaries hold. 993 tests. Inbox mostly idle. Pause can starve the billing batch. |
| [10](./10-honesty-risks-next.md) | Cross-cut | 1009 | Sales script, do-not-demo list, ranked P0s, next ten actions. |

---

## 4. What is honestly sellable

Say these sentences and the code will back you:

1. **0% GMV.** Tenant BYOK. Hub fee is a separate platform checkout and is currently **unpriced** (`Saas:Plan:AmountMyr = 0`).
2. **Two collection modes.** Stripe/CHIP = off-session card. Billplz/Razorpay/Xendit/offline = emailed hosted bill. Billplz cannot vault.
3. **Subscriptions.** Statuses PENDING / TRIALING / ACTIVE / PAST_DUE / SUSPENDED / CANCELED. Cancel at period end exists. Plan/seat change is **next renewal, RM 0 today**.
4. **Email recovery.** Campaign builder, snapshots, AUTO_CHARGE only on vaulted rails. WhatsApp is not a channel.
5. **Receipts.** `RCPT-` Official Receipt. Footer: not an LHDN e-invoice.
6. **Quotes.** `QT-` proforma, `/pay/{id}`, Net terms + reminder emails.
7. **Integrator.** Scoped `sk_test_` / `sk_live_`, signed `{ id, event_type, created_at, data }`, VitePress how-tos. `subscription.updated` is still forbidden.

Do **not** say: we file e-invoices; we do FPX e-mandate; we have DuitNow/TnG buttons; Team/Viewer works; Xendit is turnkey in ops; MRR is ledger cash; we prorate; we have WhatsApp dunning; chargebacks are accounted.

---

## 5. Ranked remaining problems

### P0 — fix before another merchant demo

| # | Problem | Evidence |
|---|---------|----------|
| 1 | Collection pause does not bump `NextBillingDate` or `failedIds`. One due paused sub can consume the hourly billing batch of 50. | [01](./01-commerce-subscriptions-checkout.md), [09](./09-architecture-tenancy-tests.md), `BillingEngineJob` |
| 2 | Update-payment / arrears is an unauthenticated subscription GUID. | [01](./01-commerce-subscriptions-checkout.md), `PublicArrearsEndpoints` |
| 3 | Stripe dispute publishes `GatewayRefundCompleted`. Cash leaves the ledger; sub stays ACTIVE; later refund can double-contra. | [03](./03-ledger-refunds-disputes-credits.md), `CommerceGatewayDisputeCreatedHandler` |
| 4 | CHIP/Billplz `EventId` = object id for fail **and** pay. Fail then pay can drop fulfillment. | [02](./02-payments-adapters-rails.md) |

### P1 — honesty and first-week product

- Trial cannot be canceled (`SubscriptionCancelDecision` rejects `TRIALING`).
- SST only on first checkout; renewals are net.
- 100% coupon / $0 Stripe path forces reminder-only (card not vaulted).
- Dunning email amount is catalog `Price`, not snapshot × seats.
- Invite email points at `/accept-invite`; no page exists ([05](./05-identity-roles-keys-audit.md)).
- Xendit in the gateway dropdown with no credential fields.
- Razorpay labeled “e-mandate” while `SupportsEmandate` is false.
- B2B PDF titled Tax Invoice **before** MyInvois VALID; quote B2B can put company name in CRM `IdValue` ([04](./04-lhdn-invoicing-documents.md)).
- No in-repo sandbox VALID artifact.
- OpenAPI honesty gate: 160 Minimal vs 149 OpenAPI ([08](./08-contracts-webhooks-dx.md)).
- Ops does not hide Admin chrome from Viewer/Member.
- README still says Xendit is a planned wrap and is not shipping.
- Tracker still has LP-056 / LP-097 / LP-137 as **N** while the code is live; LP-094 / LP-057 as **Y** while they have money bugs.

---

## 6. Architecture health

The modular monolith is still the right shape. NetArchTest locks host-composes-Infrastructure and module boundaries. Tenant filters fail closed. Outbox is real on all nine schemas. ~993 `[Test]` methods exist.

Strain from the wave dump:

- Eight inbox pollers with almost no inbox writers except Messaging.
- Wave 3/4 holes are **untested compositions** (invite accept, pause claim-loop, dispute→ledger, Xendit form).
- Identity is split-brain: cookie `CLIENT`, membership role injected from `X-Tenant-Id`.
- TypeSpec catalog lagged M2M commerce routes.

This is a coherent engine with recent trust debt, not a mess.

---

## 7. Market position (unchanged wedge, updated capability)

The job is still: **FPX + subscriptions + MyInvois + integrator webhooks**. No incumbent owns all four.

| Rival | They still win | We still exist because |
|-------|----------------|------------------------|
| HitPay | No-code + wallets + next-day MYR | We are BYOK + ledger + (aspirational) MyInvois, not an acquirer |
| Xendit | Real SEA mandates | Wrap them; do not clone xenPlatform. They do not file MyInvois. |
| Billplz / CHIP | Cheap hosted FPX | Rails, not OS. We already wrap them. |
| Stripe Billing | Developer default | No recurring FPX. No MyInvois. Expensive in MY. |
| Paddle / Polar | MoR tax | MoR breaks LHDN seller-of-record. Already refused. |

We now have the **subscription + commercial paper** half of the wedge. The **MyInvois half** is un-hidden and unproven. HitPay still wins wallets. Do not chase wallets or e-mandate until P0s and one VALID exist.

---

## 8. What to do next

Order is in [10](./10-honesty-risks-next.md) §7. Parent list:

1. Billing pause: push `NextBillingDate` or add `failedIds`.
2. Token-gate public update-payment.
3. Stop publishing `GatewayRefundCompleted` from disputes; set `HasOpenDispute`.
4. CHIP/Billplz EventId must include event type (or allow COMPLETED to supersede FAILED).
5. One sandbox **PENDING → VALID + QR** on a product checkout. Until then, sell receipts.
6. Fix quote B2B identity (ID type/value).
7. Accept-invite page + hide Admin chrome for Viewer.
8. Xendit credential fields **or** remove Xendit from the dropdown. Relabel Razorpay.
9. Flip README, ADR 023, and `007` tracker to match this tree.
10. Price Hub SaaS or keep public pricing at RM 0 honestly.

**Do not** start PayPal, usage, Xero, Meta WhatsApp, overlay checkout, or homemade FPX e-mandate. The refuse list still holds.

---

## 9. Demo script that does not require lying

Legal profile (stationery only) → Stripe or Billplz keys + Resend → product **without** TIN → share portal checkout → pay → Official Receipt `RCPT-` → ops transaction + CSV. If Stripe: show a renewal or period-end cancel. If Billplz: say “we email the next bill.” Show VitePress events + a signed webhook to the sample cashier.

Do **not** click Pause collection, Disputes, Team invite, WhatsApp step, B2B quote as “e-invoice,” or Xendit save.

---

## 10. Closing

Waves 0–4 graduated the repo from “serious backend, hidden UI” to “too much product to demo carelessly.” The architecture is coherent. Stripe/CHIP/Billplz money loops are mostly honest. The next work is **trust**, not features.

Read `01`–`10` before changing the roadmap. This parent is the judgment, not the evidence.
