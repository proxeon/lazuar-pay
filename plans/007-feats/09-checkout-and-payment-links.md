# 09 — Hosted checkout and payment links

**Program:** `plans/007-feats` — competitor features vs Lazuar Pay  
**Date:** 2026-08-16  
**Status:** Analysis only — **no product code from this file**  
**Scope:** Hosted checkout and payment-link *buyer journeys*: Stripe Payment Links + Checkout, Billplz Payment Forms / Catalog, CHIP payment pages / Collect links, HitPay payment links, Xendit invoices / payment links, Gumroad, Payhip, ThriveCart / SamCart, Polar checkout, PayPal links — scored against **Lazuar Pay’s own portal checkout**, not against Aura’s salon `/book` wizard.  
**Author role:** staff product / payments analyst for Lazuar Hub CaaS (Commerce public buy links + M2M cashier) and the boundary with Aura guest money (System B).

**This file is not** a Malaysia informal-stack dossier. Informal WhatsApp + Excel + DuitNow QR + Billplz paste is a sibling job. This file is the **hosted checkout product** those informal operators paste. The two jobs share Billplz as a rail; they do not share a buyer surface.

**Standing constraints (do not contradict):**

- Lazuar Pay is **BYOK software**, not a Merchant of Record and not an acquiring bank.
- Guest money (System B / Lazuar Pay / Billplz) is **not** SaaS money (System A / Paddle).
- Buyer money on Billplz / Stripe / CHIP (tenant keys) is not Lazuar's SaaS fee.
- Production guest fulfillment is **not** claimed until a sandbox three-book soak.
- `HUB_PAYMENTS_DEFAULT_NEW_ORGS_TO_HUB` stays **false** until that soak.
- Do not delete Paddle or legacy K2 without gates.
- Do not become a website builder, marketplace, gym OS, POS, ERP, or medspa EMR to “match competitors.”
- Do not become a marketplace take-rate business.
- Do not mix Paddle and guest.
- Polar, Gumroad, Payhip, and PayPal *are* MoR-shaped. Copying their tax-inclusive “we take 5–10% and file VAT” is a company-shape mistake, not a checkout feature.
- Do not rebuild ThriveCart / SamCart order-bump funnels inside Hub to “match AOV.” That is a different company.
- Aura (salon) is a **customer** of Hub, not a competitor.
- Wrap rails (Stripe, Billplz, CHIP, later Xendit) — do not rebuild acquiring.

**Primary sources (read, not summarized away):**

| Source | Absolute path | Role |
|--------|---------------|------|
| Portal checkout page | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-portal/src/app/[tenantSlug]/checkout/[productSlug]/page.tsx` | SSR product fetch + auth cookie + `CheckoutView` |
| Blind checkout layout | `…/checkout/[productSlug]/layout.tsx` | Sticky “Powered by Lazuar” chrome; no tenant brand |
| Success page | `…/checkout/[productSlug]/success/page.tsx` + `CheckoutSuccessView.tsx` | Polls status; never unlocks on `?payment=success` alone |
| Custom quote route | `…/app/[tenantSlug]/pay/[sessionId]/page.tsx` | **`notFound()` — MVP-HIDE** |
| `CheckoutView` | `apps/lazuar-portal/src/modules/checkout/components/CheckoutView.tsx` | Coupon, quantity state, PWYW display, cancel banner |
| `CheckoutForm` | `…/CheckoutForm.tsx` | Identity + optional address; tax/company **commented out** |
| `OrderSummaryCard` / `PromoCodeInput` | same folder | PWYW input, promo apply/remove |
| `QuoteView` | same folder | Proforma invoice UI — **orphaned** by pay-route hide |
| Portal API | `…/modules/checkout/lib/api.ts` | `GET product`, `GET validate-coupon`, `POST /public/commerce/checkout`, `GET status` |
| TypeSpec public commerce | `packages/api-spec/modules/commerce/public-routes.tsp` | Unauthenticated buy-link surface |
| Checkout / product / coupon / custom models | `packages/api-spec/modules/commerce/models/{checkout,product,coupon,custom-checkout}.tsp` | Contracts vs live UI |
| `InitiateCheckoutCommandHandler` | `apps/lazuar-api/Modules/Commerce/Application/Commands/InitiateCheckoutCommandHandler.cs` | Two-hop: CRM + session + gateway URL |
| `PublicCheckoutEndpoints` | `…/Infrastructure/Endpoints/PublicCheckoutEndpoints.cs` | POST checkout; status **never mints token** |
| `CheckoutSessionCashier` | `apps/lazuar-api/Modules/Payments/Application/Services/CheckoutSessionCashier.cs` | Shared Billplz / Stripe / CHIP / Razorpay generate |
| Adapters | `…/Infrastructure/Gateways/{Billplz,Stripe,ChipCollect}GatewayAdapter.cs` | What the buyer actually sees on hop 2 |
| Custom checkout create | `CreateCustomCheckoutCommandHandler.cs` | Admin line-item payment link |
| Session expiry | `CheckoutSessionExpiryJob.cs` | 5-min loop; expire OPEN + release coupon reserve |
| Paid fulfillment | `GatewayPaymentCompletedIntegrationEventHandler.OpenCheckout.cs` | Coupon confirm; Order / Subscription; `payment_link.paid` |
| M2M cashier | `packages/api-spec/modules/payments/{routes,models}.tsp` + `CreateIntegrationCheckoutCommandHandler.cs` | Ad-hoc amount → `checkout_url` |
| Hub cashier sample | `examples/hub-cashier-next/` | Teachable redirect + signed webhook unlock |
| ADR 019 CaaS pivot | `docs/architecture-decision-log/019-checkout-as-a-service-pivot.md` | Why Lazuar is a link, not a site builder |
| ADR 023 UI lobotomy | `docs/architecture-decision-log/023-pure-caas-mvp-ui-lobotomy.md` | Why TIN / quotes / tax invoice are hidden |
| Competitor public docs (2026-08-16) | Stripe Payment Links / Checkout / abandoned carts; Billplz Catalog + payment-link blog; CHIP Collect + payment-links; HitPay MY payment-link guides; Xendit MY payment links; Polar checkout links + embed; PayPal.Me / Payment Links; Gumroad / Payhip help; ThriveCart / SamCart marketing | Feature facts below |

**How to read this file.**  
Sibling `13-payments-refunds-rails.md` (when it lands) owns **money physics** (BYOK vs MoR vs POS). This document answers a different question: **what does a Malaysian buyer *see and feel* when they tap a payment link**, and how does Lazuar’s portal compare to the products a creator, indie hacker, or salon owner will actually paste into WhatsApp.

Letter collision reminder (do not mix):

| Surface | Who pays whom | URL the buyer opens | Who hosts the *payment-method* UI |
|---------|---------------|---------------------|-----------------------------------|
| **Commerce product buy link** | Buyer → creator, via creator’s BYOK | `/{tenantSlug}/checkout/{productSlug}` on `lazuar-portal` | Hop 1 = Lazuar form. Hop 2 = Billplz bill / Stripe Checkout / CHIP purchase |
| **Commerce custom payment link** | Buyer → creator, ad-hoc line items | `/{tenantSlug}/pay/{sessionId}` | **Portal route is `notFound()`.** Backend + QuoteView still exist. |
| **M2M cashier** | Guest → salon / integrator | Integrator’s `checkout_url` (usually gateway host) | **No Lazuar identity page.** Direct Billplz / Stripe / CHIP. |
| **Update-payment / arrears** | Existing subscriber | `/{tenantSlug}/update-payment/{subId}` | Lazuar arrears card → hop 2 gateway |
| **Aura `/book`** | Guest → salon (System B) | Aura storefront | Aura wizard, then Hub M2M checkout_url |

This file scores the **first four**. Aura `/book` is an Aura product. Informal WhatsApp+QR is not this checkout.

---

## Method

### What was inspected in Lazuar Pay (live source, 2026-08-16)

I read the portal checkout vertical slice end-to-end, then the two backend products that can emit a hosted URL, then the teachable sample.

**Portal routing and chrome**

- `apps/lazuar-portal/src/app/layout.tsx` — `html lang="en"`, Geist + Geist Mono, metadata title “Lazuar Portal”, global footer with Lazuar Terms / Privacy / Refund (not the creator’s).
- `apps/lazuar-portal/src/app/page.tsx` — lock-icon landing; no catalog; “use the magic links sent to your email.”
- `apps/lazuar-portal/src/app/[tenantSlug]/layout.tsx` — passthrough. ADR 017 promised “Fetches Tenant Theme/Colors.” **It does not.**
- `apps/lazuar-portal/src/app/[tenantSlug]/checkout/[productSlug]/layout.tsx` — “Blind Checkout”: zinc-50 page, sticky header, padlock + “Powered by Lazuar”, **no product name, no logo, no language switch**.
- `apps/lazuar-portal/src/app/[tenantSlug]/checkout/[productSlug]/page.tsx` — server `GET /public/commerce/{tenantSlug}/products/{slug}` with `revalidate: 60`; optional `lazuar_auth` cookie → `/one/auth/me` + entitlements (admin-of-this-tenant flag); `?cancelled=true` banner.
- `apps/lazuar-portal/src/app/[tenantSlug]/checkout/[productSlug]/success/page.tsx` — same product fetch; `CheckoutSuccessView` behind `Suspense`.
- `apps/lazuar-portal/src/app/[tenantSlug]/pay/[sessionId]/page.tsx` — **`notFound()`**. QuoteView import is commented `[MVP-HIDE]`.
- `apps/lazuar-portal/src/app/[tenantSlug]/update-payment/[subId]/page.tsx` — arrears card; server action `POST /public/commerce/checkout/{subId}/update-payment` then `redirect(url)`.
- `apps/lazuar-portal/src/app/[tenantSlug]/portal/page.tsx` — magic-token buyer dashboard; tax-invoice download `[MVP-HIDE]`; cancel plan form exists.
- Legal pages under `src/app/legal/{terms,privacy,refund}/` — platform-as-processor, creator-as-MoR, “all sales are final unless the creator said otherwise.” English only. Last updated June 2026.

**Checkout module (the thing this file is about)**

- `CheckoutView.tsx` — client state: coupon, quantity (default `1`), PWYW `customPrice`, discount, cancelled/global errors. Coupon math = `(validate.discount_amount / product.price) * quantity`. Changing quantity or PWYW **removes** the coupon. Zero-amount success `router.push(.../success)` **without** `?sub_id=`.
- `CheckoutForm.tsx` — name + email required; phone if `requires_phone`; address block if `requires_address` or `requires_tax_id`; company / TIN block is **commented `[MVP-HIDE]`** and the submit payload **hard-sets** `company_name` / `tax_id` to `undefined`. Quantity is sent but **there is no quantity control in the JSX**. Submit → `POST /public/commerce/checkout` → `window.location.href = result.url` or zero-amount callback. CTA copy: “Proceed to Payment” / “Securing Data…”. Legal line points at **Lazuar** terms, not the creator’s.
- `CheckoutLayout.tsx` — `flex-col-reverse` so on mobile the **order summary is above the form**. Desktop: form left, 380px summary right.
- `OrderSummaryCard.tsx` — item name, optional audience (never set by View), subtotal, PWYW number input (disabled once coupon applied), discount row, promo slot, “Total Due Today”. **No quantity stepper. No interval line. No tax line. No shipping. No method logos.**
- `PromoCodeInput.tsx` — uppercase mono field; Apply / Remove; client-side error text.
- `IdentityBanner.tsx` — only if a cookie session exists. Three skins: guest-mode, workspace-admin, logged-in member. Lets the buyer toggle guest vs “Use my Lazuar account.”
- `QuoteView.tsx` — full proforma (logo, TIN, SSM, line items, expiry, HMAC draft PDF, “TIN collected at checkout” amber note). **Unreachable** from the pay route.
- `types.ts` — `CheckoutContext` / `CheckoutAuthContext`. No locale. No variant. No bump.
- `lib/api.ts` — openapi-fetch to `NEXT_PUBLIC_API_URL`, `credentials: "include"`. Coupon invalid → generic “Invalid promo code.” Checkout error → `error.detail`. Status poll returns whatever the API sends.

**Commerce public API and handlers**

- `GET /public/commerce/{tenantSlug}/products/{slug}` — active product only; 404 otherwise.
- `GET /public/commerce/{tenantSlug}/validate-coupon?code&product_slug` — unit-price discount preview. Does **not** take quantity. Failures return `200 { is_valid: false }` (not 404).
- `POST /public/commerce/checkout` — body `PublicCheckoutRequestDto`. Handler: resolve tenant slug; **require Communications email config**; resolve/create CRM profile; optionally lock+validate+**Reserve** coupon; persist `CheckoutSession` 24h; if net 0 → `ProcessZeroAmountCheckout`; else `GenerateCheckoutSessionQuery` → Payments cashier → **string URL**. Custom path (`session_id` set) hard-codes currency **MYR**, product name “Custom Payment Request”, success URL `/{slug}/checkout/custom/success?sub_id=…` (**no such portal route**).
- `GET /public/commerce/{tenantSlug}/checkout/{sessionId}/status` — org-bound; `COMPLETED` or `PENDING`; **`Token` always `null`**. Legacy query-param path same rule.
- `GET /public/commerce/{tenantSlug}/custom-checkouts/{sessionId}` — still implemented; portal does not call it.
- `GET /checkout/{subId}/arrears` + `POST …/update-payment` — subscription price, not arrears ledger; `setupFutureUsage: true`.
- `EnforceCheckoutConfiguration` **does** run server-side (phone / tax id / address). Ops product form **always posts `requires_tax_id: false`**, so the hidden TIN fields and the server rule cannot currently fire from a product created in Ops.

**Payments hop 2 (what the buyer pays on)**

- Cashier resolves: preferred gateway → first active tenant config → legacy `"BILLPLZ"` last resort (Commerce) / `PAYMENTS_NOT_CONFIGURED` (M2M `requireActiveGateway: true`).
- Billplz adapter: `POST /api/v3/bills` with collection_id, email, amount in sen (truncate), `callback_url` + `redirect_url` = Commerce success URL. Buyer lands on **Billplz hosted bill**. Method mix is the **collection’s** mix (FPX, wallets, cards, BNPL if the merchant enabled them). Lazuar does not render those buttons.
- Stripe adapter: Checkout Session `mode=payment`, one line item, `Quantity` from request, `SuccessUrl` / `CancelUrl`, metadata on session **and** PaymentIntent. Apple Pay / Google Pay / Link appear **if Stripe Checkout is the hop-2 page** and the Stripe account has them. Lazuar does not enable `allow_promotion_codes`, `optional_items`, `after_expiration.recovery`, custom domain, or Embedded Checkout.
- CHIP adapter: `POST https://gate.chip-in.asia/api/v1/purchases/` with `brand_id`, one product, success/failure/cancel redirects, optional `force_recurring`. Buyer lands on **CHIP hosted purchase**. FPX bank list, DuitNow QR, wallets, cards, BNPL, even Google Pay / stablecoins are **CHIP brand configuration**, not Lazuar UI.
- M2M: `POST /integrations/payments/checkouts` — amount ≥ RM 2.00 for MYR (`CheckoutAmountRules.MyrMinimum`), absolute success/cancel URLs, idempotency, opaque metadata. Returns `checkout_url`. No coupon, no CRM, no portal, no email-config gate.

