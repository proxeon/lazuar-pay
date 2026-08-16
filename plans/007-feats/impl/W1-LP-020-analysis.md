# W1-LP-020 — BM / EN localization on hosted checkout

**Status:** Analysis only — **do not implement from this file**  
**Date:** 2026-08-16  
**ID authority:** [00-implement-ids.md](../00-implement-ids.md) Wave 1 row `LP-020` (“BM / EN”). Tracker label in [00-checklist-tracker.md](../00-checklist-tracker.md) is “BM / EN localization” (Wave 1, `Ours = N`). Grouped with LP-021 / LP-025 as checkout conversion.  
**Not this ID:** [01-lazuar-feature-inventory.md](../01-lazuar-feature-inventory.md) reuses `LP-020` for “One-time + monthly + yearly” (already shipped). [18-pricing-onboarding-trust.md](../18-pricing-onboarding-trust.md) reuses `LP-020` for “Partner / affiliate portal + MassPay” (**refuse**). Ignore those meanings.  
**Evidence (do not reopen as product strategy):** [09-checkout-and-payment-links.md](../09-checkout-and-payment-links.md) §7; [16-communications-whatsapp-email.md](../16-communications-whatsapp-email.md) §10 / Localization; [20-sequencing-and-tracker-schema.md](../20-sequencing-and-tracker-schema.md) `LP-UX-001`.

**Invariant:** A Malaysian buyer on hosted product checkout can read hop-1 chrome, errors, and success/cancel copy in Bahasa Malaysia or English, without changing public checkout URLs, gateway redirect URLs, or any API contract.

---

## 0. Scope lock

In scope:

- Hosted product checkout (`/{tenantSlug}/checkout/{productSlug}`)
- Success / expired / timeout / invalid-session states on the same product
- `?cancelled=true` banner
- Checkout header (“Powered by Lazuar”) + language toggle
- Root footer **labels** (Terms / Privacy / Refund Policy / copyright) as seen from checkout
- Product-not-found `not-found.tsx` (buyer hits this on a bad slug)
- Currency **display** via `Intl` (`en-MY` / `ms-MY`)
- Smallest i18n mechanism that can do the above

Out of scope (do not expand this ticket):

- Locale-prefixed routes (`/ms/...`, `/en/...`)
- `next-intl` plugin + `[locale]` routing (see §5)
- Legal **bodies** (`/legal/terms|privacy|refund`) — English contract copy stays
- Buyer dashboard, magic-link form, cancel-plan, `CommunityPortalView`
- Update-payment / arrears (`/{tenant}/update-payment/{subId}`)
- Custom quote / `QuoteView` / `/{tenant}/pay/{id}` (`notFound()`, `[MVP-HIDE]`)
- Email / WhatsApp catalog (LP-151 / LP-153 / Meta `ms` templates)
- Ops, admin, developers, docs, sample cashier
- Merchant product names, coupon codes, brand “Lazuar”
- Hop-2 Billplz / CHIP / Stripe hosted pages (except the optional Stripe `locale` note in §7.3)
- Quantity stepper (LP-014), mobile layout (LP-021), branding/logo (LP-025), TIN (LP-022)
- CRM / checkout-session / product `locale` column
- Translating `ProblemDetails.detail` on the API

**Sibling conversion tickets — do not mix:**

| ID | Job |
|----|-----|
| LP-021 | Mobile-first / wallet QR on our page |
| LP-025 | Branding (logo, colors) on checkout |
| LP-014 | Quantity control (state exists; no JSX) |
| LP-022 | Company + TIN fields |

---

## 1. Product contract

Sellable sentence after this ticket:

> A buyer opening a Lazuar payment link can switch hop 1 between **EN** and **BM**. Choice survives the hop-2 redirect in the same browser. Amounts look like Malaysian money (`RM 50.00`). Merchant-written names stay as typed.

| Input | Result |
|-------|--------|
| First visit, `Accept-Language` has `ms` / `ms-MY` | Hop 1 renders BM |
| First visit, otherwise | Hop 1 renders EN |
| `?lang=ms` or `?lang=en` | That locale wins; cookie written |
| Toggle EN \| BM | Cookie written; same URL (query may be updated); no full navigation to a new path |
| Return from Billplz/Stripe/CHIP to `…/success?sub_id=` | Same locale as hop 1 (cookie) |
| Product name / coupon code | Never translated |
| Legal page opened from the consent line | Still English (labels in the footer may be BM) |
| `id` / `id-ID` (Bahasa Indonesia) | **Not** BM |

