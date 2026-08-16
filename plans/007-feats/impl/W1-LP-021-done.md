# W1-LP-021 — done

Hosted product checkout hop 1 and success are usable on a 320–430 px phone: no forced second `100vh`, no 14 px inputs, address stacks, identity banner wraps. **CSS / layout only.** No checkout redesign, no hop-2 methods, no QR, no poller or copy rewrite. LP-020 EN | BM dictionary and toggle are unchanged.

Layout slice of implement-ids `LP-021` is shipped. Tracker “wallet QR on our page” is **not** this ticket (hop 2 / LP-033–037 / LP-045).

## What changed

### G4 — nested `min-h-screen`

- `checkout/[productSlug]/layout.tsx` — wrapper `flex flex-1 flex-col min-h-0` (fills space above the root footer; does not claim another `100vh`). `main` is `flex-1 flex flex-col w-full min-h-0`.
- `CheckoutSuccessView.tsx` — all five shells (`VERIFYING` / `SUCCESS` / `TIMEOUT` / `EXPIRED` / `ERROR`) drop `min-h-screen` for `flex-1 … w-full`. Cards `p-6 sm:p-8 md:p-12`. Poll constants, `COMPLETED`-only paid, and `t()` copy untouched.
- `success/page.tsx` `Suspense` fallback matches the flex-1 shell.

### G2 / G7 — inputs

- Visible hop-1 `<input>`s are `text-base` (16 px): name, email, tel, street, city, postal, state, country, promo, PWYW, quantity value.
- Autofill: `name`, `email`, `tel`, `address-line1`, `address-level2`, `postal-code`, `address-level1`, `country`. Postal `inputMode="numeric"`. Name `autoCapitalize="words"`. PWYW `inputMode="decimal"`.
- No `user-scalable=no` / `maximum-scale=1`.

### G3 — address grid

- `grid-cols-1 sm:grid-cols-2`. Visible `<label>` + `id` / `htmlFor` on city, postal, state, country (existing `form.*` keys). Still a raw ISO-2 text box (LP-020). TIN block stays `[MVP-HIDE]`.

### G1 / G6 — IdentityBanner

- Stacks below `sm` (`flex-col gap-2` → `sm:flex-row sm:justify-between`). Label `min-w-0 break-words`. Toggle `min-h-11 shrink-0`. Three color skins kept. Still `t("id.*")`.

### G5 / G6 — summary / promo / PWYW

- Title `min-w-0 break-words`. Total (and subtotal / discount) rows `flex-wrap`. Amount `tabular-nums shrink-0`.
- PWYW `h-11 w-24 text-base`. Promo input wrapper `min-w-0 flex-1`, input `h-11 text-base`, Apply/Remove `h-11 shrink-0`.
- Pay CTA stays `w-full h-14`.

### G8 — xs padding

- `CheckoutView` `py-4 sm:py-8 md:py-12`; banners `break-words`.
- `CheckoutLayout` `gap-4 lg:gap-6`; form card `p-4 sm:p-6 lg:p-8`. `lg:flex-row` / `lg:w-[380px]` unchanged.
- Root footer `pt-4 sm:pt-6` + `pb-[max(1rem,env(safe-area-inset-bottom))]` (sm: 1.5 rem). Links and `t()` labels unchanged.

### Header overflow (post LP-020 / LP-025 chrome)

BM `chrome.poweredBy` (“Dikuasakan oleh Lazuar”) + EN | BM + padlock overflowed ~320 px. Powered-by **text** is `hidden sm:inline`; padlock and the language toggle stay visible. No copy change. Logo / name remain LP-025.

## Files changed (this ticket)

- `apps/lazuar-portal/src/app/layout.tsx` — footer pad only.
- `apps/lazuar-portal/src/app/[tenantSlug]/checkout/[productSlug]/layout.tsx` — `flex-1` chrome (branding fetch already on this branch from LP-025).
- `apps/lazuar-portal/src/app/[tenantSlug]/checkout/[productSlug]/success/page.tsx`
- `apps/lazuar-portal/src/modules/checkout/components/CheckoutSuccessView.tsx`
- `apps/lazuar-portal/src/modules/checkout/components/CheckoutForm.tsx`
- `apps/lazuar-portal/src/modules/checkout/components/CheckoutLayout.tsx`
- `apps/lazuar-portal/src/modules/checkout/components/CheckoutView.tsx`
- `apps/lazuar-portal/src/modules/checkout/components/IdentityBanner.tsx`
- `apps/lazuar-portal/src/modules/checkout/components/OrderSummaryCard.tsx`
- `apps/lazuar-portal/src/modules/checkout/components/PromoCodeInput.tsx`
- `apps/lazuar-portal/src/modules/checkout/i18n/CheckoutI18n.tsx` — powered-by `hidden sm:inline` only. Toggle + messages untouched.
- `plans/007-feats/00-checklist-tracker.md` — LP-021 Lazuar `N` → `Y` (layout). Competitor columns untouched.

No new components, no `useIsMobile`, no API / TypeSpec, no `QuoteView`, no quantity/TIN/QR/logo work.

## Tests run

- `npx tsc --noEmit -p apps/lazuar-portal/tsconfig.json` — clean.
- `pnpm --filter lazuar-portal test` (`i18n.test.mjs`) — **14 passed**, 0 failed. EN/BM key parity still holds.
- ESLint on the files above — only pre-existing `CheckoutForm`/`CheckoutView` `any` and `CheckoutSuccessView` poller `setState`-in-effect. No new lint.

### §8 smoke

Device-mode widths (320 / 360 / 390 / 430 / 768 / 1024) and a real-iPhone focus-zoom check **not run** here (no portal session / device lab). Implementer still owes `document.documentElement.scrollWidth > clientWidth === false` and `visualViewport.scale === 1` on focus.

Static class audit vs §8.1–8.3:

| Check | Status |
|-------|--------|
| Address one column below `sm` | `grid-cols-1 sm:grid-cols-2` |
| Inputs ≥ 16 px | `text-base` on every hop-1 field |
| Identity banner stacks + 44 px toggle | `flex-col` / `min-h-11` |
| Promo / PWYW ≥ 44 px | `h-11` |
| Success not `header + 100vh + footer` | `flex-1` shells |
| `lg` form left / 380 px summary | unchanged |
| Guest path still hides banner | `if (!userName) return null` |
| Submit / poller | untouched |

Quantity stepper buttons stay `h-8` (LP-014; not in G6). Wallet QR still absent.

Not committed. Not pushed.