**Fulfillment after pay**

- Hop 1 (gateway → Hub): signed processor webhook → `GatewayPaymentCompleted`.
- Commerce open session: confirm coupon reserve; `session.Complete()`; if `custom_payment_link` → tx log + outbound `payment_link.paid` **and return (no Order)**; else create Subscription (interval ≠ `one_time`) or Order (`one_time`) + lifecycle events.
- Communications: `OrderCompletedDigitalDeliveryHandler` sends “Digital Product Delivery” if that template exists; `fulfillment_url` is the **portal URL**, not an R2 file. `DocumentPublishedIntegrationEventHandler` emails Official Receipt / Quotation Ready with HMAC document link — **tax-invoice download is MVP-HIDE in portal**.
- Success page: 10 attempts × 2.5 s of `GET …/status`. ACTIVE/COMPLETED → “Order Complete!” + “Go to Dashboard”. Token from response is **always null**, so the dashboard link is `/{slug}/portal` without `?token=`. Missing `sub_id` → “Invalid Session”. Timeout → “Processing Payment” + email hope. Zero-amount path from View **does not pass `sub_id`**, so a 100% coupon can land on Invalid Session.

**Hub cashier sample (`examples/hub-cashier-next`, port 3020)**

- Local order in `.data/` → server `POST /integrations/payments/checkouts` → `window.location = checkout_url`.
- `/pay/success` polls **local** order every 2 s and **never** marks paid. Copy is explicit: success_url is not fulfillment.
- `/pay/cancel` marks local order cancelled unless already paid via webhook.
- Fake webhook script exists for offline demos. README: not production; no Stripe/Billplz SDK; no `@repo/*`.

**Ops merchant surface (what the creator configures)**

- `ProductForm.tsx`: name, slug, FIXED | PWYW, interval `one_time` | `mo` | `yr`, price MYR, min price if PWYW, gateway select, active gated on Resend, require address, require WhatsApp, **`requires_tax_id: false` hardcoded**, fulfillment targets as leftover textarea (real webhooks live under Developer).
- Billplz product warning: cannot vault; no silent auto-charge.
- Coupons: PERCENTAGE | FIXED, max uses, min price, product scope, expiry. Reserve / confirm / release domain is real. Expiry job releases reserves.
- Custom checkouts / Quotes: create + copy `/{slug}/pay/{id}` + mark-paid. Copy target is the **hidden** portal route.

### What was inspected in competitors (public product / help, 2026-08-16)

No signed-in partner dashboards. No live sandbox purchase on this pass. Treat competitor rows as **publicly documented capability**, not a reverse-engineered build.

| Competitor | What I opened |
|------------|----------------|
| **Stripe Payment Links + Checkout** | stripe.com/payments/payment-links, stripe.com/payments/checkout, docs.stripe.com/payment-links/customize, docs.stripe.com/payment-links/promotions, docs.stripe.com/payments/checkout/abandoned-carts (hosted variant) |
| **Billplz Payment Forms / Catalog** | main.billplz.com, main.billplz.com/blog/insights/payment-link-malaysia-for-business (2025-12-01), catalog.billplz.com |
| **CHIP Collect / payment pages** | chip-in.asia/collect/payments, chip-in.asia/collect/payment-links (interactive builder: qty, PWYW, custom fields, custom slug, FPX bank grid on first paint) |
| **HitPay payment links** | hitpayapp.com/blog/what-is-a-payment-link-sea-guide-hitpay (2026-06-02), how-to-create-send-payment-link-malaysia (2026-05-20), payment-links-pre-filled-amounts (2026-06-08) |
| **Xendit invoices / payment links** | xendit.co/en-my/products/payment-links/, help.xendit.co customize language (EN / Bahasa Indonesia / browser) |
| **Gumroad** | gumroad.com/help/article/128-discount-codes, /191-a-guide-to-buying-on-gumroad |
| **Payhip** | help.payhip.com customizable checkout (2026-03 / changelog 2026-06), custom checkout questions, upsells / cross-sells / custom domain |
| **ThriveCart / SamCart** | thrivecart.com and samcart.com compare pages 2026; order-bump / 1-click upsell help |
| **Polar** | polar.sh/docs/features/checkout/links, polar.sh/docs/features/checkout/embed |
| **PayPal** | paypal.com/us/business/accept-payments/payment-links, PayPal.Me FAQ, Guest Checkout help |

### Scoring vocabulary

Used in the scorecard and dossiers. Same letters as the program tracker Layer A, plus an honesty tag for Lazuar.

| Mark | Meaning |
|------|---------|
| **Y** | Production-grade, marketed, a buyer can hit it without a hidden flag |
| **P** | Slice, processor-dependent, contract-only, or honesty gap |
| **N** | Not a product job on that stack |
| **—** | Not applicable to that category |
| **X** | Trap if Lazuar copied it (company-shape, MoR, or funnel OS) |

Lazuar depth (separate): `shipped` · `partial` · `hidden` · `ghost` · `none`.

- **shipped** — buyer can complete the job on the live portal or M2M path.
- **partial** — job exists on one hop, one gateway, or with a lie (PWYW display ≠ charged amount).
- **hidden** — code exists, `[MVP-HIDE]` / `notFound()` / Ops toggle off.
- **ghost** — TypeSpec / state / DTO field with no buyer control or no server use.
- **none** — not modeled.

### What this method refuses to claim

- I did not time a Lighthouse run or a real 4G first-contentful-paint. “Time to first pixel” is an **architecture** score (SSR vs SPA, one hop vs two, webfonts, whether payment-method buttons exist on first paint), not a lab number.
- I did not complete a live Billplz / Stripe / CHIP sandbox pay on 2026-08-16 for this file. Hop-2 method grids are from adapter payloads + processor public docs + CHIP’s own payment-link demo page.
- I did not claim Aura production guest fulfillment. M2M + Commerce code paths **exist**. Soak is still a Wave 0 gate.
- I did not treat Stripe Checkout Apple Pay as a Lazuar feature. It is a **Stripe account** feature that appears only if hop 2 is Stripe.

### Three products, one brand — do not flatten them

Every later section names which Lazuar product it means.

```text
A. COMMERCE BUY LINK (this file’s primary audit)
   Instagram / WhatsApp / Framer button
        → GET /{tenant}/checkout/{slug}     (Lazuar hop 1: identity)
        → POST /public/commerce/checkout
        → 302/assign window.location        (processor hop 2: methods)
        → success?sub_id=                   (poll; webhook is SSoT)

B. COMMERCE CUSTOM LINK (lobotomized)
   Ops “copy quote link”
        → /{tenant}/pay/{sessionId}         (notFound in portal)
        → QuoteView would POST checkout
          with session_id + product_slug="custom"
        → hop 2 Billplz/CHIP/Stripe
        → success URL points at missing /checkout/custom/success

C. M2M CASHIER (Aura / sample / any app)
   Integrator server
        → POST /integrations/payments/checkouts
        → checkout_url is already hop 2
        → integrator success_url polls local
        → signed payment.completed unlocks
```

ADR 019 says Lazuar is the **fulfillment engine behind other people’s pixels**. That is why hop 1 exists at all: collect CRM + coupon + address *before* the processor page, then unlock portal / WhatsApp / webhook *after*. The conversion cost of that extra page is the entire competitive problem this file is about.

---

## Buyer journey map

### The job the buyer has

A Malaysian (or SEA, or global-digital) buyer has already decided to pay, or is one tap from deciding. They are in WhatsApp, Instagram, Telegram, email, or a landing page. They need:

1. To **see** that the link is the right merchant and the right amount, on a phone, in under two seconds.
2. To **pay** with a method they already have — FPX (Maybank2u / CIMB Clicks / RHB), DuitNow QR, Touch ’n Go, GrabPay, a Visa/Mastercard, Apple Pay if the phone offers it — **without creating an account**.
3. To **know it worked**, and to get the thing (file, access, receipt, appointment hold) without refreshing hope.

Everything else (promo codes, bumps, TIN, locale, branding) is either conversion insurance or back-office. Competitors differ on how much insurance they bolt onto step 1–2.

### Canonical buyer timelines (what “good” looks like in 2026)

**Informal / Billplz-native (the Malaysia default)**

```text
WA bubble: "deposit RM50 ya https://www.billplz.com/bills/xx"
  → 1st pixel: Billplz bill. Amount, merchant collection name, FPX + wallet buttons.
  → tap Maybank2u → bank page → approve
  → Billplz paid screen → maybe redirect
  → seller sees dashboard / email; buyer gets Billplz receipt
Time to first payment-method button: first paint.
Account: none.
Fulfillment: human (seller texts "ok nampak").
```

**Stripe Payment Link (global creator / SaaS)**

```text
https://buy.stripe.com/…  or  pay.example.com/…
  → 1st pixel: Stripe Checkout. Logo, amount, Apple Pay / Link / card / (local methods if enabled).
  → optional promo field, optional upsell toggle, qty stepper
  → pay → Stripe success or merchant success_url
  → Stripe receipt email; merchant webhook
Time to first method: first paint.
Account: guest Customer object by default.
Abandoned: checkout.session.expired + recovery URL (if enabled).
```

**CHIP Collect payment link (Malaysia, no-code)**

```text
https://pay.chip-in.asia/kelasbinawebsite   (custom slug)
  → 1st pixel: CHIP page. Merchant logo, product(s), qty, FPX bank grid, cards, DuitNow QR.
  → name / email / phone / address if the link requires them — ON THE SAME PAGE as methods
  → pay → CHIP thank-you or merchant redirect
Time to first method: first paint (demo page shows Maybank tile above the fold on mobile).
Account: none.
```

**HitPay / Xendit payment link (SEA PSP)**

```text
hitpay / xendit hosted invoice
  → 1st pixel: branded amount + every enabled rail (FPX, DuitNow, TnG, GrabPay, cards, BNPL)
  → pay → thank-you + merchant notification
Same shape as CHIP/Billplz: one hop, methods on first paint.
```

**Gumroad / Payhip / Polar (creator MoR)**

```text
product URL or overlay / embed
  → email + pay (card / PayPal / Apple Pay) on the same surface or overlay
  → instant file / license / Discord / GitHub
  → MoR receipt + tax
One hop or overlay. No FPX. USD-first (Polar is Stripe-under-the-hood).
```

**ThriveCart / SamCart (funnel OS)**

```text
sales page → checkout (order bump checkbox)
  → pay (Stripe/PayPal/Apple/Google)
  → 1-click upsell page (no re-enter card)
  → thank-you / membership
Optimised for AOV, not for MY rails.
```

**PayPal.Me / PayPal Payment Links**

```text
paypal.me/Name/50  or  ncp/payment/…
  → PayPal wallet or guest card
  → PayPal receipt
No FPX. Guest card is a setting, historically flaky outside the US.
```

### Lazuar Commerce buy-link timeline (as implemented)

