# 10 — Product honesty, docs drift, remaining P0/P1, what to do next

**Program:** `plans/008-evals`  
**File:** `10-honesty-risks-next.md`  
**Date:** 16 August 2026 (workspace clock)  
**Branch context:** `plans/008-evals/README.md` names `feat/007-waves-1-4-implement` (`4624070`). This report evaluates **code as it is in the workspace**, not the August 16 competitor inventory as if it were still the product.  
**Not:** an implementation ticket. **Not:** a rewrite of ADR 021/023. **Not:** a rescoring of `plans/007-feats/00-checklist-tracker.md` (that file was not edited).  
**How W\*-done.md is used:** `plans/007-feats/impl/W*-done.md` files are **intent and landing notes**. Almost every one ends with `Not committed. Not pushed.` and a suggested tracker flip. They are **not** a score. Where a done file and the live file disagree, **the live file wins**. Where a done file and the tracker disagree, **the tracker is stale**.

**Sources read for this report (not summarized away):**

| Source | Absolute path | Why it is law or evidence |
|--------|---------------|---------------------------|
| Product watermark | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/README.md` | Shipping identity, honest-capability paragraph, Xendit “planned wrap” |
| ADR 021 | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/architecture-decision-log/021-compliance-caas-pivot.md` | Company shape: Compliance CaaS; kill vitamins; keep WhatsApp dunning + Xero **sync** |
| ADR 023 | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/architecture-decision-log/023-pure-caas-mvp-ui-lobotomy.md` | GTM: hide LHDN/B2B UI; compete on checkout + dunning |
| Parent evaluation | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/007-feats/00-evaluation.md` | 16 Aug judgment; many sentences are now false |
| Tracker | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/007-feats/00-checklist-tracker.md` | Living matrix; cells not flipped after Waves 0–4 |
| Refuse constitution | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/007-feats/19-refuse-list-and-adjacents.md` | Wave **R** still holds |
| Schema | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/007-feats/20-sequencing-and-tracker-schema.md` | Honesty rules; `shipped` requires a demoable path |
| Ops routes | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/App.tsx` | What a merchant can actually click |
| Ops sidebar | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/components/Sidebar.tsx` | Nav that is not a comment |
| Admin routes | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-admin/src/App.tsx` | Superadmin surface |
| Portal tree | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-portal/src/app/` | Buyer surfaces |
| Wave done notes | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/007-feats/impl/W*-done.md` | Intent, not score |

008-evals `README.md` points at sibling slices `01`–`09` as evidence. Those files are the per-domain uncondensed evaluations. This file is the **cross-cut**: what we may say in a sales call, what we must not demo, how the documents lie to each other, the ranked remaining P0/P1, the refuse lock, the next ten engineering actions, and a demo script that does not require lying.

---

## 0. How to read this file

A sentence is **sellable** only if all three are true:

1. A merchant or buyer can complete the job on a current local deploy (`task dev` + `task fe`) **without opening Git**.  
2. The money, access, and document that result match the words on the screen.  
3. The job is not on the refuse list in `19-refuse-list-and-adjacents.md`.

A sentence is a **lie** if any document (README, ADR, tracker, evaluation, pricing page, privacy page, ops copy) claims the job and one of those three fails.

`plans/007-feats/impl/W*-done.md` is allowed to say “tracker can move X → Y.” That is a **recommendation**. `00-checklist-tracker.md` was not updated. Treating a done file as a flipped cell is how the next README lie starts.

---

## 1. What the product is, literally, on this disk

### 1.1 Company shape (still ADR 021 + BYOK)

ADR 021 (`docs/architecture-decision-log/021-compliance-caas-pivot.md`) is still the company:

> Lazuar is exclusively a Compliance-First Checkout Engine (Compliance CaaS).  
> If a feature does not directly facilitate a transaction or keep a business legally compliant, we will not build it.

ADR 019/021/023 and the refuse list still forbid: Merchant of Record, licensed acquiring, GMV take-rate, website/link-in-bio builders, community DRM, POS hardware, full ERP, crypto settlement as a near-term product, India GSTN / Indonesia Coretax before MyInvois is a sold feature.

Money planes are still three, and they still must not collapse:

| Plane | Who pays whom | Processor on this disk |
|-------|---------------|------------------------|
| **G — merchant GMV** | Buyer → merchant | Tenant BYOK: Stripe, Billplz, CHIP, Razorpay, **and now Xendit hosted invoices** |
| **U — utility credits** | Merchant → Lazuar | Prepaid `TenantCreditBalance` for live-key LHDN submit (WhatsApp send cost is 0 and the channel is off) |
| **S — Hub SaaS fee** | Merchant → Lazuar | `Saas:Plan:AmountMyr` is **0** in repo config; checkout **400**s until an operator sets a positive amount |

We do not hold settlement. We do not KYC merchants for *our* acquiring. We do not remit SST/VAT as MoR.

### 1.2 GTM clock (ADR 023) versus the routes that actually exist

ADR 023 (`docs/architecture-decision-log/023-pure-caas-mvp-ui-lobotomy.md`) decided a **Pure CaaS MVP** by `[MVP-HIDE]`:

- Ops: remove Invoicing (Quotes, Tax Invoices, Credit Notes); remove Legal & Billing Profile; remove “Requires Company Name & Tax ID” on product forms.  
- Portal: remove TIN/company on checkout; force `notFound()` on `/pay/[sessionId]`; remove “Download Tax Invoice.”

**That document is no longer a description of the running UI.** It is a historical GTM decision that Wave 2 **reversed in the frontend without writing a new ADR**.

`apps/lazuar-ops/src/App.tsx` (comment still says “Pure CaaS MVP — ADR 023”) now **routes**:

```
/pricing, /signup, /login
/commerce/dashboard
/commerce/products
/commerce/subscribers
/commerce/transactions
/commerce/disputes
/commerce/coupons
/commerce/dunning-campaigns
/commerce/dunning-campaigns/new
/commerce/dunning-campaigns/:id
/commerce/templates
/developer/api-keys
/developer/webhooks
/developer/logs
/workspace/general
/workspace/team
/workspace/audit
/workspace/billing-profile
/workspace/payment-gateways
/workspace/email
/workspace/billing
/workspace/ledger
/invoicing/quotes
/invoicing/tax-invoices
/invoicing/credit-notes
```

The only remaining `[MVP-HIDE]` in that file is **ops chat** (`/ops/chat`).

`apps/lazuar-ops/src/components/Sidebar.tsx` shows four modules: Commerce, **Invoicing**, Developer, Workspace. Invoicing links are Quotes, Sales documents, Credit Notes. Workspace includes **Legal & Billing**. Commerce includes **Disputes**.

`apps/lazuar-ops/src/App.tsx` also routes `/workspace/ledger` (Utility Ledger). The sidebar **does not** link it. The page is a typed URL, not a nav item.

`apps/lazuar-admin/src/App.tsx` is still a one-page superadmin: `/login`, `/platform/gateways`. Catch-all → `/platform/gateways`.

Portal App Router (no `App.tsx`; this is the buyer surface):

| Path | File | Live? |
|------|------|-------|
| `/{tenantSlug}/checkout/{productSlug}` | `apps/lazuar-portal/src/app/[tenantSlug]/checkout/[productSlug]/page.tsx` | Yes — hosted hop 1 |
| `/{tenantSlug}/checkout/{productSlug}/success` | `.../success/page.tsx` | Yes — polls server status (Wave 0 honesty) |
| `/{tenantSlug}/checkout/custom/success` | `.../checkout/custom/success/page.tsx` | Yes — quote/custom success |
| `/{tenantSlug}/pay/{sessionId}` | `apps/lazuar-portal/src/app/[tenantSlug]/pay/[sessionId]/page.tsx` | **Yes — no `notFound()`**. ADR 023’s quote block is gone. |
| `/{tenantSlug}/portal` | `.../portal/page.tsx` | Yes — magic-link list, cancel, plan change, documents |
| `/{tenantSlug}/update-payment/{subId}` | `.../update-payment/[subId]/page.tsx` | Yes — **raw subscription GUID, no token** |
| `/legal/privacy`, `/legal/terms`, `/legal/refund` | `.../legal/...` | Yes |
| `/accept-invite` | **missing** | Email still mints this URL |
| `/verify-email` | **missing** | Email still mints this URL |
| `/reset-password` | **missing** | Email still mints this URL |

`App:ClientUrl` is `http://localhost:3004` in both `apps/lazuar-api/src/Lazuar.Api/appsettings.json` and `appsettings.Development.json`. That is **portal**, not ops (`3003`). Invite / verify / reset emails therefore land on portal paths that do not exist.

