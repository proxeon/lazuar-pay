# W1-LP-020 — done

Hosted product checkout hop 1 is EN | BM. Typed dictionary under `modules/checkout/i18n` — no `next-intl`, no locale-prefixed routes, no API / TypeSpec / email / legal-body change. Public path stays `/{tenantSlug}/checkout/{productSlug}` plus existing `success` / `?cancelled=true` / `?sub_id=`.

Locale is `en` | `ms` only (`id` / `id-ID` stay EN). Resolution: `?lang=` / `?locale=` → cookie `lazuar_locale` → `Accept-Language` tag whose primary subtag is `ms` → `en`. Toggle writes the cookie (`Path=/`, 1 year, `SameSite=Lax`) and keeps `cancelled` / `sub_id`. `html lang` follows the cookie / Accept-Language on the root layout. Amounts use `Intl` `en-MY` / `ms-MY` (`RM`, not `MYR 50.00`). Product names and coupon codes are not translated.

## Files changed

### New dictionary (ADR 017 checkout slice)

- `apps/lazuar-portal/src/modules/checkout/i18n/locales.ts` — `en` | `ms`, `parseLocale`, `resolveCheckoutLocale`, cookie name.
- `messages.ts` — `en` `as const` + `ms: Record<MessageKey, string>` (76 keys). §6.1 catalog plus keys already visible from LP-014 (quantity / recurring / auto-debit) and `meta.checkoutTitle`.
- `format.ts` — `{name}` / `{product}` / `{year}` interpolate; `formatMoney` / `currencySymbol`.
- `errors.ts` — `includes` map for promo / `Payment gateway` / email provider; unknown C# → generic; phone / tax / address `detail` passthrough.
- `translate.ts` — server `t(locale, key, vars?)`.
- `getCheckoutLocale.ts` — server resolve from query + cookie + `Accept-Language` (best-effort URL headers for layouts).
- `CheckoutI18n.tsx` — checkout-only provider, `useCheckoutT()`, EN | BM header toggle.
- `i18n.test.mjs` — node:test (no Vitest).

### Wired surfaces

- `src/app/layout.tsx` — `lang={locale}`; footer labels via `t()`; legal hrefs unchanged.
- `src/app/not-found.tsx` — three not-found strings.
- `checkout/[productSlug]/layout.tsx` — provider + toggle next to padlock; title `Checkout · {product}` / `Bayar · {product}`.
- `checkout/[productSlug]/page.tsx` — `generateMetadata` sees `?lang=`.
- `CheckoutView.tsx` — cancel banner + remapped errors (stale exact gateway substring replaced with `includes("payment gateway")`).
- `CheckoutForm.tsx` / `IdentityBanner.tsx` / `OrderSummaryCard.tsx` / `PromoCodeInput.tsx` / `CheckoutSuccessView.tsx` — buyer chrome through `t()`.

### Tracker

- `plans/007-feats/00-checklist-tracker.md` — LP-020 Lazuar `N` → `Y`. Competitor columns untouched.

`lib/api.ts` still throws English. UI maps those phrases. Legal page bodies stay English.

## Tests run

- `node --experimental-strip-types --test apps/lazuar-portal/src/modules/checkout/i18n/i18n.test.mjs` — **14 passed**, 0 failed. Covers `parseLocale("id")` → `null`, `parseLocale("ms-MY")` → `ms`, resolve order, key parity, `RM` money, gateway remap.
- `npx tsc --noEmit -p apps/lazuar-portal/tsconfig.json` — only pre-existing `CommunityPortalView.tsx` (`at_period_end` missing). No errors in LP-020 files.
- ESLint on new i18n files + layout / not-found / header / summary / promo / identity — clean.

Manual §8.2 (incognito Accept-Language, Billplz hop-2 return, legal body still EN) **not run** here.

Not committed. Not pushed.