```text
Creator share: https://hub.lazuar.com/portal/{tenant}/checkout/{slug}
                (or localhost:3004 / custom basePath)

[Hop 0 — DNS + Next]
  Root layout: Geist from Google Fonts, lang=en, Lazuar footer.
  Checkout layout: sticky "Powered by Lazuar".
  page.tsx: GET product (60s cache) + optional /one/auth/me.

[Hop 1 — Lazuar identity. NO payment methods.]
  Mobile: Order Summary (name + MYR amount + promo field) then Account Details.
  Fields always: Full Name, Email Address.
  Optional: WhatsApp Number, Billing Address (street, city, postcode, state, country code).
  Hidden: company + TIN (commented).
  Ghost: quantity (state=1, sent, no stepper).
  Partial lie: PWYW input changes the summary, not the charge.
  CTA: "Proceed to Payment" → POST /public/commerce/checkout
       Gates: workspace must have Resend; gateway must be active.
       Failures: amber/red banners; gateway-missing is rewritten to
                 "This creator is currently updating their payment settings."

[Hop 2 — Processor hosted page]
  Billplz: bill URL. Methods = collection.
  Stripe: checkout.stripe.com. Methods = Stripe account (card, wallets, …).
  CHIP: gate.chip-in.asia checkout_url. Methods = brand.
  Buyer may re-enter name/email. Billplz name is extracted from the
  email local-part (GatewayCommon.ExtractName), not the form's Full Name.

[Hop 3 — Return]
  success?sub_id={CheckoutSession.Id}
    → poll GET …/status up to ~25s
    → COMPLETED ⇒ "Order Complete!" + Dashboard without magic token
    → else TIMEOUT ⇒ "check your email"
  cancel?cancelled=true
    → back to hop 1 with amber "Payment was cancelled or failed."

[Hop 4 — Actual fulfillment, not the URL]
  Gateway webhook → Hub → Commerce Order/Sub + outbound webhooks
                 → Communications email / WhatsApp templates
  Success HTML is not entitlement.
```

**Time to first *Lazuar* pixel:** Next.js SSR of a small form. Should be fine on 4G *if* Geist and the API product GET are warm. There is no image LCP on hop 1 (no product photo).

**Time to first *payment-method* pixel:** **one extra round trip + one extra origin** after the buyer has already typed name and email. That is the conversion hole. Stripe / CHIP / HitPay / Billplz Catalog / Xendit do not ask the buyer to submit a form *before* they see Maybank or Apple Pay.

### Lazuar M2M / hub-cashier timeline (closer to a payment link)

```text
Integrator /pay form (email + amount) 
  → POST integrator /api/checkout
  → POST Hub /integrations/payments/checkouts
  → 302 to Billplz/Stripe/CHIP
  → success_url polls integrator order (not paid)
  → Hub webhook payment.completed → paid
```

This **is** a payment link. It is also **not** the portal CheckoutForm. Scoring “Lazuar checkout” as if it were only M2M would make hop-1 gaps disappear. Scoring it as if it were only hop 1 would hide that Aura already skips hop 1.

### Field-by-field buyer map (Commerce hop 1)

| Buyer need | Where it lives on hop 1 | Honest status |
|------------|-------------------------|---------------|
| Who am I paying? | Product **name** in summary. No logo. Header says Lazuar. Footer says Lazuar. Terms are Lazuar’s. | Weak merchant recognition |
| What am I buying? | `product.name` only. No description, image, variant, interval copy (“billed monthly”), fulfillment promise. | Thin |
| How much? | `currency` + `price` × qty. PWYW input is cosmetic. No SST line. “Total Due Today” even for `mo`/`yr`. | Partial; subscription honesty gap |
| Can I change qty? | State + API. **No control.** | Ghost |
| Promo? | Visible field. Validate then reserve on submit. URL cannot prefill. Qty change drops it. | Partial |
| Do I need an account? | No. Cookie session is optional sugar. `is_guest_checkout` is sent and **ignored** by the handler. | Guest-first (good) |
| Phone / address / TIN? | Phone and address if product flags. TIN hidden + Ops forces false. | Partial / hidden |
| BM? | No. `lang="en"`. Country placeholder `"Country Code (e.g. MY)"` (ISO-2), while CRM default on empty is `"MYS"` (ISO-3). | None + a code trap |
| FPX / TnG / Apple Pay? | Not on this page. Appear on hop 2 if the gateway collection/brand/account has them. | Processor-dependent |
| What if I leave? | Session 24h, expiry job 5 min, coupon reserve released. **No email.** | Inventory hygiene only |
| Did it work? | Poll + email hope. Dashboard link has no token. Custom-link success URL is a 404 waiting to happen. | Partial / broken edge |
| Receipt? | Communications “Official Receipt” if Billing published a document; portal download hidden. Processor also emails its own receipt (Billplz / Stripe). | Partial, dual receipts |

### Psychological environment (ADR 017 Rule 3)

The blind layout is correct in intent: no portal sidebar, no marketing nav, no “log in to continue” wall. It is incorrect in **brand ownership**. A blind checkout that still says “Powered by Lazuar” + Lazuar legal + Lazuar footer, and does not show the creator’s mark, trains the buyer to wonder whether they are paying a platform. Stripe Checkout, CHIP links, Xendit invoices, and HitPay unfurls all lead with **the merchant**. PayPal.Me leads with PayPal — and that is why people distrust it for local SMEs.

### Two-hop tax on mobile

`CheckoutLayout` puts the summary first on small screens (`flex-col-reverse`). That is the right order (amount before identity). Combined with hop 2, the thumb path is:

1. Scroll past amount + promo.
2. Type name + email (± phone ± four address fields).
3. Tap Proceed.
4. Wait for API + gateway create.
5. Land on a **second** origin that often asks for name/email **again** and *then* shows banks.

Billplz Catalog, CHIP payment links, HitPay, and Xendit collapse 2–5 onto one origin. That is the bar.

### Where Aura System B sits on this map

Aura `/book` collects service + slot + guest details **on Aura**, then creates a Hub M2M checkout (product C) and redirects to hop 2. Aura does **not** send the guest through Lazuar `CheckoutForm`. So:

- Salon guests never see promo codes, PWYW, or “Powered by Lazuar” hop 1.
- Salon guests also never see hop-1 address/TIN.
- Salon guests *do* pay the two-origin tax (Aura wizard → Billplz/Stripe/CHIP).

Improving Commerce hop 1 does not automatically improve Aura `/book`. Improving **hop 2 method honesty** (“you will pay with FPX on Billplz”) helps both. Tracker IDs below tag `plane=B` when the row is shared with Aura guest pay.

---

## Competitor UX dossiers

Each dossier is the **buyer-facing hosted page / payment link**, not the full PSP. Pricing and KYC are noted only where they change the checkout.

### 1. Stripe Payment Links + Checkout

**What it is.** Two products, one hosted UI family.

- **Payment Links** (`buy.stripe.com/…`, optional custom domain `pay.merchant.com`): no-code, long-lived URL, Dashboard or API. Quantity, adjustable qty, after-promo coupons, optional items (up to 10), subscription term upsell (monthly → yearly), shipping, tax IDs, custom fields, branding (logo, colors, font, shape), success page or `success_url`, invoices after one-time pay.
- **Checkout Sessions**: programmatic, short-lived (default 24h, min 30 min). Hosted, embedded page, or embedded form. Same wallet surface (Link, Apple Pay, Google Pay) “out of the box,” no extra domain verify for Apple Pay on Stripe’s host.

**Buyer journey.** One hop. First pixel *is* the payment page. Guest Customer by default for one-time Payment Links (Stripe’s own docs warn that “first-time customer only” promos therefore mis-fire). Account is a Link 1-click, not a forced login.

**Promo.** Coupons → customer-facing promotion codes. Payment Link flag `allow_promotion_codes`. URL `prefilled_promo_code`. Recovery sessions can re-enable codes.

**Order bump / upsell.** Not ThriveCart-style 1-click post-purchase. **Optional items** and **cross-sells** on the same Checkout. **Subscription upsells** change term before pay. No post-pay 1-click charge product in Payment Links the way SamCart sells it.

**Quantity / variants.** Adjustable quantity on the line item. Variants = separate Prices / line items / optional items, not a Shopify-style option matrix.

**Tax ID / company.** Checkout can collect tax IDs (VAT/GST) and billing address; Stripe Tax optional. This is a first-class Dashboard toggle, not a commented JSX block.

**Localization.** 35+ languages, 135+ currencies (Stripe marketing). Locale follows browser or `locale` on the session. **Not Bahasa Malaysia as a first-class MY product story.** FPX exists in Stripe’s method guide as a Malaysia rail; it is not what a US Payment Link shows by default. A MY creator on Stripe still loses DuitNow / TnG / GrabPay unless they add those methods (often they cannot, or they add Stripe-hosted FPX only).

**FPX / eWallet.** Possible as Stripe payment methods where launched; **not** the reason a pasar malam seller picks Stripe. Wallets on Checkout are Apple/Google/Link, not TnG.

**Apple / Google Pay.** Yes, hosted Checkout, no extra Lazuar-style work. Domain registration required only if you leave Stripe’s host (custom domain / Elements).

**Abandoned cart.** Documented: `consent_collection[promotions]`, `after_expiration.recovery.enabled`, `checkout.session.expired` webhook, 30-day recovery URL that clones the session, `recovered_from` on the new session, optional promo on recovery. This is the gold standard for *hosted-page* abandonment. Payment Links expire less like Sessions; recovery is a Checkout Session feature.

**Success / fulfillment.** Stripe-hosted thank-you, or `success_url`. Receipt email from Stripe. Webhooks `checkout.session.completed` / `payment_intent.succeeded`. Fulfillment is the merchant’s job (same honesty as Lazuar). Customer Portal for subscriptions.

**Embed / overlay / redirect.** Hosted redirect, embedded page, embedded form, Payment Links “buy button” / QR. This is the menu Lazuar ADR 019 explicitly refused to rebuild as a site builder — but **embed of the pay surface** is not a site builder.

**Branding / custom domain.** Dashboard branding. Custom domain **$10/month** on Payment Links (public pricing page). Without it, `buy.stripe.com` is the trust mark — acceptable globally, weaker in MY WhatsApp than `pay.chip-in.asia/yourbrand`.

**Receipts.** Stripe-generated. Invoices for one-time if enabled. Tax invoice ≠ LHDN e-Invoice.

**Honest score vs Lazuar hop 1.** Stripe wins time-to-method, wallets, promo URL, optional items, abandonment, embed, custom domain, tax ID collection. Lazuar wins nothing on the *page*. Lazuar’s only structural answers are **BYOK Billplz/CHIP (real MY rails)** and **post-pay fulfillment (portal, WhatsApp dunning, outbound webhooks, later LHDN)**. Those are not visible on first pixel.

**Trap if copied blindly.** Become Stripe. Lose FPX-as-default. Take card fees on every RM 50 deposit. Teach Malaysian aunties Apple Pay.

### 2. Billplz Payment Forms / Catalog / Bills

**What it is.** The incumbent MY payment link. Three buyer-facing shapes that people conflate:

1. **API Bills** (`www.billplz.com/bills/{id}`) — what Lazuar’s adapter creates. One amount, one email, collection’s payment mix, redirect_url, callback_url. No Lazuar branding.
2. **Catalog Payment Form** (`catalog.billplz.com`) — no-code store / form / shareable link. Billplz’s 2025-12-01 payment-link article: free plan, unlimited forms, branded page, no website. Catalog marketing: inventory, variants, categories, “payment-first architecture” / fewer abandoned carts.
3. **Dashboard “create a bill / payment link”** — the WhatsApp paste an owner makes by hand.

**Buyer journey.** One hop. First pixel on a bill is amount + **Pay** + method list the collection allows: FPX (including FPX CCA on newer collections), e-wallets via 2C2P (TnG, GrabPay, Boost — Billplz’s own rollout posts), cards as add-on, instalments / BNPL (Atome / Grab PayLater / SPayLater) when the merchant enabled them. **No account.** Name/email often prefilled from the bill create payload.

**Promo / bump / qty / variants.** On a raw **bill**: none. Amount is fixed at create. Catalog: product listing, variants, cart-like behaviour (Catalog site claims real-time cart). Promo codes are **not** a Billplz-bill primitive; sellers who need them use Catalog, a form tool, or a layer like Lazuar hop 1.

**Tax ID / company.** Not a Billplz-bill field. Some collections collect extra reference fields. B2B TIN is the merchant’s problem (or Lazuar’s hidden QuoteView).

**Localization.** Billplz UI is EN with MY bank names. Bank pages are BM/EN depending on the bank (Maybank2u is bilingual). Currency **MYR**. This is the language of the market even when the chrome is English.

**FPX / eWallet.** **This is the product.** Flat-fee FPX is why Billplz exists. Wallets and cards are attachable. HitPay’s 2026 comparison still calls Billplz e-wallet coverage “partial” vs HitPay’s longer list (ShopeePay, Maybank QR, Alipay, WeChat).

**Apple / Google Pay.** Not the Billplz story. If a card add-on supports wallets, it is the card processor’s widget, not a Billplz marketing pillar.

**Abandoned cart.** A bill that is not paid sits `due` until it expires (if the merchant set expiry). **No recovery email product** comparable to Stripe’s `after_expiration.recovery`. Informal recovery is the owner sending the same URL again. Catalog claims fewer abandons via shorter checkout, not via email drip.

**Success / fulfillment.** Billplz paid page + optional redirect. Email receipt from Billplz. Merchant dashboard. API callback. **No portal, no file unlock, no LHDN.** This is exactly ADR 019’s complaint: “native payment links leave buyers on a generic Thank You.”

**Embed / overlay / redirect.** Redirect to bill URL. WooCommerce / Shopify plugins embed *as a method*, not as a Stripe-like overlay. QR of the bill URL is common on posters.

**Branding / custom domain.** Collection logo / title on the bill. Catalog branded page. **No `pay.yourdomain.com`.** Trust mark is Billplz + the bank.