### 1.3 Adapters that exist (README is wrong about Xendit)

`Modules/Payments/Infrastructure/DependencyInjection.cs` registers `XenditGatewayAdapter`.  
`Modules/Payments/Infrastructure/Gateways/XenditGatewayAdapter.cs` is a BYOK wrap of `POST https://api.xendit.co/v2/invoices` plus `x-callback-token` webhooks and invoice refunds. Comment in the file: “Reminder-only until a payment-token soak proves off-session.”

`PaymentGatewayCapabilities` (`Modules/Payments/Contracts/PaymentGatewayCapabilities.cs`):

| Capability | Stripe | CHIP | Billplz | Razorpay | Xendit |
|------------|--------|------|---------|----------|--------|
| Off-session / vault | yes | yes | no | no | **no** |
| API refund | yes | yes | no (mark-refunded) | yes | yes |
| DuitNow QR (hosted page, not our pixel) | no | yes | yes | no | yes |
| Hosted wallets (Grab/Shopee/TnG/Boost) | no | yes | no | no | yes |
| FPX e-mandate | **false for every gateway** | | | | |

Ops and admin payment-settings dropdowns include `XENDIT`. M2M checkout allow-list includes `XENDIT`. Refund modal includes `XENDIT`.

Root README still says, twice:

- Honest-capability paragraph: “WhatsApp dunning, Xero/QuickBooks sync, and **Xendit are not shipping until their adapters exist**.”  
- Phase 1: “**Xendit is a planned wrap, not a live adapter.**”

That is the single loudest docs-vs-code contradiction in the repo. The adapter exists. It is a **hosted-invoice wrap**, not e-mandate, not xenPlatform, not off-session. Selling “Xendit like Stripe Billing” would still be a lie. Selling “you can paste Xendit keys and get a hosted invoice” is now true.

### 1.4 WhatsApp and Xero (ADR 021 keep-list)

ADR 021 explicitly **keeps** WhatsApp dunning and Xero / cloud-accounting **sync**.

Code:

- `Messaging:WhatsAppEnabled` defaults **false** (`appsettings.json`).  
- Transport is `Modules/Messaging/Infrastructure/Messaging/ConsoleMessagingService.cs` — `IsBillable => false`; logs `[Local Dispatch] [MESSAGING/SMS]`.  
- `W4-LP-074-done.md` / `W4-LP-155-done.md`: **did not** build Meta Cloud; “delete claims”; tracker stays **N**.  
- `W4-LP-121-done.md`: **not shipped**. No Xero OAuth. Tracker stays **N**.

The keep-list is still the keep-list. It is **not** a demoable product.

### 1.5 LHDN (the moat) — remounted UI, unproven VALID

Wave 2 remounted merchant surfaces:

- Ops Invoicing + Legal & Billing (`App.tsx`, `Sidebar.tsx`).  
- Checkout TIN/company when `requires_tax_id` (`W2-LP-022-done.md`).  
- Quotes + `/pay/{id}` (`W2-LP-102-done.md`).  
- `INV-` tax invoice PDF on B2B pay **without waiting for VALID** (`W2-LP-103-done.md`).  
- Poller still owns VALID/INVALID (`W2-LP-111-done.md`).  
- Official Receipts carry a footer that they are **not** MyInvois tax invoices (`W4-LP-100-done.md`).

`GetPublicPricingQueryHandler` still hard-codes `Lhdn_credits_live = false` and `Whatsapp_credits_live = false`. The public pricing page (`apps/lazuar-ops/src/components/PricingPage.tsx`) therefore still tells strangers: “LHDN merchant UI is not live in Hub Ops yet.” **That sentence is now false as a routing fact and still true as a VALID fact.** Invoicing nav exists. A sandbox document that becomes `VALID` with a scannable QR has **not** been evidenced in any W2 done file. Every LHDN “Y” recommendation is gated on that missing run.

Default signing is unsigned XML 1.0 (`Lhdn:Signing` = `Off`; `W2-LP-117-done.md`). Do not say “XAdES-signed e-invoice.”

---

## 2. What we can sell without lying

This is the sales script that does not require a footnote the founder cannot defend.

### 2.1 Identity (one breath)

We are **headless checkout + subscription state + email recovery + a double-entry ledger**, sitting **on top of** the merchant’s own Billplz / Stripe / CHIP / Razorpay / Xendit account. We are **not** the acquirer. We are **not** Merchant of Record. We take **0% of guest GMV**. Hub software is free in repo config until an operator sets `Saas:Plan:AmountMyr`. Official Receipts are payment receipts. They are **not** LHDN tax invoices.

### 2.2 Jobs that are honest to put in a deck

**Take money (one-time and first subscription period)**

- Shareable product checkout at `/{tenantSlug}/checkout/{productSlug}` (portal `:3004`).  
- Guest checkout: name, email, amount; optional address/phone/quantity; coupons; pay-what-you-want.  
- Hop 1 is EN | BM (`W1-LP-020-done.md`).  
- Success page polls server status; timeout is “still processing,” not “paid.”  
- Redirect to **one** BYOK hosted page (Billplz bill, Stripe Checkout, CHIP Collect, Razorpay link, Xendit invoice). We do not render DuitNow QR or wallet buttons as our own pixels. Those appear on the **processor** page when the merchant enabled them there.  
- Branding is logo/colors/name, not a site builder.  
- Sample integrator path: `examples/hub-cashier-next` + `POST /integrations/payments/checkouts`.

**Run subscriptions (with the collection-mode sentence said out loud)**

- Products: one-time / monthly / yearly; FIXED or PWYW; optional trial days (`TRIALING`) that do not charge before `TrialEndsAt`.  
- Statuses persisted: `PENDING`, `ACTIVE`, `TRIALING`, `PAST_DUE`, `SUSPENDED`, `CANCELED`. Collection pause is a **flag on ACTIVE**, not a `PAUSED` status (`W3-LP-057-done.md`).  
- **Two collection modes** (`W1-LP-053-done.md`, `PaymentGatewayCapabilities`):  
  - **Auto-debit:** Stripe and CHIP only (`SupportsOffSession`). Card/token on file; billing job charges off-session.  
  - **Pay-link each cycle:** Billplz, Razorpay, Xendit, offline. Billing mints a hosted bill; email carries `{{renewal_link}}`. **Billplz cannot vault.** Saying “silent FPX renewals” is a lie.  
- Cancel immediately (ops default). Cancel **at period end** (portal default; `CancelAtPeriodEnd`; billing job finalizes when due) — **code exists** even though tracker `LP-056` is still **N**.  
- Plan change and quantity change are **next-renewal-only** (`amount_due_now = 0`; `prorate=true` is 400). Do not say “proration like Stripe Billing.”  
- Offline / manual / COMPED enroll exists. COMPED still has a `NextBillingDate` and will `PAST_DUE` unless ops records payment. Do not say “lifetime.”

**Recover failed payments (email only)**

- Campaign builder: day offsets, EMAIL, AUTO_CHARGE (skipped on reminder-only), grace, SUSPEND/CANCEL.  
- Failed vaulted renewal can enter `PAST_DUE` and a run (Wave 0 intent; do not re-open the 2026-08-03 “ACTIVE forever” gap without re-reading the job).  
- Email requires tenant Resend BYOK.  
- WhatsApp step type may still appear in the builder; `DunningStepDispatcher` demotes/skips when `WhatsAppEnabled` is false. **Do not click WhatsApp in a demo.**  
- Update-payment **page** exists. Sell it only as “the link we email,” not as a secure customer-portal secret (see P0 GUID).

**Money after the charge**

- Inbound webhooks: verify, persist `PaymentWebhookLog`, EventId + business-key idempotency, `{ received: true }` is intake not fulfillment (`W0-LP-090-done.md`).  
- Full and partial refunds from ops for Stripe / CHIP / Razorpay / Xendit. Billplz / offline: **mark-refunded** only (`W1-LP-091-done.md`, `RequiresMarkRefunded`).  
- CSV export of transaction logs (`W1-LP-097-done.md`) — code exists; tracker still **N**.  
- Double-entry ledger on the payment happy path (cash / fee / gross / tax). Official Receipt `RCPT-` with “Payment receipt. Not an LHDN e-invoice.”  
- Disputes **page** exists (`/commerce/disputes`). You may say “we show Stripe chargebacks as OPEN rows.” You may **not** say “we account chargebacks correctly” (see P0 dispute-as-refund).

**Developer product**