Done is **not** “the whole portal is bilingual.” Done is hop-1 + success + the strings a buyer cannot avoid on that path.

Tracker today (`Ours = N`) is honest: `html lang="en"` is hardcoded; every checkout string is English in JSX.

---

## 2. What exists

There is **no** i18n library in this repo.

| Surface | Fact |
|---------|------|
| `lazuar-portal` deps | Next `16.2.9`, React 19. No `next-intl`, no `i18next`, no `lingui` |
| `next.config.ts` | `output: "standalone"`, `basePath: process.env.NEXT_BASE_PATH \|\| ""` (prod `/portal`) |
| Middleware | **None** |
| `html lang` | `"en"` in root layout |
| `locale` grep in apps | date-picker `date-fns` props only |
| Types | `CheckoutContext` has no locale |
| API | No locale on product, session, CRM, templates, or `PublicCheckoutRequestDto` |
| Currency display | `{currency} {n.toFixed(2)}` → `MYR 50.00` (no `Intl`, no `RM`) |
| Dates on checkout | None. Portal dashboard uses `toLocaleDateString()` / `"en-MY"` — out of scope |
| Font | Geist `subsets: ["latin"]` — BM is Latin. No extra font |

Production buyer URL (already in the wild, already stamped onto gateway sessions):

```
{App:ClientUrl}/{tenantSlug}/checkout/{productSlug}
{App:ClientUrl}/{tenantSlug}/checkout/{productSlug}/success?sub_id={session.Id}
{App:ClientUrl}/{tenantSlug}/checkout/{productSlug}?cancelled=true
```

`App:ClientUrl` is `https://hub.lazuar.com/portal`. Adding a `[locale]` segment would break every existing WhatsApp link and every already-issued `success_url` / `cancel_url`.

### 2.1 Checkout files (the only slice)

| Path | Role | Hardcoded EN? |
|------|------|----------------|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-portal/src/app/layout.tsx` | `html lang`, metadata, global footer | Yes |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-portal/src/app/not-found.tsx` | Bad slug / archived product | Yes |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-portal/src/app/[tenantSlug]/checkout/[productSlug]/layout.tsx` | Blind header “Powered by Lazuar” | Yes |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-portal/src/app/[tenantSlug]/checkout/[productSlug]/page.tsx` | SSR product + auth; no copy | — |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-portal/src/app/[tenantSlug]/checkout/[productSlug]/success/page.tsx` | Suspense + `CheckoutSuccessView` | spinner only |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-portal/src/modules/checkout/components/CheckoutView.tsx` | Cancel banner, global error, coupon catch | Yes |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-portal/src/modules/checkout/components/CheckoutForm.tsx` | Labels, placeholders, consent, CTA | Yes |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-portal/src/modules/checkout/components/IdentityBanner.tsx` | Guest / admin / logged-in | Yes |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-portal/src/modules/checkout/components/OrderSummaryCard.tsx` | Subtotal / discount / total | Yes |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-portal/src/modules/checkout/components/PromoCodeInput.tsx` | Promo chrome | Yes |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-portal/src/modules/checkout/components/CheckoutSuccessView.tsx` | Five poller states | Yes |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-portal/src/modules/checkout/lib/api.ts` | Fallback error strings | Yes |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-portal/src/modules/checkout/components/CheckoutLayout.tsx` | Slots only | — |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-portal/src/app/legal/*` | Contract pages | Yes — **do not translate bodies** |

`CheckoutLayout` has no copy. `QuoteView` is orphaned. Quantity lives in `CheckoutView` state and is posted, but **there is no quantity control in the JSX** — do not invent one here.

### 2.2 Adjacent buyer strings (explicitly not this ticket)

| Surface | Why leave it |
|---------|----------------|
| `/{tenant}/portal` + `RequestMagicLinkForm` | Dashboard, not checkout |
| `CommunityPortalView.tsx` | Unused community chrome |
| `update-payment/[subId]/page.tsx` | Dunning recovery (LP-173 adjacent) |
| `DefaultMessageTemplates.cs` | Merchant-owned EN catalog; no locale column |
| Legal articles | Lawyer-facing EN; Malaysia governing law already stated |