**Receipts.** Billplz receipt. Not a tax invoice. Refunds are **dashboard / no complete public refund API**.

**Relationship to Lazuar.** Lazuar **is** a Billplz bill factory on the default path. Hop 2 *is* this dossier. Lazuar hop 1 is an extra form in front of a product that was already one-tap. Every field hop 1 adds must beat “just send the Billplz URL.”

**Honest score.** For time-to-method, FPX, guest, MYR, WhatsApp paste: **Y**. For promo, bump, qty, tax ID, BM chrome, Apple Pay, abandonment emails, embed, custom domain, fulfillment: **N or P**. Lazuar should steal **nothing** from Catalog’s storefront ambitions (ADR 015 / 019: no CMS). Lazuar should steal Catalog’s **first-pixel methods** if it wants Commerce links to convert like bills.

### 3. CHIP payment pages / Collect payment links

**What it is.** Malaysian PSP (BNM-adjacent merchant acquirer / TPA for FPX). Three collect shapes: hosted purchase (API — what Lazuar’s adapter calls), **no-code payment links** (`pay.chip-in.asia/{slug}`), plugins (Woo, EasyStore, Bookly, …), plus CHIP mini for in-person DuitNow QR.

**Buyer journey (payment link, from CHIP’s own 2026 marketing + interactive demo).** One hop. First pixel: merchant mark, product thumbnail + title + price, quantity, **FPX bank grid (Affin, Alliance, BSN, CIMB, Hong Leong, HSBC, KFH, Maybank, OCBC, Public, RHB, StanChart, …)**, cards, DuitNow QR, wallets (TnG, ShopeePay, GrabPay, Boost, Maybank QR, WeChat, Alipay), BNPL (Atome, SPayLater), Google Pay logo on the Collect marketing page, even stablecoins on Collect. Contact fields (name, email, +60 phone, MY address, custom “special request”) sit **on the same page** as methods. CTA label configurable: Pay / Book / Donate.

**No-code builder features CHIP documents on `/collect/payment-links`:**

- One or multiple products on one link
- Customer-changeable quantity
- Customer-choose-amount (PWYW / donation)
- Custom fields and dropdowns
- Custom URL slug (`pay.chip-in.asia/kelasbinawebsite`)
- Require phone / require address
- Prefill customer details (their notes article: auto-fill payment links)
- Post-payment redirect (thank-you, download, calendar)
- Pause link
- Request-payment-on-your-behalf email
- QR
- Click / conversion / revenue by link

**Promo / bump.** Not marketed as coupons or order bumps. Multi-product on one link is a **catalog**, not a ThriveCart bump.

**Tax ID.** Custom fields can collect anything; no LHDN-specific TIN widget in the public demo.

**Localization.** Demo chrome is EN; bank names are the real localization. Currency MYR. Cross-border: SG/TH/ID QR in, settle MYR.

**Apple / Google Pay.** Google Pay is on the Collect logo row. Apple Pay is claimed in older CHIP social posts for card/contactless; treat as **card-network / brand config**, not a CHIP-link checkbox Lazuar can toggle.

**Abandoned cart.** Pause + expiry of links. Email “we sent them a request.” Not Stripe recovery URLs.

**Success / fulfillment.** CHIP thank-you; optional redirect; email to merchant; dashboard receipt with method + timeline (their analytics screenshots show “Paid via FPX”). Recurring tokens if `force_recurring` (Lazuar already sends this for subscription products).

**Embed / redirect.** Redirect. Plugins inject CHIP as a Woo/EasyStore method. No Polar-style overlay documented as a first-class Collect feature.

**Branding / custom domain.** Logo on the page. **Custom slug on chip-in.asia, not a custom apex domain.** “Powered by CHIP.”

**Receipts.** CHIP receipt in dashboard + email. Refunds from dashboard / mini. Settlement XLS.

**Relationship to Lazuar.** Second-class K2 in Hub (adapter exists, Ops can select CHIP, product form warns Billplz cannot vault). Lazuar hop 2 on CHIP **is** a CHIP purchase, **not** a CHIP payment-link: one `products[]` line, no bank grid under Lazuar control, no custom slug, no multi-product, no CHIP-native qty UI (qty is folded into description / minor units). If a creator wants CHIP’s no-code link, they can **bypass Lazuar entirely** and paste `pay.chip-in.asia/…`. Lazuar only wins if fulfillment after `purchase.paid` is worth the extra hop.

**Honest score.** CHIP’s no-code link is the **closest MY product** to “what Lazuar hop 1 + hop 2 should feel like if they were one page.” Qty, PWYW, phone, address, custom fields, custom slug, FPX first paint — CHIP already ships them **on the processor page**. Duplicating that builder inside Lazuar is how you become a bad CHIP. Using CHIP as hop 2 and **skipping** hop 1 for simple products is the adult move.

### 4. HitPay payment links

**What it is.** Singapore-born, SEA PSP. Malaysia is a first-class country: FPX, DuitNow QR, DuitNow Online Banking, TnG, GrabPay, Boost, ShopeePay, cards, Atome / Grab PayLater / SPayLater. Payment link is a **core SKU**, not an afterthought. Payouts next business day on domestic (their 2026 guides).

**Buyer journey.** Create in dashboard in “under 5 minutes” after KYC (1–3 business days). Amount + description → URL. Share WhatsApp / email / SMS. **Unfurl**: HitPay’s own Instagram/blog claim rich previews (store name, logo, “Pay …”) in WhatsApp / Telegram / Slack. First pixel on the hosted page: **all enabled methods**, no per-link method config. Pre-filled amount links lock the total; open links exist for “customer types the amount.”

**Promo / bump / qty / variants.** Not the HitPay story. This is a **cashier link**, not a store. Quantity is “how many times you send the link,” not a stepper.

**Tax ID / company.** Invoice-ish fields possible on some HitPay invoice products; the 2026 MY payment-link posts do not sell TIN collection.

**Localization.** EN chrome, MY method names. Multi-country (SG PayNow, PH GCash/Maya) on the same merchant brain — HitPay’s real differentiator vs Billplz.

**FPX / eWallet.** **Y**, comprehensive. HitPay’s 2026 “Payment Gateway Malaysia” article scores itself above Billplz on wallet breadth.

**Apple / Google Pay.** Cards + wallets where the card network / HitPay checkout supports them; not as loud as Stripe. SEA wallets matter more.

**Abandoned cart.** Unpaid link stays open. Reminders exist on HitPay invoices (separate from the simplest link). Not Stripe recovery.

**Success / fulfillment.** HitPay confirmation + notification. POS / QR / terminal in the same account (they sell an all-in-one). No creator portal.

**Embed / overlay / redirect.** Redirect + QR. WhatsApp unfurl is the growth hack. Plugins for storefronts.

**Branding / custom domain.** Logo / store name on the page and in unfurls. Custom domain not the 2026 blog pillar.

**Receipts.** HitPay receipt. Dashboard refunds.

**Relationship to Lazuar.** Do not put a **HitPay adapter inside Aura**. If HitPay appears, it is **Pay K2**, same as CHIP. A salon that only needs “paste a RM 50 link in WA” can use HitPay **without** Lazuar. Lazuar’s reason to exist in front of HitPay is the same as in front of Billplz: **booking / entitlement / dunning / LHDN**, not a prettier first hop.

**Honest score.** Time-to-first-method **Y**. Guest **Y**. MY rails **Y**. Promo/bump/qty/tax/BM/abandon/embed **N or P**. Unfurl is a feature Lazuar hop-1 URLs currently lose (Open Graph on portal checkout was not found in the layout; title is “Lazuar Portal”).

### 5. Xendit invoices / payment links

**What it is.** SEA PSP (ID-first, MY live). Payment Links + xenInvoice: dashboard, API, or QR → Xendit-hosted page. MY marketing (xendit.co/en-my/products/payment-links/, fetched 2026-08-16): FPX, cards, TnG, GrabPay, DuitNow QR, ShopeePay, WeChat, Alipay, PayLater (Grab / Shopee), **corporate bank account** pay-from for B2B, virtual accounts, card terminal.

**Buyer journey.** One hop. Methods on the page. Invoice duration configurable. Language: help center documents **English, Bahasa Indonesia, or browser**. **Bahasa Malaysia is not listed.** Appearance: logo, message, brand colors — “without building your own UI.” Reminders and real-time notifications are sold as cash-flow tools.

**Promo / bump / qty.** Invoice line items (qty × unit) are a **merchant-authored invoice**, not a buyer stepper on a product link. Optional items / bumps: **N**.

**Tax ID / company.** Indonesia story is NPWP / PPN. Malaysia story on the payment-link page is corporate-bank pay-in, not LHDN TIN. Stronger B2B invoice energy than Billplz bills.

**Localization.** EN / ID / browser. MYR + FPX on the MY site. Multi-country chrome is a Xendit habit (the MY page still carries ID legal footer residue).

**FPX / eWallet / BNPL.** **Y** on the MY product page.

**Apple / Google Pay.** Cards; not the headline.

**Abandoned cart.** Invoice expiry + **reminders** (Xendit sells this). Closer to “dunning an invoice” than Stripe Checkout recovery.

**Success / fulfillment.** Xendit paid state + webhook + dashboard. BookMyShow quote on the page: create link, check status, refund in one click.

**Embed / overlay / redirect.** Redirect + QR + API. Live demo at demo.xendit.co.

**Branding / custom domain.** Logo / colors / message. Custom domain not emphasised.

**Receipts.** Invoice / receipt in product; refunds in dashboard. Tax documents are market-specific, not MY e-Invoice.

**Relationship to Lazuar.** No Xendit adapter in Hub today (`AllowedGateways` = STRIPE, BILLPLZ, CHIP, RAZORPAY). Adding Xendit would be another K2, not a hop-1 rewrite. Xendit’s **invoice reminders** overlap Commerce dunning; do not rebuild xenInvoice inside Ops.

**Honest score.** Same PSP-link shape as HitPay/CHIP: first-pixel methods **Y**, guest **Y**, MY rails **Y**, branding **Y**, locale **P** (ID not BM), promo/bump **N**, fulfillment portal **N**. B2B corporate-bank pay is a row Lazuar QuoteView *wanted* and then hid.

### 6. Gumroad

**What it is.** Creator MoR. Product page + checkout (overlay or page) + file delivery + memberships. Takes a cut (publicly discussed as ~10% + processing; treat the exact 2026 rate as marketing-volatile). Cart exists (2020s-era multi-product cart).

**Buyer journey.** Land on product (cover, description, variants/tiers) → checkout: email, pay (card / PayPal / Apple Pay / Google Pay — Gumroad help and buyer guides). Discount box on checkout. Guest checkout under the confirmed email counts toward offer limits (help article 128). Optional custom checkout fields, tipping, recommendations (creator checkout settings). Overlay checkout is the classic Gumroad move: stay on the sales URL.

**Promo.** First-class Discounts tab. Codes attachable to product URLs so the buyer skips the product page and hits checkout already discounted.

**Order bump / upsell.** Recommendations / “product recommendations” at checkout — softer than SamCart 1-click. Not a full funnel OS.

**Quantity / variants.** Variants and tiers on the product. Cart qty. Membership tiers.

**Tax ID / company.** MoR tax, not buyer TIN. VAT collected by Gumroad in relevant regions.

**Localization / MYR / FPX.** USD-first creator economy. **No FPX, no DuitNow, no TnG.** A Malaysian buyer of a US PDF can pay card. A Malaysian seller of a RM 79 course to a Malaysian audience will lose conversion vs Billplz/CHIP.

**Apple / Google Pay.** **Y** on Gumroad checkout.

**Abandoned cart.** Email is Gumroad’s (they have the buyer email from the first field). Not as documented as Stripe recovery URLs.

**Success / fulfillment.** **This is the product.** Instant download, membership access, Gumroad library. Receipt from Gumroad the MoR.

**Embed / overlay / redirect.** Overlay is the brand. Embed widgets. Redirect less important.

**Branding / custom domain.** Product page on gumroad.com / username. Custom domain is a paid-era feature; trust mark is still Gumroad for many buyers.

**Receipts.** Gumroad receipt + tax invoice from Gumroad the seller-of-record.

**Relationship to Lazuar.** ADR 019’s enemy and teacher. Teacher: overlay + instant file + discount-in-URL + guest email. Enemy: **MoR cut** and **no MY rails**. Lazuar must not become Gumroad-in-MY. Lazuar *should* notice that Gumroad collects email **on the same surface as pay**, not on a prior origin.

**Trap.** `X` — copy MoR, copy 10%, copy “Gumroad for Malaysia.”

### 7. Payhip

**What it is.** Gumroad-class creator store (digital, physical, memberships) with a lower-profile MoR/processor mix. 2026 changelog (help article 386, 2026-06-02) and blog (2026-03-04): **customizable checkout** — banner, logo, background colors, **custom CSS**; 100+ new payment methods claimed on the blog; custom checkout questions; custom digital orders; upsells / upgrades / cross-sells; coupon codes; affiliate; custom domain; embed.

**Buyer journey.** Store or product → checkout (email-minimal for digital; name optional via settings). Guest by default (“bare minimum… email address only” — custom questions article). Upsell / upgrade after or during as configured. Custom CSS means the page can look like the creator, not like Payhip.

**Promo.** Coupon codes, social discounts.