- Ops: API keys (live/test, reveal once, revoke), scoped keys (including `commerce.subscriptions:read|write`), outbound webhook endpoints, delivery logs + redrive path from Wave 0/1 work.  
- M2M: create checkout; list/get/cancel subscriptions (`W1-LP-137-done.md`). Not full Commerce admin (no product CRUD over keys unless a later ticket says so).  
- TypeSpec → OpenAPI → `lazuar-developers` Scalar hub. Event catalog is better than August 16 and still not Stripe-complete.  
- LHDN SDKs (npm + NuGet) exist as libraries. That is not “MyInvois is live in the merchant UI.”

**Merchant console (what you can click without apology)**

- Dashboard: net-cash style cards + MRR/ARR from **subscription snapshots**, not from a CFO-grade ledger flatten (`W3-LP-161-done.md`). Say “directional MRR,” not “board-ready RevRec.”  
- Checkout links, subscribers, transaction logs, coupons, dunning campaigns, templates.  
- Payment gateways (BYOK, environment `test`|`live`, Billplz host follows config not hostname — `W1-LP-182-done.md`).  
- Email provider (Resend).  
- Team page (roles ADMIN/MEMBER/VIEWER) — **invite email is broken** (see P1). You can still add the first user via seed / signup.  
- Audit log.  
- Plan & billing: shows Hub Starter at RM 0; Pay is 400 until `AmountMyr > 0`.  
- Legal & Billing + Invoicing: you may **open** them. You may not claim a sandbox `VALID` QR unless you have just produced one.

**Buyer portal**

- Magic-link login. List subscriptions. Cancel at period end (healthy ACTIVE) or immediate (PAST_DUE). Keep. Plan change with “No charge today.” Documents table when HMAC URLs exist. Receipt download when `document_url` is present.

**Commercial model**

- 0% GMV. Pricing page headline: “RM 0 on your sales.”  
- Checkout software free today (`AmountMyr = 0` ⇒ `checkout_is_free`).  
- Credit packs 50/500, 100/1100, 200/2500 in config. Starter grant 50. LHDN submit costs 3 credits **when** a live-key submit actually runs. WhatsApp is not billed.

### 2.3 Sentences that are true if you keep the qualifier

| Sentence | Qualifier that keeps it honest |
|----------|--------------------------------|
| “Multi-gateway BYOK” | Stripe, Billplz, CHIP, Razorpay, Xendit **hosted invoice**. Not Fiuu, SenangPay, Midtrans, PayMongo. |
| “Subscriptions that renew” | Auto-debit only on Stripe/CHIP. Everyone else is an emailed link. |
| “Dunning” | Email sequences + off-session retry on vaulted rails. Not WhatsApp. |
| “Double-entry ledger” | Happy-path payments and API refunds. Disputes currently post as refunds (lie). Deferred revenue parked. |
| “LHDN / e-invoice” | Backend + remounted UI. **No proven sandbox VALID.** Receipt ≠ tax invoice. Unsigned XML 1.0 by default. |
| “Xendit” | Hosted invoice wrap. Reminder-only. Not e-mandate. Not xenPlatform. |
| “Apple Pay / Google Pay” | Only if the **Stripe** (or CHIP hosted) account shows them. We do not draw the buttons. |
| “Trials” | `TrialDays` 0–90; converts on due tick if vaulted. 100% coupon is not a trial. |
| “Pause” | Collection holiday flag. **Do not demo on a book of due subscriptions** (starve). |
| “Staff roles” | ADMIN/MEMBER/VIEWER in ops. Invite **link 404s**. |
| “Self-serve signup” | `/signup` + `/pricing` + workspace create. Time-to-first-checkout is still “partial” if Billplz callback is not a public HTTPS base. |

---

## 3. What we must not demo

These are not “stretch goals.” They are **landmines**. A prospect who sees them will either (a) think we have a product we do not, or (b) watch money/access break.

### 3.1 Do not demo — channel and compliance claims

| Demo move | Why it is a lie |
|-----------|-----------------|
| Dunning campaign step **WhatsApp** | `ConsoleMessagingService` + `WhatsAppEnabled=false`. Logs to console. Privacy page still names Meta. |
| “We file MyInvois and here is the QR” | No W2 done file records a sandbox `VALID`. Poller exists. Tests mock `VALID`. IRBM sandbox is not evidenced. |
| Tax invoice as if it were a validated e-invoice | `W2-LP-103-done.md`: `INV-` PDF is issued **on pay**, before VALID. That is a commercial tax invoice, not a MyInvois success. |
| Xero / QuickBooks / “sync to your accountant” | `W4-LP-121-done.md`: not shipped. |
| FPX auto-debit / e-mandate / “we charge their bank every month” | `SupportsEmandate` is hard-false (`W4-LP-032-done.md`). Billplz is reminder-only. |
| Silent Billplz renewal | Ops already warns. Still the most common founder assumption. |
| GSTN / Coretax / InvoiceNow | Refuse-until-MyInvois-sold (`LP-209`). Zero modules. |
| “Compliance CaaS is live” | ADR 021 identity. ADR 023 hid it. Wave 2 remounted chrome. **VALID loop unproven.** The moat is inventory + UI, not a closed government loop. |

### 3.2 Do not demo — money correctness

| Demo move | Why it is a lie |
|-----------|-----------------|
| **Pause collection** on a workspace that has other due subscribers | Billing claim SQL does not exclude paused rows; skip does not add `failedIds`; same paused row is claimed up to **50 times per cycle**; other due subs **starve**. See §5.1. |
| **Disputes** as “we reversed the sale correctly” | Handler publishes `GatewayRefundCompletedIntegrationEvent`. Ledger posts **refund** contra. LHDN may **cancel/CN** a VALID invoice with reason “Customer requested refund.” A chargeback is not a refund. See §5.3. |
| CHIP pay-then-fail or fail-then-pay on the **same purchase id** | `EventId = purchaseId` for both `purchase.paid` and `purchase.payment_failure`. Unique `(Provider, EventId)` drops the second type. See §5.7. |
| Hub **Pay** on Plan & billing | `Saas:Plan:AmountMyr` is 0; `CreateSaasCheckoutCommandHandler` 400s. See §5.5. |
| “Net cash includes Billplz fees” | Billplz fee path is still estimated / historically 0 in older gap docs. Do not put a fee slide on a Billplz-only tenant without reading the current adapter lines. |

### 3.3 Do not demo — security and onboarding

| Demo move | Why it is a lie |
|-----------|-----------------|
| “Secure customer portal update-card link” | `/update-payment/{subId}` and `GET/POST /public/commerce/checkout/{guid}/…` take a **bare GUID**. No magic token, no HMAC, no tenant slug on the API. Anyone who sees a GUID can read arrears and start a checkout (RM 1 verification on ACTIVE, full price on PAST_DUE). See §5.2. |
| Team **Invite** | Ops toasts “Invitation sent.” Email is `{portal}/accept-invite?token=`. Portal has no such route. Next.js 404. Same class of bug: `/verify-email`, `/reset-password`. See §5.4. |
| Ops catch-all as “invite landing” | `App.tsx` `path="*"` → `/commerce/dashboard`. Even if you pointed `ClientUrl` at ops `:3003`, `/accept-invite` would **not** accept; it would dump a logged-in user on the dashboard. |

### 3.4 Do not demo — refuse and adjacent theatre

Do not open, even if leftover components exist:

- Website / funnel / link-in-bio builders  
- Community / Telegram bouncer / Vault / Academy  
- POS / tap-to-pay  
- Marketplace / Discover / split pay  
- MoR / “we take 2%”  
- Settlement / payout reports as if we paid them  
- SMS product, marketing blasts, HubSpot CRM  
- Crypto / USDC checkout  
- Escrow.com / DocuSign at checkout (delay, not MVP)  
- Ops AI chat (still `[MVP-HIDE]`)

Portal still contains `modules/community/` and a privacy sentence about Meta WhatsApp. Do not walk a prospect through those files.

### 3.5 Do not demo — documents that look legal

| Surface | Honest label | Dishonest label |
|---------|--------------|-----------------|
| `RCPT-` PDF | Official Receipt / payment receipt | Tax invoice, e-invoice, MyInvois |
| `INV-` PDF before VALID | Commercial tax invoice we generated | LHDN-validated invoice |
| Quote `/pay/{id}` | Proforma / payment request | Tax invoice |
| Credit note after a **dispute** | (should not exist via refund event) | Customer-requested refund |

---

## 4. README / ADR / tracker / 007-feats / routes — drift table

Read this table left to right. **Code** is what `task dev` runs. Everything else is allowed to be wrong.

### 4.1 Identity and rails