Report 16 is correct: faking BM as a second body on the same email row without a locale field is worse than EN-only mail. Do not “fix” email here.

### 2.3 API errors that leak onto hop 1

Portal shows `error.detail` / `error_message` from the API. Those strings are English and stay English on the wire. Map **known** phrases on the client; do not add `Accept-Language` to Commerce.

| Source | English today | Buyer sees |
|--------|---------------|------------|
| `api.ts` coupon throw | `Invalid promo code.` / `This code cannot be applied.` | Promo error |
| `ValidateCouponQueryHandler` | `Invalid promo code.` / `Product not found.` | Same (200 + `is_valid: false`) |
| `Coupon.Validate` | archived / expired / max uses / min price / wrong product | `error_message` |
| `InitiateCheckoutCommandHandler` | workspace/email/product/coupon/phone/tax/address required | `error.detail` |
| `CheckoutSessionCashier` | `Payment gateway '{name}' is not configured for this workspace.` / `…disabled…` | `error.detail` |
| `CheckoutView.handleError` | Remaps a **stale** substring `"Payment gateway is not configured or active for this workspace"` | **Dead.** Current messages insert `'{name}'` in the middle |

That remap must become a prefix/regex/`includes("Payment gateway")` map to a friendly BM/EN string. Do not keep the exact stale phrase.

---

## 3. Competitor bar (compressed)

From report 09 §7 and the tracker row (`Billplz/CHIP/HitPay/Xendit = P`, `Stripe/Paddle/Chargebee = Y`):

- Nobody in the MY set markets **Bahasa Malaysia** as a first-class checkout locale. Xendit is EN + **Bahasa Indonesia**. Stripe has 35+ languages including `ms`, driven by browser or session `locale`.
- Real BM for FPX is **Maybank2u / CIMB / TnG after hop 2**. That is why we must not over-build hop-1 chrome (no bank-name dictionaries, no sen-as-words).
- EN-only hop 1 is acceptable when hop 2 is a Malaysian bank page. It is **not** acceptable when hop 2 is Stripe Checkout still in English for a BM-only buyer. Stripe `SessionCreateOptions.Locale` is unset today. Optional follow-up — not required to flip this row (see §7.3).
- HitPay / Billplz win on familiarity, not on a translation framework. A 60-key dictionary plus a toggle is enough to stop losing the first screen.

Do not copy Stripe’s 35-language routing. Two locales. Checkout only.

---

## 4. Locale model

**Codes**

| Store | Value |
|-------|--------|
| App locale | `en` \| `ms` |
| Toggle label | `EN` \| `BM` |
| `html lang` | `en` \| `ms` |
| `Intl` | `en-MY` \| `ms-MY` |
| Cookie | `lazuar_locale` (`en` \| `ms`) |

Reject `id`, `id-ID`, `zh`, and anything else. `ms-MY` / `ms-BN` in query or `Accept-Language` collapse to `ms`. `en-GB` / `en-US` / `en-MY` collapse to `en`.

**Resolution order** (first hit wins):

1. `?lang=` (also accept `?locale=`) if it maps to `en` \| `ms`
2. Cookie `lazuar_locale`
3. `Accept-Language` first tag whose primary subtag is `ms`
4. Default `en`

**Cookie**

- Name: `lazuar_locale` (same family as `lazuar_auth`)
- `Path=/` (covers `/portal` via Caddy and local `NEXT_BASE_PATH`)
- `Max-Age` ≥ 1 year, `SameSite=Lax`, not `HttpOnly` (client toggle must write it; or write via a tiny Route Handler)
- No `Domain` (stay on the hub host)

**Why not a path prefix**

`InitiateCheckoutCommandHandler` stamps:

```csharp
var successUrl = $"{clientUrl}/{request.TenantSlug}/checkout/{request.ProductSlug}/success?sub_id={session.Id}";
var cancelUrl  = $"{clientUrl}/{request.TenantSlug}/checkout/{request.ProductSlug}?cancelled=true";
```

Those URLs are already on live Billplz `redirect_url`, Stripe `SuccessUrl`/`CancelUrl`, CHIP `success_redirect`. Cookie survives the same-browser hop-2 return. Query `?lang=` is only for first-share and toggle. Do **not** ask the API to echo locale on success/cancel.