**Order bump / upsell.** Cross-sells and upsells/upgrades are in the official tutorial timeline (Aurelius Tjin et al.). Not SamCart-unlimited, but **Y** as a product job.

**Quantity / variants.** Product options; cart.

**Tax ID / company.** Custom questions can ask. Not LHDN.

**Localization / MY rails.** Global payment-method expansion in 2026 marketing. **Do not assume FPX.** Treat as card / PayPal / local-where-Payhip-turned-it-on.

**Apple / Google Pay.** In the “100+ methods” bucket; verify per merchant currency. Score **P**.

**Abandoned cart.** Email exists in the creator-platform sense. Not Stripe-grade recovery URLs.

**Success / fulfillment.** Instant digital delivery, membership, store account.

**Embed / overlay / redirect.** Embed products on the creator’s site. Overlay/checkout page both exist.

**Branding / custom domain.** **Y** in 2026 (banner, logo, CSS, custom domain on paid plans). Older reviews that say “no custom domain” are stale.

**Receipts.** Payhip receipts. MoR/tax depends on how the seller is configured.

**Relationship to Lazuar.** Closest **creator-store** analog to what a naive reading of ADR 019 “storefront templates” could become. ADR 023 already chose **not** to ship a store. Steal: guest-email-only default, checkout CSS/brand, upsell as a *later* Commerce flag. Refuse: becoming Payhip.

### 8. ThriveCart / SamCart

**What it is.** Checkout **funnel operating systems** for info-product / coaching / high-ticket digital. They do not want to be a PSP. They sit on Stripe + PayPal + Apple Pay + Google Pay and sell **AOV**.

**Buyer journey (the thing they advertise in 2026 compare pages).**

```text
VSL / sales page
  → checkout (mobile-first)
       order bump checkbox(es)   ← before pay
  → card / Apple / Google / PayPal
  → 1-click upsell page          ← after pay, no PAN re-entry
  → downsell if no
  → thank-you / membership
```

SamCart 2026 compare: unlimited bumps, unlimited 1-click upsells/downsells, A/B testing, “creators who add one upsell see 68% AOV lift.” ThriveCart 2026: bumps capped by plan (1 on Standard, up to 6 on Pro+ ~$295/yr), upsell/downsell funnels, lifetime license positioning vs SamCart’s rising price.

**Promo.** Coupon codes on checkout. Affiliate engine is part of the SKU.

**Quantity / variants.** Product options; bumps are extra SKUs.

**Tax ID / company.** Address / VAT where the funnel asks. Not MY TIN.

**Localization / MY rails.** **N.** These pages assume US/EU cards. A MY buyer sees a USD/card funnel. FPX does not appear. Using ThriveCart in MY means Stripe/PayPal only.

**Apple / Google Pay.** **Y** — they shout it, because 1-click wallets feed 1-click upsells.

**Abandoned cart.** Both sell cart-abandonment email. ThriveCart/SamCart live or die on this.

**Success / fulfillment.** Thank-you pages, memberships, integrations (Zapier, course platforms). The “success page” is a **funnel asset**, not a receipt.

**Embed / overlay / redirect.** Checkout templates, pop-ups, cart types. The sales page is the embed.

**Branding / custom domain.** Full. That is the point.

**Receipts.** Processor receipts + their own order emails.

**Relationship to Lazuar.** **Do not implement.** Order bump + 1-click upsell requires:

- a vaulted card (Billplz cannot);
- a post-pay page in the payment-method session (Lazuar hop 2 is the PSP’s page, which will not render your bump);
- or a third hop after return (feels scammy if the buyer thought they were done).

ADR 019 + 023 already rejected funnel builders. Score the *capability* so the tracker can mark it **X**, not so someone files a ticket.

**Trap.** `X` — “SamCart for Malaysia.”

### 9. Polar checkout

**What it is.** Open-source **Merchant of Record** for developers (polar.sh). Stripe under the hood (2026 reviews: cards, no PayPal, no regional QR). Usage billing, seats, GitHub/Discord/license entitlements, global tax. Tailwind Labs-class audience. **Not a MY PSP.**

**Buyer journey.** **Checkout Link** = long-lived URL. Visit → Polar creates a **short-lived Checkout Session** → redirect to Polar-hosted (Stripe-powered) page. Docs (fetched 2026-08-16):

- One or several products on a link; buyer **picks one** (not a cart; multi-product order “isn’t supported yet”).
- Preset discount (silently applied) and/or allow discount codes.
- Success URL (optional `{CHECKOUT_ID}` substitution) or Polar confirmation.
- Return URL = back button.
- Trial override per link.
- Seat count lock per link.
- Metadata → session → order/subscription.
- Query params: `product_id`, `customer_email`, `customer_name`, `discount_code`, `amount` (PWYW), `locale` (BCP 47, beta), `custom_field_data.{slug}`, `theme=light|dark`, UTM + `reference_id`.

**Embed.** Official: script tag + `data-polar-checkout` opens **inline checkout** on allow-listed hosts. Wallet on embed. This is the cleanest “stay on my site” story in the developer-MoR set.

**Guest vs account.** Guest email on checkout. Customer Portal via email or server-minted customer session. Very close to Lazuar’s intended magic-link portal — Polar **ships** the tokened portal. Lazuar’s status poll **stopped minting** tokens (honesty fix) and did not replace them with Polar-grade customer sessions on the success page.

**Promo.** Preset + codes + URL prefill. Better than Lazuar (no URL prefill, no silent preset).

**Order bump.** **N.** Multi-product is a switcher, not a bump.

**Quantity / variants.** Seats. PWYW amount query. Product switcher. No SKU matrix.

**Tax ID.** MoR tax, not buyer TIN. Custom fields exist.

**Localization.** Locale query + browser detect, **beta**. No BM. No MYR-as-home-currency story. No FPX.

**Apple / Google Pay.** Via Stripe Checkout underneath. **Y** if Polar’s host enables them.

**Abandoned cart.** Session expiry (short-lived child session). Not Stripe’s recovery-email product as a Polar SKU.

**Success / fulfillment.** Polar confirmation + entitlement grant (license, GitHub, Discord, file) + emails. This is what ADR 019 wants Hub to be **without** being the MoR.

**Branding / custom domain.** Polar-hosted checkout, theme query, embed on your domain. Custom pay domain is not the headline (embed replaces it).

**Receipts.** Polar / Stripe MoR invoices.

**Relationship to Lazuar.** Polar is the **developer-experience north star** for Checkout Links (query params, embed, customer portal, entitlements) and the **company-shape anti-pattern** (MoR, Stripe-only, 5%+50¢-class pricing in 2026 reviews). Steal the link query-param design. Steal embed *of hop 2* only if the PSP offers it (Stripe Embedded Checkout; CHIP/Billplz generally do not). Do not steal MoR.

### 10. PayPal links (PayPal.Me, Pay Links and Buttons, Payment Links)

**What it is.** Three buyer-facing things, sloppily named:

1. **PayPal.Me** (`paypal.me/Name` or `paypal.me/Name/50`) — personal/business handle, optional amount. Recipient types the amount if open. Requires a PayPal account to *create*. Buyer often needs PayPal or guest card.
2. **PayPal Payment Links** (business accept-payments page, 2026): no website, methods PayPal / Venmo (US) / Pay Later / Apple Pay / cards, buyer-set amounts for services and deposits.
3. **Pay Links and Buttons (NCP)** — `paypal.com/ncp/payment/…` with `locale.x` / `country.x` query overrides.

**Buyer journey.** One hop onto PayPal origin. Trust mark is PayPal, not the merchant — fine in the US, **poisonous** for a Malaysian SME selling to Malaysians (fees, FX, “why PayPal”). Guest checkout exists as an account setting and has a long history of randomly disappearing.

**Promo / bump / qty / variants.** **N** on .Me. Payment Links are amount + item, not a catalog.

**Tax ID.** **N**.

**Localization.** Browser locale; merchant can append `locale.x`. **No BM-MY product.** MYR receiving is a PayPal-account capability, not a reason to pick it in PJ.

**FPX / eWallet.** **N.**

**Apple / Google Pay.** Apple Pay **Y** on Payment Links (PayPal’s page: “Apple Pay does not charge any additional fees” — meaning Apple’s, not PayPal’s). Google Pay not the headline.

**Abandoned cart.** **N** as a product.

**Success / fulfillment.** PayPal receipt. No portal.

**Embed / overlay / redirect.** Buttons + links. Overlay is classic PayPal Checkout on websites, not .Me.

**Branding / custom domain.** PayPal chrome. .Me handle is the “custom slug.”

**Receipts.** PayPal.

**Relationship to Lazuar.** Allowed as a **future K2** only if a tenant has a real PayPal business account and a non-MY buyer set. Not a MY v1 rail. Hub checkouts are MYR-first. Score for completeness; do not sequence.

**Trap.** Teaching a KL salon to collect deposits on PayPal.Me.

### Cross-dossier synthesis (what “table stakes” means in 2026)

If the buyer is **Malaysian and the amount is MYR**, table stakes on a payment link are:

1. **Methods on first paint** (FPX bank tiles or DuitNow / TnG / GrabPay).
2. **Guest** (email at most).
3. **Phone-sized, one origin.**
4. **Merchant name visible** (logo or trading name, not the platform).
5. **A receipt** (processor email is enough).
6. **A way to try again** if they cancel.

Promo, qty, TIN, BM chrome, Apple Pay, abandonment mail, embed, custom domain, bumps are **tier 2**. Instant entitlement (file, seat, booking) is Lazuar’s **actual** tier-1 differentiator — and it happens *after* hop 2.

If the buyer is a **global creator audience**, table stakes flip: Apple/Google/Link, overlay/embed, promo-in-URL, MoR tax, instant download. FPX drops off. That is Polar/Gumroad/Payhip. Lazuar should not pretend to win that set on hop 1.

---

## Lazuar portal audit

This is the honest read of `CheckoutForm` / `CheckoutView` and the routes around them. Every claim has a file.

### 1. What a buyer actually gets today (Commerce buy link)

**URL.** `/{tenantSlug}/checkout/{productSlug}`. Tenant slug is the workspace public slug. Product must be `IsActive`. Inactive or unknown → Next `notFound()`.

**Chrome.** Sticky top bar, right-aligned padlock, “Powered by Lazuar.” No merchant logo, no product title in the header, no language control, no method icons. Footer (root layout) is © Lazuar Platform + Terms + Privacy + Refund Policy — all platform pages.

**Main column (form).**

- IdentityBanner only if `lazuar_auth` produced a name.
- “Account Details”: Full Name, Email Address, optional WhatsApp Number (`requires_phone`) with placeholder `+60 12-345 6789` and helper “Required for delivery and important updates.”
- “Billing Details” if `requires_tax_id || requires_address`. TIN/company block is commented. Address: street, city, postal, state, country code placeholder `"Country Code (e.g. MY)"`.
- Legal paragraph: proceeding agrees to **Lazuar’s** Terms and Privacy; purchase is “a direct transaction with the Creator.”
- Submit: full-width 56px black “Proceed to Payment.”

**Side / top column (summary).**

- “Order Summary”, product name, no image.
- Subtotal: `CURRENCY 00.00` or PWYW `<input type="number">`.
- Promo Code field, always shown.
- “Total Due Today.”

**Missing from the pixel.** Quantity stepper. Interval (“then RM x / month”). Method logos. Merchant logo. Product description. SST. Locale. Order bump. Apple Pay button. FPX tiles. Trust badges beyond the padlock. Open Graph / WhatsApp unfurl specific to the product (root metadata is “Lazuar Portal”).

### 2. Guest checkout vs account — shipped, with a dead flag

Hop 1 does **not** require login. A cold visitor types name + email and pays. That is the correct default (Gumroad / Payhip / Polar / every MY PSP).

If a `lazuar_auth` cookie exists, `page.tsx` prefills name/email and locks those inputs until the buyer taps “Checkout as Guest.” Admins of this tenant get a blue “Viewing as Workspace Admin” warning — good, prevents the owner from accidentally buying their own plan as themselves.

`is_guest_checkout` is on the TypeSpec DTO and the command. **`InitiateCheckoutCommandHandler` never reads `request.IsGuestCheckout`.** CRM `ResolveClientProfileCommand` always runs. Guest vs account is a **UI lock**, not a data-plane rule. Score: **shipped UX, ghost flag**.

### 3. Promo codes — shipped with several honesty nicks

**What works.**

- Ops can create PERCENTAGE / FIXED coupons with max uses, min original price, product allow-list, expiry.
- Hop 1 validate endpoint returns discount on **unit** price. View scales it by quantity for display.
- Submit reserves under row lock; paid path `ConfirmReservation`; expiry job `ReleaseReservation`. The June 2026 gap doc that said “confirm only on zero-amount” is **stale** — `HandleOpenCheckoutSessionAsync` confirms.
- Zero-amount path also confirms.

**What is weak.**

- Validate ignores quantity. A FIXED RM 10 coupon preview is RM 10 off the unit; View then does `discountRatio = 10 / unitPrice` and multiplies by qty — so a RM 10 code on a RM 100 item × 3 display-discounts RM 30. Server submit does `CalculateDiscount(product.Price) * quantity` — same rule. **A “RM 10 off” code is RM 10 off per seat.** That may be unintended.
- Changing quantity or PWYW **strips** the coupon rather than revalidating.
- No `?promo=` / `?discount_code=` (Polar / Stripe / Gumroad all have this). WhatsApp cannot send a pre-discounted Commerce link without a dedicated product.
- No silent preset discount on the link.
- Coupon is **Commerce-only**. M2M cashier has no coupon. Aura `/book` has no coupon.
- `PromoCodeInput` is always rendered, even if the tenant has zero coupons — a dead field that, on conversion-research folklore, *lowers* conversion (Baymard: hide unused promo).