| Claim | Where it is written | Code / routes | Verdict |
|-------|---------------------|---------------|---------|
| Shipping product = ADR 021 + ADR 023; LHDN B2B UX unrouted until Phase D.3 | README watermark table | Invoicing + Legal & Billing **routed and in the sidebar**. Only ops chat remains `[MVP-HIDE]`. | **Stale watermark.** ADR 023 is no longer the UI. A new ADR (or a dated amendment) is required if Wave 2 remount is the product. |
| Honest capability: BYOK Stripe/Billplz/CHIP/Razorpay; Xendit **not** shipping until adapter exists | README L18 | `XenditGatewayAdapter.cs` + DI + ops/admin dropdowns + M2M allow-list | **README lie.** Adapter is a hosted-invoice wrap, reminder-only. |
| Phase 1: “Xendit is a planned wrap, not a live adapter” | README L74 | Same | **README lie.** Same qualifier: wrap ≠ e-mandate. |
| WhatsApp dunning, Xero/QuickBooks not shipping | README L18 | Console stub; no Xero client | **True.** |
| Billplz renewals = emailed hosted link | README L18; `W1-LP-053-done.md` | `IsReminderOnlyGateway`; hop-1 “Not auto-debit” | **True.** |
| `RCPT-` is not a MyInvois tax invoice | README L18; `W4-LP-100-done.md` | Receipt footer | **True.** |
| Prepaid utility wallet; console WhatsApp not billed | README § Prepaid | `IsBillable => false`; `WhatsAppSend` cost 0 | **True.** |
| Architecture diagram still draws “Vault / SaaS (Secure R2 PDF)” | README ASCII art | Vault module **removed** (ADR 022) | **Cosmetic drift.** Do not present the diagram in a sales deck without editing. |
| Phase 2/3: Escrow, e-sign, Telegram bouncer, Wise, BNPL, Bitcoin, Singpass | README “Master Integration Roadmap” | Refuse or delay per `19` | **Historical ambition.** Watermark already says so; the roadmap section still reads like a backlog. |
| GSTN / Coretax “not scheduled” | README Phase 1 | No modules | **True** (and must stay true). |
| Bitcoin looks the same to the ledger | README § Absolute Financial Truth | Journal shape, not a rail | **Architecture metaphor.** Do not demo crypto. |

### 4.2 ADR 021 vs ADR 023 vs Wave 2 remount

| ADR sentence | Status vs code |
|--------------|----------------|
| 021: exclusively Compliance CaaS | Company law. **Product** is still Pure CaaS + remounted but unproven LHDN chrome. |
| 021: keep WhatsApp dunning | Keep-list. Channel **absent**. Honesty patch landed (`W4-LP-074`). |
| 021: keep Xero sync | Keep-list. Code **absent**. |
| 021: kill giveaways, community DRM, link-in-bio | Held. No modules. Refuse list agrees. |
| 021 pillar 1: B2C consolidation on the 28th | Job exists (`W2-LP-114-done.md` threshold 10000). **Y** gated on merchants seeing last-run + sandbox. |
| 021 pillar 2: TIN before pay + instant LHDN QR | TIN fields remounted. Instant **commercial** INV-. QR gated on VALID. Escrow/e-sign **not** built (delay). |
| 021 pillar 3: USDC/Web3 + zero-rate export | Aspiration. Crypto refuse. `LP-119` N. |
| 023: hide Invoicing, Legal profile, TIN toggle, quote route, tax-invoice download | **All remounted** except ops chat. 023’s “tree-shake orphaned components” is false for invoicing. |
| 023: compete on Billplz + **WhatsApp** dunning | WhatsApp half is still a lie. Email + FPX link is the real temporary wedge. |
| 023: “zero-friction reactivation = uncomment `[MVP-HIDE]`” | Wave 2 did more than uncomment (CRM company name, B2B metadata, poller/ledger join fixes). Reverse is not a comment toggle anymore. |

**Implication:** Until someone writes ADR 024 (“Wave 2 remount; VALID is the go-live gate”), every new engineer will read 023 and hide invoicing again, or read 021 and sell VALID.

### 4.3 `00-evaluation.md` (16 Aug parent judgment) — stale cells

`plans/007-feats/00-evaluation.md` is still the parent narrative. These sentences are **false as of this workspace**:

| Evaluation sentence | Now |
|---------------------|-----|
| “Only the first [pillar] is honestly sellable today. The ledger exists but is not yet audit-grade. Dunning has a campaign UI. WhatsApp is a console stub. LHDN … with **no merchant nav**.” | WhatsApp stub: still true. LHDN **has merchant nav**. Ledger still not audit-grade (dispute-as-refund). |
| “`TRIALING` is mentioned once, no trial product field. No proration, no usage, no plan change.” | `TrialDays` + `ActivateTrial` exist. Plan change + quantity exist (next-renewal-only). Usage still N. |
| “Customer portal: magic-link list + hard cancel. No payment-method update, no invoice history, no cancel-at-period-end.” | Update-payment CTA exists (insecure). Documents table exists. Period-end cancel is portal default. |
| Wave 0–4 described as **later** | `plans/007-feats/impl/` contains W0–W4 done notes. Implementation happened. Evaluation was not rewritten. |
| “Xendit adapter: not built” (via sibling 06, echoed in evaluation’s wrap list) | Built as hosted-invoice wrap. |

The evaluation’s **refuse** section and **ICP** paragraph are still correct. Do not throw the file away. Do not quote its “today” column in a sales call.

### 4.4 `00-checklist-tracker.md` — stale Lazuar cells

The tracker header says “Living file. Flip cells when code changes.” It was not flipped. W\*-done files **ask** for flips. Below: **tracker cell → what the code/done note says → honesty mark**.

Marks: **STALE** = cell should move; **HOLD** = cell is still the honest mark; **OVERSOLD** = cell is Y/P but the loop is unsafe.

#### A. Positioning

| ID | Tracker | Code / done | Mark |
|----|---------|-------------|------|
| LP-001 BYOK | Y | True (now includes Xendit keys) | HOLD |
| LP-002 MoR | R | Refuse | HOLD |
| LP-003 Acquirer | R | Refuse | HOLD |
| LP-004 SaaS fee | P | `AmountMyr=0`; path exists; 400 until priced | HOLD |
| LP-005 Credits | P | Packs + wallet; LHDN live-key only; WA not billed | HOLD |
| LP-006 Public signup + pricing | Y | `/pricing`, `/signup` | HOLD |
| LP-007 KYC for our acquiring | R | Refuse | HOLD |

#### B. Checkout

| ID | Tracker | Code / done | Mark |
|----|---------|-------------|------|
| LP-010–013, 019 links/hosted/coupon/PWYW/guest | Y | Live | HOLD |
| LP-014 Quantity | P | Wave 1 done exists; seats also W3 | likely STALE toward Y if renewal survives |
| LP-020 BM/EN | Y | `W1-LP-020-done.md` | HOLD |
| LP-021 Mobile/QR | Y | QR is hosted-page wrap, not our pixel | HOLD with qualifier |
| LP-022 Company + TIN | **B** | Remounted; `W2-LP-022-done.md` asks **P** | **STALE** |
| LP-024 Success honesty | P | Wave 0 poll exists; still not Stripe-grade | HOLD or light STALE |
| LP-025 Branding | P | Partial skin | HOLD |

#### C. Rails

| ID | Tracker | Code / done | Mark |
|----|---------|-------------|------|
| LP-032 FPX e-mandate | N | `SupportsEmandate` false; `W4-LP-032-done.md` | HOLD |
| LP-041–043 Stripe/Billplz/CHIP | Y | Live | HOLD |
| LP-044 Razorpay | W | `W4-LP-044-done.md` honesty pipe; off-session false | HOLD |
| LP-045 Xendit | W | Adapter **exists**; wrap mark is correct **if** README is fixed | HOLD as wrap; README conflicts |
| LP-047 Saved card | P | Stripe/CHIP only | HOLD |

#### D. Subscriptions

| ID | Tracker | Code / done | Mark |
|----|---------|-------------|------|
| LP-052 Auto renewal | P | Vaulted only | HOLD |
| LP-053 Reminder-only | P | `W1-LP-053-done.md` asks Y | **STALE** (should be Y with Billplz qualifier) |
| LP-054 Trials | Y | `W3-LP-054-done.md` | HOLD |
| LP-055 Cancel now | Y | Live | HOLD |
| LP-056 Cancel at period end | **N** | `CancelAtPeriodEnd` + portal default; `W1-LP-056-done.md` asks Y | **STALE** |
| LP-057 Pause / resume | Y | Flag exists; **starve bug** | **OVERSOLD** |
| LP-058 Plan change | Y | Next-renewal schedule | HOLD |
| LP-059 Proration | Y | Done file: **next-renewal-only**, not unused-time credit | HOLD only with footnote; cell text says “or next-renewal-only” — OK |
| LP-060 Seats | Y | Snapshot × quantity | HOLD |
| LP-063 Multi-price | Y | Wave 3 | HOLD |
| LP-065 Offline sub | Y | Live | HOLD |