**SSR**

Checkout page and checkout layout are Server Components wrapping Client Components. Resolve locale on the server from cookies + `lang` searchParam + `Accept-Language`, pass `locale` as a prop (or a one-file provider **inside the checkout layout**). Do not add Next middleware just to set a cookie.

**`html lang`**

Root layout is the only `<html>`. Options:

| Option | Tradeoff |
|--------|----------|
| A. Root reads cookie / `lang` and sets `lang={locale}` for the whole portal | Smallest. Dashboard stays EN copy under `lang="ms"` until a later ticket |
| B. Only checkout layout pretends to set lang (client `document.documentElement.lang`) | Flicker; SSR first paint is `en` |
| **Pick A** | Accept the dashboard mismatch; it is out of scope and unauthenticated buyers never see it |

**Do not map Indonesia → Malaysia.** `id` is a different language. A Jakarta visitor gets EN.

---

## 5. Approach decision: dictionary, not next-intl

### 5.1 What “smallest next-intl” would actually be

Official no-routing setup ([next-intl App Router](https://next-intl.dev/docs/getting-started/app-router)):

1. Add `next-intl` to `lazuar-portal`
2. Wrap `next.config.ts` with `createNextIntlPlugin()` (already has `standalone` + `basePath`)
3. Add `src/i18n/request.ts` + `messages/en.json` + `messages/ms.json`
4. Wrap root `layout.tsx` in `NextIntlClientProvider`
5. Replace every checkout string with `useTranslations` / `getTranslations`

That is the **whole app** becoming an i18n host so ~60 checkout strings can move. Locale-based routing (`app/[locale]/...`) is explicitly rejected: it fights `App:ClientUrl`, existing links, and Docker `basePath=/portal`.

No-routing next-intl is a typed dictionary plus a plugin and a provider. The plugin is the cost (Next 16.2.9 compatibility unverified here; standalone copy in `Dockerfile` must still pick up the plugin output).

### 5.2 Typed dictionary (pick this)

Stay inside the checkout vertical slice (ADR 017). No new dependency. Compile-time key parity.

```
apps/lazuar-portal/src/modules/checkout/i18n/
  locales.ts          // Locales, parseLocale, cookie name, intlTag
  messages.ts         // `en` const + `ms satisfies Record<keyof typeof en, string>`
  format.ts           // formatMoney(locale, currency, amount)
  getCheckoutLocale.ts // server: cookie + searchParam + Accept-Language
  CheckoutI18n.tsx     // client provider + `useCheckoutT()`
```

Footer / 404 / root `html lang` import `parseLocale` + a **small** `chrome` key group from the same `messages.ts` so we do not invent a second catalog.

**Why this is smaller than next-intl for LP-020**

| | Dictionary | next-intl (no routing) |
|--|------------|-------------------------|
| New npm dep | No | Yes |
| `next.config` / Docker standalone | Untouched | Plugin wrap |
| Middleware | No | Optional |
| Touches non-checkout app | Root `lang` + footer labels only | Provider around entire tree |
| ICU / rich text | 3 interpolations: `{name}`, `{product}`, `{year}` via replace | Built-in, unused |
| Swap later | `messages.ts` can feed next-intl JSON | Already there |

**Adopt next-intl later** if we localize dashboard + emails + legal and want a TMS. Do not pay that tax to ship hop-1 BM.

Interpolation: `{product}`, `{name}`, `{year}` only. No plural rules on this slice (quantity UI does not exist).

Type lock (the test):

```ts
export const en = { "cta.proceed": "Proceed to Payment", /* … */ } as const;
export type MessageKey = keyof typeof en;
export const ms: Record<MessageKey, string> = { "cta.proceed": "Teruskan ke Pembayaran", /* … */ };
```

`pnpm --filter lazuar-portal build` fails if BM is missing a key. Portal has no test runner; do not add Vitest for this.

---

## 6. String catalog

Do not translate commented `[MVP-HIDE]` TIN/company JSX. When LP-022 unhides it, add keys then.

`interval` is on `CheckoutContext` and **not rendered**. Do not add “per month” / “sebulan” until something displays it.

### 6.1 Keys (EN → proposed BM)

Tone: polite standard Malaysian (`anda`, not `awak` / informal `kamu`). Latin script. Keep “Lazuar”, “WhatsApp”, “FPX”, product names.

**Chrome**

| Key | EN | BM |
|-----|----|----|
| `chrome.poweredBy` | Powered by Lazuar | Dikuasakan oleh Lazuar |
| `chrome.langEn` | EN | EN |
| `chrome.langBm` | BM | BM |
| `chrome.langSwitch` | Language | Bahasa |
| `footer.copyright` | © {year} Lazuar Platform. All rights reserved. | © {year} Lazuar Platform. Hak cipta terpelihara. |
| `footer.terms` | Terms | Terma |
| `footer.privacy` | Privacy | Privasi |
| `footer.refund` | Refund Policy | Dasar bayaran balik |
| `meta.title` | Lazuar Portal | Portal Lazuar |
| `meta.description` | Secure checkout and buyer dashboard | Checkout selamat dan papan pemuka pembeli |

**Form**

| Key | EN | BM |
|-----|----|----|
| `form.accountDetails` | Account Details | Butiran akaun |
| `form.fullName` | Full Name | Nama penuh |
| `form.email` | Email Address | Alamat e-mel |
| `form.phone` | WhatsApp Number | Nombor WhatsApp |
| `form.phoneHint` | Required for delivery and important updates. | Diperlukan untuk penghantaran dan maklumat penting. |
| `form.phonePlaceholder` | +60 12-345 6789 | +60 12-345 6789 |
| `form.billingDetails` | Billing Details | Butiran bil |
| `form.billingAddress` | Billing Address * | Alamat bil * |
| `form.street` | Street Address | Alamat jalan |
| `form.city` | City | Bandar |
| `form.postal` | Postal Code | Poskod |
| `form.state` | State | Negeri |
| `form.country` | Country Code (e.g. MY) | Kod negara (cth. MY) |
| `form.consent` | By proceeding, you agree to Lazuar's {terms} and {privacy}, and acknowledge that your purchase is a direct transaction with the Creator. | Dengan meneruskan, anda bersetuju dengan {terms} dan {privacy} Lazuar, dan mengakui bahawa pembelian ini ialah transaksi terus dengan Pencipta. |
| `form.consentTerms` | Terms of Service | Terma Perkhidmatan |
| `form.consentPrivacy` | Privacy Policy | Dasar Privasi |
| `cta.proceed` | Proceed to Payment | Teruskan ke Pembayaran |
| `cta.securing` | Securing Data... | Menyediakan pembayaran… |

**Identity (only if `lazuar_auth` cookie exists)**

| Key | EN | BM |
|-----|----|----|
| `id.guest` | Checking out as Guest | Membayar sebagai tetamu |
| `id.useAccount` | Use my Lazuar account | Guna akaun Lazuar saya |
| `id.admin` | Viewing as Workspace Admin | Melihat sebagai pentadbir ruang kerja |
| `id.asGuest` | Checkout as Guest | Checkout sebagai tetamu |
| `id.loggedIn` | Logged in as {name} | Log masuk sebagai {name} |

**Summary / promo**

| Key | EN | BM |
|-----|----|----|
| `summary.title` | Order Summary | Ringkasan pesanan |
| `summary.subtotal` | Subtotal | Jumlah kecil |
| `summary.discount` | Discount | Diskaun |
| `summary.total` | Total Due Today | Jumlah perlu dibayar hari ini |
| `promo.label` | Promo Code | Kod promo |
| `promo.placeholder` | ENTER CODE | MASUKKAN KOD |
| `promo.apply` | Apply | Guna |
| `promo.remove` | Remove | Buang |

**Banners / client errors**

| Key | EN | BM |
|-----|----|----|
| `banner.cancelled` | Payment was cancelled or failed. Please try again or use a different payment method. | Pembayaran dibatalkan atau gagal. Sila cuba lagi atau guna kaedah pembayaran lain. |
| `error.generic` | An error occurred during checkout. | Ralat berlaku semasa checkout. |
| `error.invalidPromo` | Invalid promo code. | Kod promo tidak sah. |
| `error.promoNotApplicable` | This code cannot be applied. | Kod ini tidak boleh digunakan. |
| `error.gatewayDown` | This creator is currently updating their payment settings. Please try again later. | Peniaga ini sedang mengemas kini tetapan pembayaran. Sila cuba lagi kemudian. |
| `error.missingConfirmUrl` | Checkout completed but the confirmation link was missing. Please check your email. | Checkout selesai tetapi pautan pengesahan tiada. Sila semak e-mel anda. |
| `error.submitFailed` | Checkout submission failed. | Checkout gagal dihantar. |
| `error.statusFailed` | Status check failed. | Semakan status gagal. |

**Success poller**

| Key | EN | BM |
|-----|----|----|
| `success.invalidTitle` | Invalid Session | Sesi tidak sah |
| `success.invalidBody` | We could not verify your session. Please check your email for access links or contact support if you completed a payment. | Kami tidak dapat mengesahkan sesi anda. Sila semak e-mel untuk pautan akses, atau hubungi sokongan jika anda sudah membayar. |
| `success.verifyingTitle` | Verifying Transaction... | Mengesahkan transaksi… |
| `success.verifyingBody` | Please wait while we securely verify your transaction with the payment provider. | Sila tunggu sementara kami mengesahkan transaksi anda dengan pembekal pembayaran. |
| `success.expiredTitle` | Checkout Expired | Checkout tamat tempoh |
| `success.expiredBody` | This checkout session for {product} is no longer active. If you completed a payment, please check your email. Otherwise, start checkout again. | Sesi checkout untuk {product} tidak lagi aktif. Jika anda sudah membayar, sila semak e-mel. Jika tidak, mulakan checkout semula. |
| `success.returnCheckout` | Return to Checkout | Kembali ke checkout |
| `success.timeoutTitle` | Processing Payment | Pembayaran sedang diproses |
| `success.timeoutBody` | We are still processing your payment for {product}. Please check your email in a few minutes for your receipt. This page does not confirm payment until verification finishes. | Kami masih memproses pembayaran untuk {product}. Sila semak e-mel anda sebentar lagi untuk resit. Halaman ini tidak mengesahkan pembayaran sehingga pengesahan selesai. |
| `success.checkAgain` | Check again | Semak semula |
| `success.dashboard` | Go to Dashboard | Pergi ke papan pemuka |
| `success.completeTitle` | Order Complete! | Pesanan selesai! |
| `success.completeBody` | Your order for {product} is confirmed. Please check your email for your receipt. | Pesanan anda untuk {product} telah disahkan. Sila semak e-mel untuk resit. |

**404**

| Key | EN | BM |
|-----|----|----|
| `notFound.title` | Resource Not Found | Sumber tidak dijumpai |
| `notFound.body` | The checkout page or portal you are looking for does not exist, has been archived, or the link has expired. | Halaman checkout atau portal ini tidak wujud, telah diarkibkan, atau pautan telah tamat tempoh. |
| `notFound.home` | Return Home | Kembali ke laman utama |

~70 keys. That is the whole catalog.

### 6.2 API `detail` → key map (client)

Match **includes**, not exact equality (gateway name is interpolated).

| If `detail` includes | Use key |
|----------------------|---------|
| `Invalid promo code` | `error.invalidPromo` |
| `cannot be applied` / `not valid for the selected product` / `archived` / `expired` / `maximum usage` / `minimum original price` | `error.promoNotApplicable` (or keep EN `detail` if you want specificity — BM generic is fine) |
| `Payment gateway` | `error.gatewayDown` |
| `not configured an active email provider` | `error.gatewayDown` (same buyer meaning: creator not ready) |
| `requires a phone number` / `tax ID` / `billing address` | Keep EN `detail` **or** add three keys if those configs are on. Address/phone are rare. |
| else | `error.generic` — **do not** dump raw C# to a BM-only buyer |

Do not change C# exception text in this ticket.

### 6.3 Money

Replace `{currency} {n.toFixed(2)}` in `OrderSummaryCard` (and nowhere else on hop 1) with:

```ts
new Intl.NumberFormat(locale === "ms" ? "ms-MY" : "en-MY", {
  style: "currency",
  currency: context.currency || "MYR",
}).format(amount)
```

Expect `RM 50.00` in both. Do **not** invent “50 ringgit” / “50 sen”. PWYW input stays a number; prefix the field with `RM` or the formatted currency symbol, not the raw `MYR` ISO string.

---

## 7. Minimal code changes

No API, TypeSpec, migration, or hop-2 adapter change required to close the tracker row.

### 7.1 Must change

| File | Change |
|------|--------|
| **new** `apps/lazuar-portal/src/modules/checkout/i18n/locales.ts` | `en` \| `ms`, cookie name, `parseLocale`, `intlTag` |
| **new** `apps/lazuar-portal/src/modules/checkout/i18n/messages.ts` | `en` + `ms satisfies Record<keyof typeof en, string>` — catalog in §6.1 |
| **new** `apps/lazuar-portal/src/modules/checkout/i18n/format.ts` | `formatMoney` |
| **new** `apps/lazuar-portal/src/modules/checkout/i18n/getCheckoutLocale.ts` | Server resolve: `lang` query → cookie → `accept-language` → `en` |
| **new** `apps/lazuar-portal/src/modules/checkout/i18n/CheckoutI18n.tsx` | Client context: `locale`, `t(key, vars?)`, `setLocale` (writes cookie + `router.replace` with `lang`, keep `cancelled` / `sub_id`) |
| `src/app/layout.tsx` | `lang={locale}`; footer labels via `t()`; keep legal **hrefs** |
| `src/app/not-found.tsx` | `t()` for the three strings |
| `checkout/[productSlug]/layout.tsx` | Wrap children in `CheckoutI18n`; toggle EN \| BM next to padlock (text, no flags) |
| `checkout/[productSlug]/page.tsx` | Pass resolved `locale` into `CheckoutView` (and/or rely on layout provider) |
| `CheckoutView.tsx` | Cancel + remapped errors use `t()` |
| `CheckoutForm.tsx` | All visible strings + consent links |
| `IdentityBanner.tsx` | Four states |
| `OrderSummaryCard.tsx` | Labels + `formatMoney` |
| `PromoCodeInput.tsx` | Label / placeholder / Apply / Remove |
| `CheckoutSuccessView.tsx` | Five states; interpolate `{product}` (product name stays raw) |
| `lib/api.ts` | Keep English throws; UI maps them. Or throw stable codes (`INVALID_PROMO`) — optional, not required |

Toggle placement: checkout header, right side, `EN` / `BM` text buttons, current locale emphasized. Visible on mobile (LP-021 must not hide it later).

### 7.2 Should change (same ticket, small)

| Item | Why |
|------|-----|
| Fix gateway remap (stale substring, §2.3) | Friendly BM/EN instead of raw C# |
| `document.title` / metadata on checkout layout | `Bayar · {product}` / `Checkout · {product}` |
| Cookie write via `document.cookie` **and** a no-op `router.refresh()` | SSR success page after hop 2 already has the cookie; toggle on hop 1 should refresh server footer/`lang` |

### 7.3 Do not change

- `InitiateCheckoutCommandHandler` success/cancel URL shape
- TypeSpec / `PublicCheckoutRequestDto`
- `CheckoutSession` / CRM / products
- Email templates
- Legal page bodies
- `QuoteView`, pay route, portal dashboard, update-payment
- `next.config.ts`, Dockerfile, Caddy, `basePath`
- Adding `next-intl`
- Mapping `id` → `ms`
- Stripe/CHIP/Billplz adapters (see optional)

**Optional later (not required to close LP-020):** pass buyer locale into Stripe Checkout (`SessionCreateOptions.Locale = "ms" \| "en"`). Stripe documents `ms`. That needs a new optional field on initiate + cashier + adapter. Billplz/CHIP already speak MY bank UI. Only Stripe hop 2 stays English. Do it as a follow-up if a tenant’s default rail is Stripe and they complain; do not block hop-1 BM on an API change.

### 7.4 Provider shape (keep it local)

Prefer wrapping **only** `checkout/[productSlug]/layout.tsx`, plus calling the same `getCheckoutLocale()` from the root layout for `html lang` and footer. Do not wrap the entire `{children}` tree in a global next-intl-style provider.

ADR 017: dictionary lives under `modules/checkout` (domain). Root layout may import `modules/checkout/i18n` for the three footer keys — acceptable, because those keys exist for checkout buyers. Do not put messages under `components/ui`.

---

## 8. Tests and manual proof

Portal scripts are `dev` / `build` / `lint` only. The type lock in `messages.ts` is the automated test.

### 8.1 Must pass at build

- `ms` has every `en` key (`satisfies Record<MessageKey, string>`)
- `parseLocale("id")` → `null` (falls through to default `en`)
- `parseLocale("ms-MY")` → `ms`

Optional one-file node assert if you want it in CI without a runner — not required if `tsc` already fails.

### 8.2 Manual (this is the ticket)

1. Incognito, `Accept-Language: ms-MY` → hop 1 is BM, `html[lang=ms]`, amounts `RM …`.
2. Incognito, `Accept-Language: en-US` → EN.
3. Incognito, `Accept-Language: id-ID` → **EN**, not BM.
4. Append `?lang=ms` on an EN browser → BM + cookie. Reload without query → still BM.
5. Toggle to EN → cookie flips; `?cancelled=true` preserved.
6. Apply invalid coupon → BM error, not `Invalid promo code.`
7. Pay on Billplz/Stripe sandbox, return to `success?sub_id=` → BM success states (verifying / complete / timeout).
8. Open `/legal/terms` from the consent line → **English** body; Back works.
9. 404 unknown product slug → BM not-found if cookie is `ms`.
10. Share the raw link `/{tenant}/checkout/{slug}` (no `lang`) in WhatsApp → still 200, locale from browser/cookie. **URL shape unchanged.**

No API module tests. No webhook tests. Do not add Playwright unless the repo already grows a portal harness (it has not).

---

## 9. Risks

| Risk | Severity | Mitigation |
|------|----------|------------|
| next-intl + Next 16 + `standalone` + `basePath=/portal` | Med if chosen | **Don't.** Dictionary. |
| `[locale]` in the path | High | Breaks `App:ClientUrl` and in-flight gateway redirects. Forbidden. |
| Cookie blocked / hop-2 on another device | Low | Default EN; `?lang=` for merchants who care |
| Google-translated BM that sounds Indonesian | Med | Use the catalog in §6.1; native pass before ship; never copy Xendit ID |
| Raw C# `detail` after remap miss | Low | Gateway prefix map + generic fallback |
| Footer BM + legal body EN | Low | Honest: labels vs contract. Do not machine-translate terms |
| `html lang=ms` on English dashboard | Low | Out of scope; unauthenticated buyers never land there from hop 1 without a token |
| Translating product names | High if done | Never. Interpolation only |
| Scope creep into emails / TIN / quantity / branding | High | §0 lock |
| Over-investing hop-1 BM while hop 2 is the bank | Low | 70 keys, one toggle, no bank dictionary — matches report 09 |

---

## 10. Acceptance criteria

Close LP-020 (`Ours` N → Y) when all of the following are true:

1. Hosted product checkout and its success/cancel/404 chrome have **no leftover buyer-facing English** except: merchant product name, coupon code, brand “Lazuar”, phone placeholder, ISO country example `MY`, legal **page** bodies.
2. Locales are exactly `en` and `ms`. Toggle is EN \| BM. `id` does not become BM.
3. Public path is still `/{tenantSlug}/checkout/{productSlug}` (plus existing `success` / `?cancelled=true` / `?sub_id=`). No `/ms/` prefix.
4. Locale persists across hop 2 in the same browser via `lazuar_locale`.
5. `html` `lang` is `en` or `ms` to match the active locale.
6. Money uses `Intl` `en-MY` / `ms-MY`, not `MYR 50.00`.
7. `messages.ts` type-checks key parity. `lazuar-portal` build passes.
8. No new npm dependency. No TypeSpec / API / email / legal-body change.
9. Manual §8.2 recorded (even a short note in the done file).

Tracker flip: `00-checklist-tracker.md` LP-020 Lazuar cell `N` → `Y`. Do not touch the competitor columns.

---

## 11. Suggested implement order

1. `locales.ts` + `messages.ts` (EN copy lifted from JSX; BM from §6.1)  
2. `getCheckoutLocale` + `CheckoutI18n` + header toggle  
3. Replace strings in form / summary / promo / identity / view errors  
4. Success poller + 404 + root `lang` + footer labels  
5. `formatMoney` + gateway error remap  
6. Manual §8.2  

That is the whole ticket. If the implementer still prefers next-intl, the **only** allowed shape is no-routing + cookie + the same catalog — and they must justify the plugin against §5. The default is the dictionary.