Score: **partial**. Realer than Billplz bills. Weaker than Stripe Payment Links.

### 4. Order bump / upsell — none

No optional item, no checkbox SKU, no post-pay 1-click, no Polar product switcher, no CHIP multi-product link. `fulfillment_targets` is not a bump.

`OrderSummaryCard` has an unused `context.audience` line — leftover, not an offer zone.

Score: **none**. Tracker: Later-nice or **Never** (funnel OS). Recommendation: Never for 1-click post-pay on Billplz; Later for a single optional add-on **only if** hop 1 and hop 2 can show the same total (otherwise the Billplz bill amount will not match the summary).

### 5. Quantity and variants — ghost quantity, no variants, lying PWYW

**Quantity.**

- `CheckoutView` `useState(1)`, `handleQuantityChange` exists, passed into `CheckoutForm`.
- `CheckoutForm` never renders a control. Buyers cannot change it.
- Payload sends `quantity: 1` always (unless a future caller uses the prop).
- Server multiplies `product.Price * request.Quantity`. Stripe adapter passes `Quantity` into the line item. Billplz/CHIP fold qty into minor units **and** into the description `"{name} (xN)"` (`GatewayCommon`).
- `CheckoutSession` **does not persist quantity**. A later mark-paid / debug cannot see what was intended.
- Zero-amount handler applies coupon to `product.Price` **once**, not `* quantity` — inconsistent with the paid path if qty ever becomes >1.

**Variants.** No SKU options, no size/color, no Polar product switcher, no CHIP multi-product. One Product row = one link.

**PWYW.**

- Ops can set `pricing_model = PWYW` + `minimum_price`.
- Summary shows a number input bound to `customPrice`, clamped to `minimumPrice` on blur.
- **Submit does not send `customPrice`.** There is no DTO field for it. Handler charges `product.Price * quantity` (the “recommended” price).
- Coupon-on-PWYW uses `product.price` as the ratio base, not `customPrice`.
- This is a **buyer-facing lie**. If a creator sets recommended RM 50 / min RM 10 and the buyer types 10, the summary says 10 and Billplz/Stripe will be asked for **50**.

Score: quantity **ghost**, variants **none**, PWYW **ghost + honesty bug**.

### 6. Tax ID / company fields — hidden on purpose, server still knows

ADR 023: TIN and company collection removed from hop 1; custom quote route forced `notFound()`; tax invoice button removed from portal. Ops `ProductForm` hard-codes `requires_tax_id: false`.

Meanwhile:

- TypeSpec `CheckoutConfigurationDto.requires_tax_id` still exists.
- `InitiateCheckoutCommandHandler.EnforceCheckoutConfiguration` still throws if `RequiresTaxId && taxId` blank.
- `PublicCheckoutRequestDto` still has `tax_id`, `company_name`.
- `QuoteView` still promises “TIN will be collected during the secure checkout step” — on a route that 404s.
- Custom session `is_b2b_required` is stored; hop 2 metadata does **not** set a B2B flag for Billing.

Score: **hidden**. Reactivating is a comment flip + Ops checkbox, **plus** a decision about whether TIN is collected on hop 1 (friction) or on a post-pay portal (LHDN after money). Do not collect TIN on hop 1 for B2C MY links.

### 7. Localization (BM / EN, MYR)

| Surface | Fact |
|---------|------|
| `html lang` | `"en"` hardcoded |
| Copy | English only. “Full Name”, “Proceed to Payment”, “Total Due Today”, “Promo Code”, “Powered by Lazuar” |
| Product currency | Ops hard-codes `"MYR"` on create. `Product.Currency` is stored; update path does not change currency (older gap, still true in `UpdateDetails`). |
| Display | `{context.currency} {n.toFixed(2)}` — no `Intl`, no `RM` glyph, no sen-aware BM (`RM 50` vs `50 ringgit`) |
| Country field | Placeholder ISO-2 `MY`; CRM fallback ISO-3 `MYS` when building `BillingAddressDto` if line1 is set but country omitted |
| Phone | `+60` placeholder, no country picker, no MSISDN normalize |
| Legal | EN, Malaysia governing law |
| QuoteView | `toLocaleDateString('en-GB')`, totals prefixed `MYR` |

No i18n framework, no `ms-MY` dictionary, no bank-name localization (because hop 1 has no banks).

Xendit documents EN + **Bahasa Indonesia**. Nobody in this competitor set documents **Bahasa Malaysia** as a first-class checkout locale. The real BM is on **Maybank2u / CIMB / TnG** after hop 2. That is a reason **not** to over-invest in BM chrome on hop 1 — and a reason **to get the buyer onto hop 2 faster**.

Score: MYR **shipped** (Commerce products). BM **none**. EN-only hop 1 is acceptable if hop 2 is a MY bank. It is not acceptable if hop 2 is Stripe Checkout in English for a BM-only buyer.

### 8. FPX / eWallet buttons

**Not on hop 1.** There is no Maybank tile, no DuitNow mark, no “You will pay with FPX on the next screen.”

Hop 2:

- Billplz bill: FPX + whatever the collection enabled. Lazuar does not pass a method filter.
- CHIP purchase: brand’s enabled methods.
- Stripe Checkout: card/wallets/Stripe-FPX if the account has it.
- M2M: same adapters.

Ops cannot preview the method mix from the product form. The buyer cannot see the mix before “Proceed.”

Score: **processor-dependent / absent on Lazuar pixels**. Honesty gap vs HitPay/CHIP/Xendit/Billplz Catalog, which show methods on first paint.

### 9. Apple Pay / Google Pay

**Not on hop 1.** No Wallet button, no Payment Request API, no Stripe Embedded Checkout, no CHIP wallet widget.

Hop 2 Stripe: **Y** if the Stripe account + browser support it.
Hop 2 Billplz: **N** as a product.
Hop 2 CHIP: Google Pay appears on CHIP marketing; treat as brand-config **P**.

Score: **none** as a Lazuar feature; **partial** as “possible if K2 = Stripe.”

### 10. Abandoned cart

**What exists.**

- Session `ExpiresAt = now + 24h`.
- `CheckoutSessionExpiryJob` every 5 minutes: OPEN + past expiry → `EXPIRED` + coupon `ReleaseReservation`.
- Cancel return: `?cancelled=true` amber banner, form intact.
- Custom sessions default expiry **30 days** (`CreateCustomCheckoutCommandHandler`).
- M2M sessions have `expires_at` in the DTO; no Commerce-style coupon to release.

**What does not exist.**

- No email/WhatsApp “you left RM 79 unpaid.”
- No Stripe-style recovery URL.
- No consent checkbox for promotional recovery (Stripe `consent_collection[promotions]`).
- No analytics of hop-1 abandon vs hop-2 abandon.
- `is_guest_checkout` unused, so you cannot even segment.

Communications *could* send this (templates + Resend are the gate for checkout itself) but nothing publishes “session expired” to Communications.

Score: **partial** (inventory hygiene). Not a recovery product.

### 11. Success page / fulfillment

`CheckoutSuccessView` states: VERIFYING → SUCCESS | TIMEOUT | ERROR.

Honesty that is **good**:

- It polls the server. It does not trust `?payment=success`.
- TIMEOUT copy tells the buyer to wait for email, not that they are paid.
- hub-cashier sample is even more explicit.

Honesty that is **bad**:

- Status API never returns `ACTIVE` (Commerce status is `COMPLETED` | `PENDING`). The View checks `ACTIVE || COMPLETED` — COMPLETED works; ACTIVE is dead.
- `token` is always null. “Go to Dashboard” is unauthenticated portal, which then says “log in using a secure magic link.” The success page promised access it cannot grant.
- Zero-amount success navigation **omits `sub_id`** → ERROR “Invalid Session” after a free coupon.
- Custom-link success URL is `/{tenant}/checkout/custom/success` — **no route**. Buyer returning from Billplz on a custom session 404s.
- Copy is generic: “receipt, digital downloads, and community access links” even for a one-time donation-shaped product with no files.
- No amount, no method, no receipt number, no ICS, no download button, no WhatsApp deep link.

Fulfillment that is **real, off-page**:

- Order / Subscription rows.
- `order.completed` / `subscription.activated` / `payment_link.paid` outbound.
- Digital Product Delivery email (portal URL as the file).
- Official Receipt email if Billing published a document.
- Dunning / update-payment for past-due.

Score: **partial**. Better philosophy than “mark paid on redirect.” Worse execution than Polar/Gumroad instant access, and a **broken** custom-link return.

### 12. Embed / overlay / redirect

Commerce hop 1 is a **full page**. Submit is `window.location.href = result.url` (full redirect to hop 2).

There is:

- no Stripe Embedded Checkout
- no Polar `data-polar-checkout`
- no iframe overlay
- no Gumroad overlay
- no copy-paste script for Framer / Linktree beyond “link the button to the URL” (ADR 019’s official integration story)

M2M is also redirect-only. That is correct for Aura `/book` (the wizard *is* the embed).

Score: **redirect only**. ADR 019 said this is a feature (bring your own page). It is also why hop 1 must be fast — it *is* the page.

### 13. Branding and custom domain

| Claim in older ADRs / layouts | Live |
|-------------------------------|------|
| Tenant theme/colors on `[tenantSlug]/layout` | **Not fetched** |
| Creator logo on checkout | Only on **hidden** QuoteView (`profile.logo_url`) |
| Custom domain `pay.creator.com` | **None**. Portal is `hub.lazuar.com/portal` (`NEXT_BASE_PATH`) or localhost |
| Remove “Powered by Lazuar” | Always on |
| Product-level OG image | Root metadata only |
| CHIP-style custom slug | Tenant slug + product slug is the analog (`/acme/checkout/pro`) — decent, not `pay.chip-in.asia/pro` |

QuoteView *would* have been the branded surface (logo, legal name, TIN, SSM, proforma typography). It is lobotomized.

Score: **none** for custom domain; **partial** for URL structure; **poor** for on-page merchant brand.

### 14. Receipts

Three possible receipts, none of them a hop-1 feature:

1. **Processor** — Billplz / Stripe / CHIP email. Always, if the processor does it.
2. **Communications “Official Receipt”** — on `DocumentPublished` with HMAC link to `/public/billing/{slug}/documents/{id}`. Template-gated.
3. **Portal “Download Tax Invoice”** — `[MVP-HIDE]`.

Legal refund page: Lazuar cannot refund; creator must. Default “sales are final.”

No SST line on `OrderSummaryCard`. No LHDN QR. No PDF on the success page.

Score: **partial**. Dual receipts are confusing; hidden tax invoice is honest given ADR 023.

### 15. Time to first pixel / mobile — architecture score

**Helps.**

- SSR product fetch. Small component tree. No product images to LCP.
- `flex-col-reverse` puts amount first on mobile.
- 44–56px inputs (`h-12` / `h-14` CTA).
- Blind layout, no sidebar.
- `revalidate: 60` on product GET.

**Hurts.**

- Geist + Geist Mono from Google Fonts on every checkout.
- Extra `/one/auth/me` + entitlements when a cookie exists.
- Hop 1 has **zero** payment methods — first *useful* pixel for a ready-to-pay thumb is hop 2.
- Submit wait copy “Securing Data…” is vague and slow-feeling.
- Two origins (portal + billplz.com / checkout.stripe.com / gate.chip-in.asia).
- Address fields are a four-input grid with a raw country code — hostile on a 390px-wide phone.
- Promo field always present.
- No `inputMode`, no `autoComplete` beyond browser defaults (name/email will autofill; country code will not).
- No `viewport` tweaks beyond Next defaults.
- Dark mode variables exist; checkout does not force light (CHIP/Stripe usually stay light for trust).

**Vs competitors on mobile first paint.**

| Product | First useful pixel |
|---------|--------------------|
| Billplz bill / Catalog | Amount + Pay + banks |
| CHIP payment link | Amount + bank grid |
| HitPay / Xendit | Amount + method list |
| Stripe Payment Link | Amount + Apple Pay / card |
| Polar / Gumroad overlay | Email + pay |
| **Lazuar hop 1** | Amount + **name/email form** |
| **Lazuar M2M** | Processor page (good) |

Score: hop 1 **partial / below MY PSP bar**. M2M **fine**.

### 16. Update-payment and portal (adjacent journeys)

`update-payment/[subId]`: clear PAST_DUE / SUSPENDED card, amount = **product.Price** (not necessarily the failed invoice), CTA “Update Payment Method” → gateway with `setupFutureUsage: true`. Success returns to **portal**, not a verifying page. Good enough for dunning SMS/email `{{update_payment_link}}`.

Portal: lists subscriptions, cancel via `POST /public/commerce/{tenant}/portal/cancel` (handler exists now; June gap doc is stale). Without a token and without a cookie, it is a dead end — which is what the success page currently links to.

### 17. Custom payment links — built, then 404’d

Backend: line items, 30-day expiry, optional B2B flag, gateway preference, public GET, `payment_link.paid` webhook, mark-paid offline, HMAC draft PDF.

Ops: Quotes UI copies `/{slug}/pay/{id}`.