#### E. Dunning

| ID | Tracker | Code / done | Mark |
|----|---------|-------------|------|
| LP-070 Campaign builder | Y | Live | HOLD |
| LP-071–073, 078, 079 loops | P/Y mix | Wave 0 dones exist | mostly HOLD; do not mark WA |
| LP-074 WhatsApp recovery | N | Console; claims deleted | HOLD |
| LP-075 Magic update-PM | Y | Page exists; **unauthenticated GUID** | **OVERSOLD** as a security feature |
| LP-080 Pause dunning | Y | Different from collection pause | HOLD |

#### F. After the charge

| ID | Tracker | Code / done | Mark |
|----|---------|-------------|------|
| LP-090 Inbound webhooks | Y | `W0-LP-090-done.md` | HOLD |
| LP-091–093 Refunds | Y | Wave 1 dones | HOLD |
| LP-094 Disputes first-class | Y | Page + row; ledger via **refund event** | **OVERSOLD** |
| LP-095 Settlement reports | R | Refuse | HOLD |
| LP-097 CSV | **N** | `W1-LP-097-done.md` asks Y | **STALE** |

#### G–H. Invoicing and LHDN

| ID | Tracker | Code / done | Mark |
|----|---------|-------------|------|
| LP-100 Receipt PDF | Y | Disclaimer footer | HOLD |
| LP-101 Sequential numbers | P | Series exist | HOLD |
| LP-102 Quotes | **B** | Routed; `/pay/{id}` live; done asks Y | **STALE** |
| LP-103 Tax invoice | **B** | `INV-` on pay; done asks **P** until VALID | **STALE** (to P, not Y) |
| LP-104 Notes | **B** | Credit notes routed; VALID CN unproven | **STALE** to P |
| LP-106 Buyer download | **B** | HMAC when URL exists; done asks Y with LP-175 | **STALE** |
| LP-107 PDF branding | P | Partial | HOLD |
| LP-110 Submit | **B** | Remounted + CRM TIN required; done asks Y **when** checkout lands PENDING | **STALE** to P until sandbox |
| LP-111 VALID/INVALID poll | **B** | Poller + ops panel; done asks Y **when sandbox VALID shows** | HOLD as B/P until that run |
| LP-112 TIN validate | **B** | Done asks Y on sandbox | HOLD until sandbox |
| LP-113 QR | **B** | Gated on VALID | HOLD |
| LP-114 B2C consolidation | **B** | Job + threshold; done asks P | **STALE** to P |
| LP-116 Cancel/reject | **B** | Gated on VALID | HOLD |
| LP-117 XAdES | **N** | Unsigned 1.0 default; JSON 1.1 if cert; done asks **P** | **STALE** to P |
| LP-121 Xero | N | Not shipped | HOLD |
| LP-122 Legal profile | **B** | Remounted; done asks Y | **STALE** |
| LP-123 PDPA anonymize | Y | Live | HOLD |

Tracker note under G still says “Ops invoicing routes exist and are **unrouted** (ADR 023).” **That sentence is false.**

#### I–N. DX, comms, dashboard, portal, trust

| ID | Tracker | Code / done | Mark |
|----|---------|-------------|------|
| LP-137 M2M sub admin | **N** | `W1-LP-137-done.md` asks Y (list/get/cancel) | **STALE** |
| LP-155 WhatsApp Meta | N | Not built | HOLD |
| LP-161 Ledger MRR | Y | Snapshot MRR, **not** `billing.LedgerLines` flatten | **OVERSOLD** vs the row title “ledger-based” |
| LP-166 Staff roles | Y | Roles exist; invite 404 | HOLD with P1 |
| LP-173 Portal update PM | P | First-class CTA; GUID hole | HOLD at P (done asked Y — **do not flip to Y**) |
| LP-174 Portal plan change | Y | Live, next-renewal | HOLD |
| LP-175 Invoice/receipt history | **B** | Documents table; done asks Y | **STALE** |
| LP-182 Sandbox + test keys | P | Config environment `test`\|`live`; done asks Y | **STALE** toward Y with qualifier (no IRBM VALID sandbox) |
| LP-184 Self-serve workspace | Y | Live | HOLD |

#### Wave backlog inside the tracker

The “Priority backlog” at the bottom of `00-checklist-tracker.md` still lists Wave 0 “close loops” and Wave 1 “cancel at period end / refunds / portal update” as **to-do**. Those waves were implemented (with residual P0s). The backlog is **archaeology**. Do not staff it as if nothing shipped.

### 4.5 W\*-done.md as intent, not score

Pattern in every done file checked:

- Describes files and tests.  
- Ends with `Not committed. Not pushed.`  
- Ends with `Tracker LP-xxx can move A → B`.

Rules for 008-evals:

1. A done file is evidence that **someone believed** the job landed in this working tree.  
2. It is **not** evidence the tracker, README, or ADR were updated. They were not.  
3. It is **not** evidence a sandbox VALID, a production Billplz paid bill, or a live Xendit invoice was seen. Manual e2e lines are often “not run.”  
4. Where a done file asks **Y** and this report marks **OVERSOLD** (LP-057, LP-094, LP-075/173, LP-161), **do not flip the tracker to Y**.

### 4.6 007-feats standing constraints vs Wave 2

`plans/007-feats/README.md` still says:

> Do not sell WhatsApp dunning or LHDN e-invoice as live product until those loops are closed and (for LHDN) un-hidden.

Un-hidden **happened**. Closed **did not** (no VALID). The constraint is still the right sales rule. The word “un-hidden” is no longer the gate. **VALID is the gate.**

### 4.7 Public pricing vs remounted invoicing

`GetPublicPricingQueryHandler` hard-codes `Lhdn_credits_live = false`.  
`PricingPage.tsx` therefore prints: “LHDN merchant UI is not live in Hub Ops yet. Do not buy credits expecting e-invoice at checkout today.”

That was honest under ADR 023. It is now **half-false**: the UI is live; the government loop is not. A prospect who opens `/pricing` then logs into ops and sees Invoicing will think we are confused. Fix the copy to: **UI is there; do not buy credits expecting a VALID QR until we have a sandbox proof.**

### 4.8 Privacy page vs WhatsApp honesty

`apps/lazuar-portal/src/app/legal/privacy/page.tsx` §3 Sub-Processors:

> **Meta (WhatsApp):** For delivering automated session reminders and invite links.

`W4-LP-074-done.md` deleted README claims. The **legal page still names Meta as a sub-processor**. That is a PDPA/honesty defect. We do not send WhatsApp. We should not list Meta until we do.

### 4.9 008-evals README vs 007-feats

`plans/008-evals/README.md` says 007-feats is **historical** and not truth unless re-checked. This file is that re-check. Do not let a later editor “fix” 007 tracker cells from done files without reading §5.

---

## 5. Ranked remaining P0 / P1

Ranking rule: **money loss or theft first**, then **legal document lies**, then **security of payer identity**, then **onboarding that 404s**, then **commercial collection**, then **compliance proof**, then **idempotency collisions**.

### P0-1 — Billing collection-pause starves the billing engine

**IDs:** LP-057 (OVERSOLD).  
**Files:**

- `Modules/Commerce/Infrastructure/Workers/BillingEngineJob.cs`  
- `Modules/Commerce/Domain/Aggregates/Subscription.cs` (`PauseCollection` / `IsCollectionPaused`)  
- `Modules/Commerce/Infrastructure/Workers/DunningEngineJob.Claim.cs` (pre-dunning **does** exclude pause; billing **does not**)  
- `tests/.../BillingEngineJobTests.cs` `RunOnce_CollectionPaused_SkipsChargeAndKeepsActive`  
- `plans/007-feats/impl/W3-LP-057-done.md` (“does not roll `NextBillingDate`”)

**Mechanism, literally:**

`ClaimDueSubscriptionAsync` SQL:

```sql
SELECT * FROM commerce."Subscriptions"
WHERE "NextBillingDate" IS NOT NULL
  AND "NextBillingDate" <= NOW()
  AND "Status" NOT IN ('PENDING', 'PAST_DUE', 'SUSPENDED', 'CANCELED')
ORDER BY "NextBillingDate"
LIMIT 1
FOR UPDATE SKIP LOCKED;
```

There is **no** `CollectionPausedUntil` predicate. A paused, still-`ACTIVE`, overdue row is the oldest due row.

`ProcessOneSubscriptionAsync` then:

```csharp
if (sub.IsCollectionPaused(DateTime.UtcNow))
{
    _logger.LogInformation("Billing skipped collection-paused subscription {Id} until {Until}.", ...);
    return; // does not failedIds.Add
}
```

`ProcessBillingAsync` loops `BatchSize = 50`. After the skip it `SaveChanges` + **commits**. The row is unlocked. The next iteration claims **the same row**. Fifty times. Every other due subscription in the installation waits for the next engine interval, then loses again.

Dunning claim SQL **does** exclude pause (`CollectionPausedUntil IS NULL OR <= NOW()`). So we stop nagging and also stop billing everyone else.

`ResumeCollection` can optionally push `NextBillingDate` forward. Expiry of `CollectionPausedUntil` without an explicit resume does **not** roll the date. The test **asserts** the overdue `NextBillingDate` is unchanged. When the pause elapses, the engine immediately charges the stale period (catch-up), which may be what a merchant wanted — but only after starving the rest of the book.

**Why P0:** One CS “pause this customer until next month” on a due sub can halt **platform-wide** renewals for the life of the pause (hours × 50 wasted claims per tick). This is not a theoretical FOR UPDATE footgun; it is the control flow as written.

**Do not demo pause.** Do not flip LP-057 to a proud Y in marketing.

**Fix shape (not implemented here):** exclude paused rows in the claim SQL **or** `failedIds.Add(sub.Id)` on skip **and** decide an explicit policy for `NextBillingDate` (roll vs catch-up) on resume/expiry. Prefer exclude-from-claim so a paused row never takes a lock.

---

### P0-2 — Public update-payment is a capability URL of a guessable GUID

**IDs:** LP-075 (Y OVERSOLD), LP-173 (P — **keep P**).  
**Files:**

- `apps/lazuar-portal/src/app/[tenantSlug]/update-payment/[subId]/page.tsx`  
- `Modules/Commerce/Infrastructure/Endpoints/PublicArrearsEndpoints.cs`  
- Portal dashboard link `href=/{tenantSlug}/update-payment/${sub.id}`  
- Email hydrator: `https://{portal}/{slug}/update-payment/{subscriptionId}`

**Mechanism, literally:**

`GET /public/commerce/checkout/{subId:guid}/arrears` runs:

```sql
WHERE s."Id" = @SubId LIMIT 1
```

No tenant slug, no magic token, no HMAC, no session cookie. Response includes `product_name`, `amount`, `currency`, `status`, `is_reminder_only`.

`POST .../update-payment` with the same GUID starts a real checkout:

- `ACTIVE` vaulted → **RM 1** verification charge, metadata `update_payment=1`.  
- `PAST_DUE` / `SUSPENDED` → **full** line amount.  
- Cached hosted URL may be returned.

Portal page calls those endpoints with only `subId` from the path.

Subscription primary keys are `Guid.CreateVersion7()` (time-ordered). They appear in outbound emails, dunning templates, webhook payloads, and ops screens. Anyone who has a GUID — forwarded email, compromised ESP, leaked webhook, shoulder-surf, log aggregator — can:

1. Read whether the sub is past due and for how much.  
2. Start a payment that **updates the vault** or pays arrears.  
3. On ACTIVE, force a RM 1 charge the merchant did not request.

This is not “security through obscurity that happens to be 128 bits.” v7 GUIDs leak time. The API is **cross-tenant** if you have the id (no slug check). Compare portal **documents**, which `W2-LP-175-done.md` binds to a **magic token**. Update-payment did not get that treatment.

**Why P0:** Payer identity + payment initiation on an unauthenticated money endpoint.

**Fix shape:** require the same magic-link token (or a signed, expiring, single-purpose HMAC) on GET arrears and POST update-payment. Bind tenant slug. Do not accept bare GUID. Do **not** mark LP-173 Y until that ships.

---

### P0-3 — Dispute is booked as a refund (and may cancel a tax invoice)

**IDs:** LP-094 (Y OVERSOLD).  
**Files:**

- `Modules/Commerce/Infrastructure/EventHandlers/CommerceGatewayDisputeCreatedHandler.cs`  
- `Modules/Billing/Infrastructure/EventHandlers/GatewayRefundCompletedHandler.cs`  
- `Modules/Lhdn/Infrastructure/EventHandlers/GatewayRefundCompletedIntegrationEventHandler.cs`  
- `plans/007-feats/impl/W3-LP-094-done.md` (admits “Ledger contra is the existing Billing `GatewayRefundCompleted` consumer”)  
- `W3-LP-094-analysis.md` asked for a reverse **shaped like** a refund, not for publishing the refund event into LHDN

**Mechanism, literally:**

On `GatewayDisputeCreated` (Stripe `charge.dispute.created` today):

1. Skip platform utility / Hub SaaS types (Billing clawback keeps those).  
2. Insert `commerce.Disputes` OPEN.  
3. `TransactionLog.MarkDisputed()`.  
4. If `AmountDisputed > 0`, **publish** `GatewayRefundCompletedIntegrationEvent` with `PaymentRecordId = dispute.Id`, `RefundedAmount = AmountDisputed`, `Id = dispute.Id`.

Billing `GatewayRefundCompletedHandler` then posts:

- `AssetCash` **negative** (cash left the building)  
- `ContraRevenueRefunds`  
- tax reverse  
- a **credit-note number**

No cash left. A dispute is a scheme hold / clawback risk. Stripe has not refunded the buyer through our `IssueRefundAsync`.

Commerce’s **own** refund applier only mutates logs in `REFUND_PENDING`, so the transaction row can stay `DISPUTED` while the **ledger** says refunded. Ops can show a disputed payment and a refund journal for the same capture.

LHDN listener, if a `VALID` tax document exists and the event looks like a **full** refund:

- ≤72h → `CancelTaxDocumentCommand` with reason **“Customer requested refund”**  
- \>72h → credit note

A chargeback is not a customer-requested refund. Cancelling a MyInvois document on dispute-open is legally and operationally wrong (the sale may still be won; the document may need a different treatment; we do not have won/lost events).

**Why P0:** Books lie; a demoed “dispute” can destroy a VALID e-invoice; a later real refund can double-contra if ids differ.

**Fix shape:** stop publishing `GatewayRefundCompleted` from the dispute handler. Give disputes their own ledger event (`DISPUTE_OPEN` / later `WON`/`LOST`) that does **not** mint credit notes and does **not** use “Customer requested refund.” Keep the OPEN row and the DISPUTED badge. Do not auto-cancel the sub (that part of the handler is correct).

---

### P0-4 — CHIP `EventId` is the purchase id for both paid and failed

**IDs:** residual of LP-090 / `LP-PAY-020`.  
**Files:**

- `Modules/Payments/Infrastructure/Gateways/ChipCollectGatewayAdapter.cs` (`eventId = purchaseId`)  
- `Modules/Payments/Infrastructure/Configurations/PaymentConfigurations.cs` — unique `(Provider, EventId)`  
- `ProcessGatewayWebhookCommandHandler.cs` — lookup **EventId first**, then business key  
- `ProcessGatewayWebhookCommandHandler.Idempotency.cs` — business key is `eventType + ":" + gatewayTransactionId` (these **would** differ)  
- `ChipCollectGatewayAdapterTests.cs` — asserts EventId equals purchase id for **both** `purchase.paid` and `purchase.payment_failure`

**Mechanism, literally:**

W0-LP-090 correctly killed `Guid.NewGuid()` fallback (double-fulfill). It replaced it with “stable purchase id.” CHIP then uses that id as **`EventId`**, not only as `GatewayTransactionId`.

Unique index: one CHIP row per purchase id, forever.

Handler:

```csharp
var existing = await _logRepository.GetByEventIdAsync(parsedResult.EventId, config.GatewayType, ...);
if (existing is not null) { await HandleExistingLogAsync(...); return; }
```

`HandleExistingLogAsync` requeues the **old** outbox or republishes the **old** event type. It does not look at the new `EventType`.

CHIP can emit `purchase.payment_failure` then later `purchase.paid` on the same purchase (retry, second instrument, late capture). If failure is stored first, **paid is dropped**. Money is taken; Commerce never sees `PAYMENT_COMPLETED`. The inverse (paid then failure) drops the failure; dunning never starts.

Business keys `PAYMENT_COMPLETED:purch_x` vs `PAYMENT_FAILED:purch_x` would have allowed both **if EventId were unique per envelope**. Tests currently **require** the collision.

**Why P0:** Silent non-fulfillment on a live rail we sell.

**Fix shape:** `EventId = rawEventType + ":" + purchaseId` (or CHIP’s envelope id if they send a distinct one). Keep `GatewayTransactionId = purchaseId`. Keep fail-closed if purchase id missing. Add a test: failure then paid both persist and both publish.

Billplz `EventId = billId` is a related but **weaker** issue: failed-then-paid uses different business keys and, if EventId matches, the same drop applies. Confirm Billplz’s paid/unpaid callbacks against this handler before a Billplz-heavy launch. Stripe uses `evt_…` (unique per event) and is fine.

---

### P1-5 — Team invite (and verify/reset) 404

**IDs:** LP-166 (roles Y; invite path broken), LP-184 (signup Y for the first user only).  
**Files:**

- `Modules/One/Application/EventHandlers/NotificationDispatchDomainEventHandlers.cs`  
- `Modules/One/Infrastructure/Services/OneLinkService.cs` → `App:ClientUrl`  
- `apps/lazuar-api/src/Lazuar.Api/appsettings.json` `ClientUrl: http://localhost:3004`  
- `apps/lazuar-ops/src/modules/workspace/pages/TeamPage.tsx` — toast “Invitation sent”  
- Portal `app/` — **no** `accept-invite`, `verify-email`, `reset-password`  
- Ops `App.tsx` — **no** those routes; `*` → dashboard

**Mechanism, literally:**

Invite email:

```text
{App:ClientUrl}/accept-invite?token={plainToken}
= http://localhost:3004/accept-invite?token=...
```

Portal Next.js: no page → `not-found.tsx`. API `POST /one/workspaces/invites/accept` exists and is unused by any UI.

Same constructor for:

- `/verify-email?email=&token=`  
- `/reset-password?email=&token=`

`W3-LP-166-done.md` flipped roles to Y. It did not ship a landing page. Ops Team is a trap: the button works; the colleague never joins.

**Why P1 not P0:** Seeded `founder@acme.test` can still demo. Multi-seat onboarding cannot. First stranger who invites a bookkeeper will file a support ticket.

**Fix shape:** either (a) add portal routes that call the existing accept/verify/reset APIs, because `ClientUrl` is portal, or (b) point `ClientUrl` at ops and add the routes there. Do not send people to a path the catch-all will swallow.

---

### P1-6 — `Saas:Plan:AmountMyr = 0` (cannot collect Hub SaaS)

**IDs:** LP-004 P (HOLD).  
**Files:**

- `apps/lazuar-api/src/Lazuar.Api/appsettings.json` `"AmountMyr": 0`  
- `CreateSaasCheckoutCommandHandler` — 400 if `AmountMyr <= 0`  
- `W1-LP-004-done.md` — documents this on purpose  
- `PricingPage.tsx` — “free today”

**Why P1:** Honest if we **say** free. A demo that clicks **Pay** on Plan & billing fails. We cannot collect plane S without an operator edit. Do not show that button. Do not pretend Hub Starter is a priced SKU.

---

### P1-7 — No sandbox `VALID` (LHDN is chrome)

**IDs:** LP-110–116, 113, 117.  
**Files:** `W2-LP-111-done.md`, `W2-LP-113-done.md`, `W2-LP-103-done.md`, `W2-LP-110-done.md`, `LhdnStatusPollingJob.cs`, `scripts/lhdn_sandbox/`.

Every Wave 2 done file that wants **Y** says some variant of: *when a sandbox VALID invoice shows VALID on the remounted ops list without SQL* / *when a sandbox VALID PDF shows a scannable MyInvois QR*.

That run is not in the done files. Tests assert handler behavior with fixture `VALID`. IRBM preprod (`Lhdn:PortalUrl` = `https://preprod.myinvois.hasil.gov.my`) is not a substitute for a captured VALID uuid + longId.

Until that artifact exists (screenshot + uuid + document number + QR payload), **Compliance CaaS is an ADR**, not a product. Remounting Invoicing without VALID increases **legal** risk: merchants will send `INV-` PDFs that look official.

**Why P1 (P0 if anyone already told a customer we file):** false legal documents. Keep sales on receipts.

---

### P1-8 — Docs drift as an operational bug

Not a runtime exception. Still P1 because the next hire, the next demo, and the next README paste will re-introduce WhatsApp, hide invoicing, or deny Xendit.

Concrete lying artifacts:

1. README L18 and L74 (Xendit).  
2. README architecture diagram (Vault).  
3. ADR 023 vs `App.tsx` remount.  
4. `00-evaluation.md` “today” column.  
5. `00-checklist-tracker.md` cells listed in §4.4 and the Wave 0/1 backlog.  
6. Tracker note “invoicing unrouted.”  
7. Pricing `Lhdn_credits_live = false` copy vs remounted nav.  
8. Privacy Meta WhatsApp sub-processor.  
9. `00-evaluation.md` / older 007 reports: “Xendit adapter not present.”

Honesty is a Wave 0 feature (`20-sequencing-and-tracker-schema.md` P5). We re-opened it by shipping waves and not editing the constitution files.

---

### P1-adjacent (do not lose)

| Item | Why it is not in the top 8 |
|------|----------------------------|
| MRR is snapshot, not ledger (`LP-161`) | Misleading board metric; not a money-movement bug |
| Utility ledger routed, not in sidebar | Discoverability |
| Ops chat still hidden | Correct |
| No won/lost dispute events | Acceptable once we stop refund-posting |
| Razorpay/Xendit off-session false | Already labeled reminder-only |
| Single API replica / worker safety (`TODO.md`) | Ops, not a demo lie |
| `CommunityPortalView` leftovers | Cleanup |

---

## 6. Refuse list still holds

`plans/007-feats/19-refuse-list-and-adjacents.md` is still the constitution. Waves 0–4 did **not** reverse ADR 015/018/019/021/022. They did not mint a builder, a bouncer, a marketplace, a POS, an MoR, or an ERP.

| Tracker ID | Refuse family | Still true after Waves 0–4? |
|------------|---------------|------------------------------|
| LP-002 | MoR | Yes. Paddle remains System A for *our* seats if/when AmountMyr > 0 — not for guest GMV. |
| LP-003 | Licensed acquirer / hold settlement | Yes. BYOK only. Xendit wrap does not make us Xendit. |
| LP-007 | KYC for *our* acquiring | Yes. |
| LP-039 | BNPL as us | Yes. |
| LP-095 | Settlement / payout reports as if we paid them | Yes. |
| LP-120 | Stripe Tax / Avalara remittance | Yes. |
| LP-156 | SMS product | Yes. Console logger is not SMS. |
| LP-157 | Marketing blasts | Yes. |
| LP-168 | HubSpot CRM | Thin `ClientProfile` only. |
| LP-200 | Website / store builder | Yes. |
| LP-201 | Link-in-bio / funnel | Yes. |
| LP-202 | POS / hardware | Yes. |
| LP-203 | Marketplace / multi-vendor | Yes. |
| LP-204 | Community DRM / Telegram bouncer | Yes. Module gone. |
| LP-205 | Course / membership CMS | Yes. |
| LP-206 | Full accounting / GL replacement | Yes. Ledger ≠ Xero. |
| LP-207 | Crypto / USDC settlement | Yes. Near-term refuse. |
| LP-208 | Escrow / e-sign | **Delay**, not refuse. Still N. Stay off the demo board. |
| LP-209 | GSTN / Coretax | Delay until MyInvois is **sold** (VALID). |
| LP-210 | Affiliates / mass payouts | Delay. |

**Do not** let Xendit’s existence become xenPlatform, Connect, or a take-rate.  
**Do not** let remounted Invoicing become “Lazuar Books.”  
**Do not** let WhatsApp keep-list become Broadcast.  
**Do not** let Aura salon features enter this tracker.

The four tests in `19` (transaction-or-compliance, solo-founder-scale, money-plane, two-sided-market) still filter the backlog. The next ten actions below all pass those tests.

---

## 7. Recommended next 10 engineering actions (order)

This is a **sequence**, not a wishlist. Do not start 8 until 1–4 are in main. Do not start 9 (docs) as a substitute for 1–7.

### 1. Stop the billing-pause starve (P0-1)

In `BillingEngineJob.ClaimDueSubscriptionAsync` (and the in-memory twin), exclude rows where `CollectionPausedUntil > NOW()`. Add a test: two due ACTIVE subs, the older one paused, `RunOnce` charges the younger. Add a test: fifty paused ticks do not block a sibling. Decide resume policy in the same PR: either roll `NextBillingDate` to `max(old, until)` on expiry, or document catch-up and show it in ops. Do not leave “skip + same claim” as the behavior.

### 2. Token-bind public update-payment (P0-2)

Reuse the portal magic-token verifier already used for documents/cancel.  
`GET/POST /public/commerce/checkout/{subId}/…` must require `token` (or a signed `upd_` token minted only into email). Reject bare GUID. Bind `OrganizationId` to the token’s workspace. Change email links and portal hrefs together. Keep LP-173 at **P** until this merges.