Portal: `notFound()`. QuoteView unused. Success URL for the custom initiate path points at a **non-route**.

This is the most expensive honesty gap in the checkout product: the **high-ticket** flow ADR 020 wanted (MSA + pay) is a 404.

Score: **hidden + broken return**.

### 18. Email-config gate (Commerce only)

`InitiateCheckoutCommandHandler` refuses checkout if the workspace has no active email provider. Ops cannot activate a product without Resend. M2M cashier **does not** have this gate (`payments-integration-quickstart.md` says so explicitly).

Meaning: a creator who only wanted “Billplz link + webhook” still cannot use Commerce buy links without Resend. They *can* use M2M. They *can* use Billplz Catalog directly.

### 19. `IsGuestCheckout`, metadata, interval honesty — leftover contract

- `metadata` on public checkout is real (P09 / P10.22): stamped onto the session and copied onto Subscription at activate. Aura SaaS-subscription hints live here. Buyers never see it.
- Interval `mo`/`yr` sets `setupFutureUsage` true on hop 2 (Stripe off_session, CHIP `force_recurring`). Summary still says “Total Due Today” with no “then RM x on DATE.” Billplz cannot vault — Ops warns, hop 1 does not. A monthly product on Billplz is a **one-time bill dressed as a subscription**. Hop 1 should say so. It does not.

### 20. Audit verdict (one paragraph)

Lazuar’s hosted checkout is a **competent CRM-and-coupon pre-page** in front of **other companies’** hosted cashiers, plus a **serious fulfillment backend** (sessions, coupons with reserve/confirm/release, signed webhooks, portal, dunning, outbound `payment_link.paid`). It is **not** a competitive payment-link UX. CHIP, HitPay, Xendit, Billplz Catalog, and Stripe Payment Links all beat it on time-to-method, merchant brand, and “methods the thumb already knows.” ThriveCart/SamCart beat it on AOV machinery Lazuar should refuse. Polar/Gumroad/Payhip beat it on embed, promo-in-URL, and instant entitlement UX, while losing MY rails and taking a MoR cut Lazuar correctly refuses. The highest-leverage honesty fixes are: stop lying about PWYW, pass `sub_id` on zero-amount success, stop linking a tokenless dashboard, un-break or stay-honest about custom links, and either **show FPX/wallet marks on hop 1** or **skip hop 1** when the product does not need CRM fields. The highest-leverage product refusal is: do not build SamCart.

---

## Feature scorecard

Marks: **Y** / **P** / **N** / **—** / **X**.
Lazuar is split so M2M does not launder hop-1 gaps.

Columns: **St** Stripe Payment Links/Checkout · **Bp** Billplz bill/Catalog · **Ch** CHIP Collect link/page · **Hp** HitPay link · **Xe** Xendit invoice/link · **Gr** Gumroad · **Ph** Payhip · **TC** ThriveCart/SamCart · **Po** Polar · **PP** PayPal links · **L1** Lazuar Commerce hop 1 · **L2** Lazuar hop 2 (whatever K2 is) · **Lm** Lazuar M2M cashier

### Buyer-journey rows

| Feature | St | Bp | Ch | Hp | Xe | Gr | Ph | TC | Po | PP | L1 | L2 | Lm | Lazuar depth | Notes |
|---------|:--:|:--:|:--:|:--:|:--:|:--:|:--:|:--:|:--:|:--:|:--:|:--:|:--:|---------------|-------|
| Time to first pixel (page chrome) | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y | P | Y | Y | partial | L1 is SSR-small but fonts + extra origin |
| Time to first **method** button | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y | **N** | Y | Y | none on L1 | The conversion hole |
| Mobile one-thumb checkout | Y | Y | Y | Y | Y | Y | Y | Y | Y | P | P | Y | Y | partial | L1 fields then hop 2 |
| Guest checkout (no account) | Y | Y | Y | Y | Y | Y | Y | Y | Y | P | Y | Y | Y | shipped | `is_guest_checkout` unused |
| Optional logged-in prefills | P | N | P | N | N | P | P | P | P | Y | Y | — | N | shipped | IdentityBanner |
| Promo / discount codes | Y | N | N | N | N | Y | Y | Y | Y | N | P | N | N | partial | No URL prefill; FIXED×qty |
| Preset / silent discount on link | Y | N | N | N | N | Y | P | P | Y | N | N | N | N | none | Polar/Gumroad/Stripe |
| Order bump (pre-pay checkbox) | P | N | N | N | N | P | P | Y | N | N | N | N | N | none | Stripe optional items = P |
| 1-click post-pay upsell | N | N | N | N | N | N | P | Y | N | N | N | N | N | none | **X** to copy on Billplz |
| Quantity stepper | Y | P | Y | N | P | Y | Y | P | P | N | **N** | P | N | ghost | State+API, no UI |
| Variants / product switcher | P | P | Y | N | P | Y | Y | P | Y | N | N | N | N | none | CHIP multi-product; Polar switcher |
| PWYW / buyer-set amount | Y | P | Y | Y | P | Y | P | P | Y | Y | **P** | N | Y | ghost | L1 display lie; Lm is just `amount` |
| Tax ID / company fields | Y | N | P | N | P | N | P | P | P | N | **hidden** | N | N | hidden | ADR 023 |
| Billing address optional | Y | N | Y | N | P | P | P | P | P | N | Y | N | N | shipped | Product flag |
| Phone / WhatsApp field | P | N | Y | N | P | P | P | P | P | N | Y | N | N | shipped | Product flag |
| Locale BM | N | P | P | P | N | N | N | N | N | N | N | P | P | none | BM lives on bank/wallet pages |
| Locale EN | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y | shipped | |
| Currency MYR native | P | Y | Y | Y | Y | N | P | N | N | P | Y | Y | Y | shipped | Ops hard-codes MYR |
| FPX buttons | P | Y | Y | Y | Y | N | N | N | N | N | N | Y* | Y* | none on L1 | *if K2 collection/brand has FPX |
| DuitNow QR | N | Y | Y | Y | Y | N | N | N | N | N | N | Y* | Y* | none on L1 | |
| TnG / GrabPay / Boost | N | P | Y | Y | Y | N | N | N | N | N | N | Y* | Y* | none on L1 | Billplz wallets = add-on |
| ShopeePay / BNPL | N | P | Y | Y | Y | N | N | N | N | N | N | Y* | Y* | none on L1 | Leave at processor |
| Apple Pay | Y | N | P | P | N | Y | P | Y | Y | Y | N | Y* | Y* | none on L1 | *Stripe hop 2 |
| Google Pay / Link | Y | N | P | P | N | Y | P | Y | Y | P | N | Y* | Y* | none on L1 | |
| Abandoned-session expiry | Y | P | P | P | Y | P | P | Y | Y | N | P | P | P | partial | 24h + job; no mail |
| Abandoned-cart **email** | Y | N | N | P | Y | P | P | Y | N | N | N | N | N | none | Stripe recovery URL is gold |
| Success page (honest) | Y | P | Y | Y | Y | Y | Y | Y | Y | P | P | P | Y | partial | Tokenless dashboard; zero-amount bug |
| Instant fulfillment / entitlement | P | N | P | N | N | Y | Y | P | Y | N | P | — | P | partial | Email + webhook; not on-page |
| Embed / overlay | Y | N | N | N | N | Y | Y | Y | Y | P | N | N | N | none | Redirect only |
| Redirect (hosted) | Y | Y | Y | Y | Y | P | P | P | Y | Y | Y | Y | Y | shipped | |
| Merchant branding on page | Y | P | Y | Y | Y | P | Y | Y | P | N | N | P | P | none | L1 is Lazuar-branded |
| Custom domain / slug | Y | N | P | N | N | P | Y | Y | P | P | P | N | N | partial | `/{tenant}/checkout/{slug}` only |
| WhatsApp / OG unfurl | P | P | P | Y | P | Y | Y | P | P | P | N | P | P | none | Title “Lazuar Portal” |
| Receipt email | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y | P | Y | Y | partial | Dual + hidden tax invoice |
| Tax invoice / e-Invoice | P | N | N | N | P | Y | P | P | Y | P | hidden | N | N | hidden | LHDN is later, not hop 1 |
| Guest magic-link portal | N | N | N | N | N | Y | Y | P | Y | N | P | — | N | partial | Polar-grade portal not wired on success |

\*L2 / Lm method marks are **not Lazuar UI**. They are “the processor page the adapter opens.”

### Merchant-side rows (creating the link)

| Feature | St | Bp | Ch | Hp | Xe | Gr | Ph | TC | Po | PP | Lazuar Commerce | Lazuar M2M |
|---------|:--:|:--:|:--:|:--:|:--:|:--:|:--:|:--:|:--:|:--:|:----------------:|:----------:|
| No-code link in <5 min | Y | Y | Y | Y | Y | Y | Y | P | Y | Y | P | N (needs server) |
| API create | Y | Y | Y | Y | Y | P | P | P | Y | P | Y | Y |
| Idempotent create | Y | P | P | P | Y | N | N | N | Y | N | N | Y |
| Line-item / custom amount link | Y | Y | Y | Y | Y | N | P | P | P | Y | hidden | Y |
| Pause / expire link | Y | P | Y | P | Y | Y | Y | Y | Y | N | P | P |
| Copy URL from dashboard | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y | N |
| Signed outbound webhook | Y | Y | Y | Y | Y | P | P | P | Y | P | Y | Y |
| BYOK (not MoR) | — | Y | Y | Y | Y | N | N | Y* | N | N | Y | Y |

\*ThriveCart/SamCart are BYO Stripe/PayPal, not MoR — they are still a funnel tax.

### One-glance verdicts (do not flatten)

| Question | Answer |
|----------|--------|
| Does Lazuar have a hosted checkout? | **Yes** — two hops. |
| Does it look like a 2026 payment link? | **No.** It looks like a CRM form. |
| Does it collect money in MYR via FPX? | **Yes, if K2 = Billplz or CHIP and the collection/brand has FPX.** Not because hop 1 drew a bank. |
| Does it beat Stripe Payment Links globally? | **No.** |
| Does it beat Billplz Catalog in MY no-code? | **No**, except fulfillment after pay. |
| Does it beat Polar for indie SaaS DX? | **No** on checkout UX. **Yes** on BYOK + MY rails + no MoR cut. |
| Should we build bumps? | **No.** |
| Should we skip hop 1 when we can? | **Yes.** M2M already does. Commerce should too when phone/address/coupon are off. |

---

## Tracker IDs

New family **`CK`** — hosted checkout / payment-link **buyer and link-create** jobs. Distinct from Aura `GB` (salon `/book`) and `PY` (Aura System B money physics). Promote into `00-checklist-tracker.md` using `20-sequencing-and-tracker-schema.md` rules. **Not a ship commitment.**

Money plane: **B** = guest → merchant via Hub. **—** = UX only.

Overlap: several `CK` rows are *why* a Hub checkout still feels worse than HitPay even after soak.

### Seed catalog