### 3. Untangle dispute from refund (P0-3)

Delete the `PublishAsync(GatewayRefundCompleted…)` block in `CommerceGatewayDisputeCreatedHandler`.  
Add `GatewayDisputeOpened` (or let Billing consume `GatewayDisputeCreated` with a **dispute** reference type). No credit-note number. No LHDN cancel with “Customer requested refund.” Keep OPEN row + DISPUTED badge. Tests: Stripe dispute does **not** insert `CONTRA_REVENUE_REFUNDS`; LHDN handler does not fire.

### 4. CHIP EventId per envelope (P0-4)

Set `EventId` to something unique per CHIP callback (`event_type + ":" + purchaseId` is enough). Keep fail-closed without purchase id. Test failure-then-paid. Re-read Billplz bill-id EventId in the same PR; if unpaid then paid share EventId, apply the same split.

### 5. Invite / verify / reset landing pages (P1-5)

Add the three portal routes that `ClientUrl` already advertises, or move `ClientUrl` and add them on ops. Wire to existing `POST /one/workspaces/invites/accept`, `POST /one/auth/verify-email`, `POST /one/auth/reset-password`. Manual test: Team invite → email → accept → member appears. Until then, hide the Invite button or show “copy accept API token” as a support escape.

### 6. One sandbox MyInvois VALID (P1-7)

Operator run, not more poller code: tenant with real sandbox TIN/BRN, legal profile saved, B2B product, pay, poll until `VALID`, screenshot ops Sales documents + QR. Store the evidence next to `scripts/lhdn_sandbox/`. If IRBM sandbox cannot VALID our unsigned 1.0, **that** is the next engineering fact — do not paper it with more UI. Only then may marketing say “LHDN at the point of sale.”

### 7. Hub SaaS price is an operator switch (P1-6)

Keep `AmountMyr=0` in repo if the product is free. Disable or hide the Pay button when `<= 0` (API already 400s; UI should not offer it). When we want to charge, set AmountMyr in **deploy** config, not in a demo. Do not conflate credit top-up with Hub SaaS.

### 8. Honesty pass on the eight lying artifacts (P1-8)

Single docs PR, no product features:

- README L18/L74: Xendit is a **live hosted-invoice wrap**, reminder-only, not e-mandate.  
- README diagram: remove Vault.  
- ADR 023: add a status banner “Superseded in UI by Wave 2 remount; VALID still the compliance go-live gate” **or** write ADR 024.  
- `00-evaluation.md`: rewrite §1 and §4 “today” table; keep ICP and refuse.  
- `00-checklist-tracker.md`: flip only HOLD/STALE cells from §4.4; do **not** flip OVERSOLD to Y. Delete or date the Wave 0/1 backlog.  
- Pricing: `Lhdn_credits_live` should mean “VALID loop sold,” not “nav hidden.”  
- Privacy: remove Meta until Meta is called.

### 9. MRR label + utility ledger nav

Dashboard copy: “Committed snapshot MRR (ACTIVE, not paused, not past due). Not ledger RevRec.”  
Either link `/workspace/ledger` in the sidebar or unroute it. Phantom routes are how ADR 023 started.

### 10. Only then: keep-list or extra rails

WhatsApp Meta Cloud (`LP-074`/`LP-155`) and Xero (`LP-121`) are still ADR 021 keeps. They are **not** more important than P0-1–4. Extra rails (Fiuu, Midtrans) wait on a named tenant. Do not open a Wave 5 while pause starve and GUID checkout exist.

---

## 8. Suggested demo script (does not require lying)

**Audience:** one Malaysian SaaS/agency founder. Time: 20 minutes. Environment: local seed (`founder@acme.test` / `Password123!`, workspace `acme`) or a throwaway test tenant with **test** Billplz or Stripe keys. Public callback base must be real HTTPS if Billplz (`App:ApiBaseUrl`).

**Spoken opening (30 seconds):**

> We are not HitPay and not Stripe. You keep your Billplz or Stripe account. We run the checkout link, the subscription state, the reminder emails, and a ledger. Receipts are receipts. We are not filing MyInvois in this demo. We do not send WhatsApp.

**Do not open:** Invoicing (unless you only show a receipt and say “not e-invoice”), Disputes, Pause collection, Team Invite, Plan & billing Pay, dunning WhatsApp step, `/update-payment` as a security story, ops chat.

### Step 1 — Pricing honesty (1 min)

Open `http://localhost:3003/pricing`.

Say: “0% of your sales. Software is free today. Credits are for e-invoice later. WhatsApp is not connected.”  
If the page still says “LHDN merchant UI is not live,” **say the correction out loud**: “The invoicing screens exist; we have not proven a VALID submission. We will not use them in this demo.”

### Step 2 — Sign in and BYOK (2 min)

Log in as `founder@acme.test`.  
Workspace → Payment Gateways. Show one connected gateway. Say the collection mode:

- Stripe/CHIP: “We can charge the card again next month.”  
- Billplz/Xendit/Razorpay: “Each month we email a pay link. We cannot silent-debit FPX.”

Do not add Xendit in the demo unless those keys are in front of you and you repeat “hosted invoice, not auto-debit.”

### Step 3 — Create a checkout link (3 min)

Commerce → Checkout Links → create a **monthly** product at a small test amount.  
Show hop-1 EN/BM toggle. Copy the portal URL.  
Open incognito → pay with the **test** instrument.  
Land on success and wait until status is paid (poll). Say: “We do not trust the redirect.”

### Step 4 — Receipt, not e-invoice (1 min)

Open the transaction. Download the Official Receipt if present. Point at the footer: not an LHDN e-invoice.  
Do not open Sales documents / QR / TIN validation.

### Step 5 — Subscription + portal (3 min)

Commerce → Subscribers. Show `ACTIVE`, next bill, collection mode badge.  
Send a magic link (or use the seeded buyer path). Portal: list, **Cancel at period end**, Keep. Say: “Access stays until the date we already collected.”  
Do **not** click Update payment and call it secure. If you must show it: “This is the page the email opens. We still need to put it behind the same magic token as the portal.”

### Step 6 — Email dunning, not WhatsApp (4 min)

Open Dunning Campaigns. Show EMAIL steps and AUTO_CHARGE skipped on Billplz.  
If you have a vaulted Stripe test card that declines: trigger a failed renewal (or walk a past-due fixture). Show the email in Resend/logs with `{{renewal_link}}` / update-payment URL.  
If you cannot decline a card today, **do not fake WhatsApp**. Show the campaign JSON and the template wiki instead.

### Step 7 — Refund honesty (2 min)

On a Stripe/CHIP test payment: refund from the modal. Show `REFUND_PENDING` then refunded.  
On Billplz: show **mark-refunded** and say “we cannot call Billplz refund; your SOP is the dashboard.”

### Step 8 — Developer unlock (3 min)

Developer → API keys → mint a **test** key.  
Show outbound webhook endpoint + a delivery log.  
Optional: `examples/hub-cashier-next` create-checkout.  
Say: “Your app unlocks when this signed webhook arrives. We are not your course player.”

### Step 9 — Close (1 min)

Repeat the refuse line: “We will not build your website, your Telegram bouncer, or take 2%. When MyInvois VALID is proven, the same ledger becomes the e-invoice. Until then you get a receipt and a subscription that actually knows what day it is.”

**If they ask for LHDN:** open Legal & Billing, show the fields, show Sales documents **empty or PENDING**, and say “this is the next gate, not this meeting.”

**If they ask for WhatsApp:** “Not connected. Email works. We will not log to a console and charge you.”

**If they ask for pause:** “Not in this build. We have a flag; we will not put it on a live book until the billing job skips it without blocking everyone else.”

---

## 9. What a solo founder is allowed to want next

Allowed: P0-1 through P0-4, invite pages, one VALID artifact, README/ADR/tracker honesty, then WhatsApp **or** Xero **or** a named extra rail.

Not allowed: rebuilding 023’s hide, selling VALID, selling WhatsApp, becoming HitPay’s suite, flipping OVERSOLD tracker cells to Y because a done file asked.

The codebase is a **credible CaaS** with remounted compliance chrome and four money/security defects that make parts of Wave 3/4 **unsafe to demonstrate**. The market sentence in `00-evaluation.md` §9 is still the right company:

> We beat them only on the intersection. Build the intersection.

The intersection is: **honest FPX/card checkout + honest collection mode + email recovery + receipts + (later) MyInvois VALID from the same ledger + webhooks.** Everything in §5 that breaks that sentence is the remaining work. Everything in §6 is still someone else’s company.