| ID | Feature | Lazuar now | Plane | Class | Suggested wave | V | Why implement / refuse |
|----|---------|------------|:-----:|-------|---------------:|---|------------------------|
| CK-001 | Methods visible on first useful pixel (FPX / wallet / Apple marks or skip-to-hop-2) | none on L1 | B | table-stakes | 3 | Later | MY buyers abandon forms that hide Maybank. Either draw marks + “continue to FPX” or skip hop 1. |
| CK-002 | Single-origin checkout when no extra fields required | none | B | table-stakes | 3 | Later | If product has no phone/address/coupon, POST-and-redirect from the share URL (Payment-Link-shaped). Do not collect name twice. |
| CK-003 | Guest checkout remains default | shipped | B | table-stakes | — | Ours | Do not add a forced Lazuar account. Polar/Gumroad/HitPay all guest-first. |
| CK-004 | `is_guest_checkout` honored or deleted | ghost | — | hygiene | 3 | Partial | Dead flag. Either isolate CRM or remove from TypeSpec. |
| CK-005 | Promo codes (validate + reserve + confirm + release) | partial | B | table-stakes | 3 | Partial | Already better than Billplz bills. Fix FIXED×qty policy; don’t rebuild Stripe Coupons. |
| CK-006 | Promo prefill on URL (`?promo=` / `?discount_code=`) | none | B | differentiator | 8 | Later | WhatsApp “this code is already applied.” Polar/Stripe/Gumroad have it. |
| CK-007 | Hide promo field when tenant has no active coupons | none | — | hygiene | 3 | Later | Empty promo fields tax conversion. |
| CK-008 | Order bump / optional add-on | none | B | later-nice | — | Later | Only if the **same total** hits hop 2. One add-on max. |
| CK-009 | 1-click post-pay upsell funnel | none | B | trap | — | **Never** | SamCart. Needs vault. Billplz cannot. Company-shape. `X` |
| CK-010 | Quantity stepper that matches charge | ghost | B | table-stakes | 3 | Partial | Either ship the stepper + persist qty on session, or delete the state. |
| CK-011 | Variants / multi-price switcher | none | B | later-nice | 9 | Later | Separate products + Polar-style switcher later. Not a Catalog clone. |
| CK-012 | PWYW charges what the buyer typed | ghost | B | hygiene | 3 | Partial | **Honesty bug.** Send amount or remove the input. Min already on product. |
| CK-013 | Tax ID / company on hop 1 | hidden | B | later-nice | 10 | Later | ADR 023. Re-enable with LHDN, not before. Do not block B2C. |
| CK-014 | Custom payment link / quote (`/pay/{id}`) live | hidden | B | table-stakes | 8 | Later | Backend exists. 404 is an honesty gap for high-ticket. Fix success URL (`/checkout/custom/success` is not a route). |
| CK-015 | EN copy + MYR display | shipped | B | table-stakes | — | Both | Keep. Use `RM` / `Intl` later; not a BM project. |
| CK-016 | Bahasa Malaysia chrome on hop 1 | none | B | later-nice | 10 | Later | Banks already speak BM. Only if hop 1 stays long. Prefer CK-002. |
| CK-017 | FPX / eWallet / DuitNow **marks** on hop 1 | none | B | table-stakes | 3 | Later | Static honesty (“Pay with FPX, TnG, card on next screen”) from active K2. Not a fake method picker. |
| CK-018 | Apple Pay / Google Pay on Lazuar pixels | none | B | later-nice | 11 | Later | Only via Stripe Embedded Checkout or CHIP widget. Never fake buttons on Billplz. |
| CK-019 | Abandoned-session expiry + coupon release | partial | B | hygiene | — | Both | Job exists. Keep. |
| CK-020 | Abandoned-cart recovery email | none | B | later-nice | 8 | Later | Needs promo consent. Do not spam. Stripe shape, Resend body. Not Wave 0. |
| CK-021 | Success page polls server (not `?paid=1`) | shipped | B | hygiene | — | Ours | Keep. hub-cashier is the teaching copy. |
| CK-022 | Success grants access (token or honest wait) | partial | B | table-stakes | 3 | Partial | Status API `token` is always null. Pass `sub_id` on zero-amount. Stop “Go to Dashboard” into a login wall. |
| CK-023 | Success URL for custom links resolves | none | B | hygiene | 3 | Partial | `…/checkout/custom/success` 404. Blocker for CK-014. |
| CK-024 | Embed / overlay checkout | none | B | later-nice | 10 | Later | Only if K2 is Stripe Embedded or Polar-like. Billplz has no overlay. Do not iframe Billplz. |
| CK-025 | Merchant branding on hop 1 (logo, name, hide platform legal as primary) | none | B | table-stakes | 8 | Later | QuoteView already has logo/TIN. ADR 017 theme was promised. “Powered by Lazuar” can stay small. |
| CK-026 | Custom domain | none | B | later-nice | 10 | Later | Stripe charges $10/mo. Not Wave 3. Slug path is enough for v1. |
| CK-027 | Product OG / WhatsApp unfurl | none | B | differentiator | 8 | Later | HitPay wins paste-in-WA here. Title + amount + logo. |
| CK-028 | Receipt email (processor + optional Official Receipt) | partial | B | table-stakes | 8 | Partial | Do not dual-send confusing “tax invoice” while UI is hidden. |
| CK-029 | SST / LHDN on the hosted page | hidden | B | later-nice | 10 | Later | Not a first-pixel job. |
| CK-030 | Hop-1 interval honesty (Billplz = one-time even if `mo`) | none | B | hygiene | 3 | Partial | Do not say “subscription” on a Billplz bill. Ops already warns; hop 1 does not. |
| CK-031 | M2M cashier remains redirect + signed webhook | shipped | B | table-stakes | 0 | Ours | Do not put CheckoutForm in front of Aura `/book`. Soak still gates production claims. |
| CK-032 | Commerce email-config gate documented or relaxed | shipped | B | hygiene | 3 | Partial | Creators who only want a Billplz URL hit a Resend wall. Either say so in Ops or allow checkout with processor receipts only. |
| CK-033 | Country field ISO-2 vs ISO-3 consistency | partial | — | hygiene | 3 | Partial | Placeholder `MY`, CRM default `MYS`. Pick one. |
| CK-034 | ThriveCart-class funnel builder | none | — | trap | — | **Never** | `X` |
| CK-035 | Become MoR (Polar/Gumroad/Payhip/Paddle-for-creators) | none | — | trap | — | **Never** | Breaks BYOK + MY rails + ADR 019. `X` |
| CK-036 | PayPal.Me as MY deposit rail | none | B | trap | — | **Never** | FX + fees + no FPX. Optional future K2 for non-MY only. `X` for MY v1 |
| CK-037 | HitPay / Xendit adapter inside Aura | none | B | trap | — | **Never** | Pay K2 only, if ever. `X` inside Aura |
| CK-038 | Copy CHIP no-code builder into Ops | none | — | trap | — | **Never** | Use CHIP as K2. Do not become CHIP Catalog. `X` |

### Mapping to existing / sibling IDs (do not duplicate)

| Existing / sibling | Relationship |
|--------------------|----------------|
| Payments soak / signed webhook fulfillment | M2M path. CK-031 is the UX lock: keep it one hop. |
| Confirmation waits for server | Same philosophy as CK-021. |
| Sandbox three-book soak | **Still the gate.** No CK row becomes “Ours in production” before this. |
| `payment.failed` honesty | Hop 2 failure → L1 cancel banner is generic. |
| CHIP / extra MY rails | Enables CK-017/CK-018 on hop 2, not hop 1. |
| Aura guest checkout amounts / confirmation | Aura wizard, not CheckoutForm. Do not merge. |
| SST / LHDN | CK-013 / CK-029 wait on those programs. |
| Receipt email on online paid | CK-028. |
| Replace Paddle / take-rate | Same refuse spirit as CK-035. |

### Suggested sequence (not a program)

**Wave 0 (already locked):** `CK-031` + soak. Do not advertise Commerce links as live money until soak.

**Wave 3 (honesty, small diffs, no new company):**

1. CK-012 PWYW charges the typed amount **or** remove the input.
2. CK-022 / CK-023 success `sub_id` + custom success route + stop tokenless “Dashboard.”
3. CK-010 ship or delete quantity.
4. CK-030 Billplz ≠ subscription on the pixel.
5. CK-017 static method honesty from active K2.
6. CK-005 / CK-007 coupon policy + hide empty field.
7. CK-004 / CK-033 / CK-032 hygiene.
8. CK-001 / CK-002 design decision: **skip hop 1** when configuration is empty.

**Wave 8 (link product):** CK-014 custom links back (if B2B is back on the roadmap), CK-006 promo URL, CK-020 recovery email with consent, CK-025 merchant brand, CK-027 unfurl, CK-028 receipt honesty.

**Wave 10+:** CK-013 TIN with LHDN, CK-016 BM only if hop 1 survived, CK-024 embed only for Stripe K2, CK-026 custom domain if someone pays for the ops cost.

**Never:** CK-009, CK-034, CK-035, CK-036 (MY v1), CK-037 (inside Aura), CK-038.

### Promotion rule

A `CK-*` row may enter `00-checklist-tracker.md` when:

1. It is one buyer or merchant job (not “rebuild checkout”).
2. At least two of: a named competitor has it; Lazuar has a slice/ghost/hide; a MY creator can describe it in one breath.
3. It names a plane and does not mix Paddle.
4. Traps stay `Never` with `X`.

If `01`–`20` already covered a job under another family, **do not mint a second ID**. Add a see-also on the existing row.

---

## Appendix A — file and API map

### Portal (buyer)

| Path | Role |
|------|------|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-portal/src/app/layout.tsx` | `lang="en"`, Geist, Lazuar footer |
| `…/app/page.tsx` | Non-catalog landing |
| `…/app/[tenantSlug]/layout.tsx` | No theme fetch |
| `…/app/[tenantSlug]/checkout/[productSlug]/layout.tsx` | Blind chrome, Powered by Lazuar |
| `…/app/[tenantSlug]/checkout/[productSlug]/page.tsx` | Product + auth + cancelled |
| `…/app/[tenantSlug]/checkout/[productSlug]/success/page.tsx` | Success shell |
| `…/app/[tenantSlug]/pay/[sessionId]/page.tsx` | **notFound** |
| `…/app/[tenantSlug]/update-payment/[subId]/page.tsx` | Arrears |
| `…/app/[tenantSlug]/portal/page.tsx` | Buyer dashboard |
| `…/modules/checkout/components/CheckoutView.tsx` | Orchestrator |
| `…/CheckoutForm.tsx` | Identity submit |
| `…/CheckoutLayout.tsx` | Two-column / mobile reverse |
| `…/CheckoutSuccessView.tsx` | Poller |
| `…/OrderSummaryCard.tsx` | Totals + PWYW |
| `…/PromoCodeInput.tsx` | Codes |
| `…/IdentityBanner.tsx` | Guest toggle |
| `…/QuoteView.tsx` | Orphan proforma |
| `…/lib/api.ts` | Public commerce client |
| `…/modules/core/lib/server-client.ts` | Cookie-forwarding SSR client |

### API

| Method | Path | Role |
|--------|------|------|
| GET | `/public/commerce/{tenantSlug}/products/{slug}` | Buy-link catalog |
| GET | `/public/commerce/{tenantSlug}/validate-coupon` | Unit discount preview |
| POST | `/public/commerce/checkout` | Create session + hop-2 URL |
| GET | `/public/commerce/{tenantSlug}/checkout/{sessionId}/status` | Poll; token always null |
| GET | `/public/commerce/checkout/{subId}/status?tenant_slug=` | Legacy poll |
| GET | `/public/commerce/{tenantSlug}/custom-checkouts/{sessionId}` | Custom link payload |
| GET | `/public/commerce/checkout/{subId}/arrears` | Update-pay summary |
| POST | `/public/commerce/checkout/{subId}/update-payment` | Recovery hop 2 |
| GET/POST | `/public/commerce/{tenantSlug}/portal` (+ `/cancel`) | Magic portal |
| POST | `/integrations/payments/checkouts` | M2M cashier |
| GET | `/integrations/payments/checkouts/{id}` | M2M poll |

### Backend

| Path | Role |
|------|------|
| `Modules/Commerce/Application/Commands/InitiateCheckoutCommandHandler.cs` | Hop 1 server |
| `Modules/Commerce/Infrastructure/Endpoints/PublicCheckoutEndpoints.cs` | HTTP |
| `Modules/Commerce/Infrastructure/Workers/CheckoutSessionExpiryJob.cs` | Abandon hygiene |
| `Modules/Commerce/Infrastructure/EventHandlers/GatewayPaymentCompletedIntegrationEventHandler.OpenCheckout.cs` | Fulfill |
| `Modules/Payments/Application/Services/CheckoutSessionCashier.cs` | Gateway pick |
| `Modules/Payments/Infrastructure/Gateways/*GatewayAdapter.cs` | Hop 2 create |
| `examples/hub-cashier-next/**` | M2M reference UX |

## Appendix B — competitor source notes (2026-08-16)

- Stripe Payment Links: promotion codes, optional items (max 10), subscription upsells, custom domain $10/mo, Apple/Google/Link, 35+ languages / 135+ currencies (marketing). Abandoned carts: `after_expiration.recovery`, 30-day recovery URL, promotional consent.
- Billplz: Catalog free-tier payment forms (2025-12-01 blog); bills API is what Hub calls; FPX + optional wallets/cards/BNPL; no promo on raw bills.
- CHIP: `/collect/payment-links` builder — qty, PWYW, custom fields, custom slug, require phone/address, prefill, redirect, pause, QR, FPX grid on first paint; Collect also lists Google Pay + stablecoins.
- HitPay: MY guides 2026-05/06 — FPX, DuitNow, TnG, GrabPay, Boost, ShopeePay, cards, Atome/Grab/SPayLater; WhatsApp unfurl; <5 min create after KYC.
- Xendit MY payment links page: FPX, DuitNow QR, TnG, GrabPay, ShopeePay, WeChat, Alipay, PayLater, corporate-bank pay; branding; EN/ID/browser locale (help).
- Gumroad help 128 / 191: discounts, guest email counts toward limits, checkout fields, recommendations.
- Payhip 2026-03/06: checkout CSS/logo/banner, custom questions, upsells/cross-sells, custom domain.
- ThriveCart vs SamCart 2026 compare pages: bumps + 1-click upsells as the SKU; Apple/Google/PayPal/Stripe.
- Polar checkout links docs: long-lived link → short session; query params including `locale`, `discount_code`, `amount`; embed script; one product per pay.
- PayPal Payment Links / .Me / NCP locale query: wallets + Apple Pay; no FPX.

## Appendix C — honesty bugs to keep in the open

These are not “later features.” They are ways the current pixels lie.

1. **PWYW summary ≠ charge** (`OrderSummaryCard` vs `InitiateCheckoutCommandHandler` using `product.Price`).
2. **Zero-amount success omits `sub_id`** → Invalid Session.
3. **Success “Go to Dashboard”** has no magic token (`Token = null` by policy).
4. **Custom initiate success URL** `/{tenant}/checkout/custom/success` has no page.
5. **Custom `/pay/{id}`** is `notFound` while Ops still copies that URL.
6. **`is_guest_checkout` unused.**
7. **Quantity UI absent; quantity not persisted on session.**
8. **FIXED coupon × quantity** may surprise a merchant who meant RM 10 off the order.
9. **“Total Due Today”** on `mo`/`yr` + Billplz (no vault).
10. **QuoteView TIN promise** on a 404 route.
11. **Country `MY` vs `MYS`.**
12. **Billplz hop-2 name** is email local-part, not the Full Name the buyer typed.

Do not close this file by claiming any of the twelve are “fine because fulfillment emails exist.”

---

*End of 09 — Hosted checkout and payment links. Analysis only. 16 August 2026.*
