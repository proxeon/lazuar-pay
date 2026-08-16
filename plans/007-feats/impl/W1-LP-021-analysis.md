# W1-LP-021 — Mobile-first checkout (small-viewport layout)

**Status:** Analysis only — **do not implement from this file**  
**Date:** 2026-08-16  
**ID authority:** [00-implement-ids.md](../00-implement-ids.md) Wave 1 row `LP-021` (“Mobile-first checkout”). Tracker label in [00-checklist-tracker.md](../00-checklist-tracker.md) is “Mobile-first / wallet QR on our page” (`Ours = N`, Wave 1, grouped with LP-020 / LP-025 as checkout conversion). Sibling UX seed: [20-sequencing-and-tracker-schema.md](../20-sequencing-and-tracker-schema.md) `LP-UX-002` (“Mobile-first checkout conversion (complete in one thumb)”, seed `partial`).  
**Not this ID:** [01-lazuar-feature-inventory.md](../01-lazuar-feature-inventory.md) reuses `LP-021` for Trials. [18-pricing-onboarding-trust.md](../18-pricing-onboarding-trust.md) reuses `LP-021` for Hub national-ID KYC (refused). Ignore those meanings.

**Invariant:** Hosted product checkout on `lazuar-portal` must be usable on a 320–430 px phone without horizontal scroll, without iOS focus-zoom, and without a second blank viewport of padding. This ticket is **CSS / layout only**. It is not a checkout redesign, not hop-2 methods, and not branding.

---

## 0. Scope lock

In scope:

- Hosted product checkout hop 1: `/{tenant}/checkout/{slug}`
- Hosted product success / verifying / timeout / expired / invalid: `/{tenant}/checkout/{slug}/success`
- Blind checkout chrome that wraps both (`checkout/[productSlug]/layout.tsx`) plus the global root footer that always sits under it
- Tailwind classes, spacing, overflow, tap targets, input font-size, and a handful of native input attributes that exist only so mobile autofill / keyboards work

Out of scope (do not expand this ticket):

- Wallet / DuitNow QR / FPX bank grid / Apple Pay / Google Pay **on our page** (tracker’s extra phrase). Those are hop-2 gateway UI or later rails: LP-033–037, LP-045. [09-checkout-and-payment-links.md](../09-checkout-and-payment-links.md) already records hop 1 has zero methods by design.
- Checkout branding (logo, colors, merchant name in the header) — **LP-025**
- BM / EN copy — **LP-020**
- Quantity stepper (state exists, no control) — **LP-014**
- Company + TIN fields (`[MVP-HIDE]`) — **LP-022**
- Success-page payment truth (already closed in [W0-LP-024-done.md](./W0-LP-024-done.md)) — **LP-024**
- Embed / overlay checkout — **LP-018**
- Custom quote `/pay/[sessionId]` + `QuoteView.tsx` (`notFound()`, `[MVP-HIDE]`)
- Buyer dashboard, magic-link portal, update-payment, legal pages (except they share the root footer)
- Sample `examples/hub-cashier-next` (M2M; no portal hop 1)
- API, TypeSpec, Commerce handlers, fonts, dark-mode product decision, new JS breakpoint hooks

**Stale notes in `09` — do not reopen:** zero-amount missing `sub_id`, poller treating `ACTIVE` as paid, 10 × 2.5 s window. Those were LP-024. Current `CheckoutForm` assigns `result.url`; `CheckoutSuccessView` treats **only** `COMPLETED` as paid (20 × 3 s).

**How this analysis was done:** static class audit of the portal checkout tree at 320 / 360 / 390 / 430 / 768 / 1024. No live device session, no screenshot lab. Implementer must still smoke the widths in §8.

---

## 1. What “mobile-first” means here

Two readings of the same ID exist. This ticket uses the **implement-ids** reading, narrowed further by the assigned work (“audit portal checkout layout on small viewports; minimal CSS/layout fixes, not a redesign”).

| Reading | Source | This ticket? |
|---------|--------|--------------|
| Hop-1 page usable with one thumb on a phone | `00-implement-ids` “Mobile-first checkout”; `LP-UX-002` | **Yes — layout only** |
| Wallet QR / method list on *our* page | Tracker label; `00-evaluation` “show which rail”; HitPay/Xendit first paint | **No.** Hop 2 already owns methods. Do not draw a fake QR. |
| Stripe-like restyle, sticky new chrome, new components | Temptation | **No.** |

Tracker `Ours = N` is slightly harsh: the stack already reverses to “amount then form” under `lg`. After this ticket the **layout slice** should be `Y`. The **wallet-QR slice** stays `N` and is not part of closeout.

Competitor first-paint (from `09` §15) is out of reach without hop-2 work: Billplz/CHIP/HitPay show banks or wallets on pixel 1; we show amount + name/email. That two-hop tax is accepted. We only stop the hop-1 page from fighting the phone.

---

## 2. Viewport model (current chrome)

Breakpoints in play (Tailwind v4 defaults): `sm` 640, `md` 768, `lg` 1024. Checkout becomes two columns only at **`lg`**. iPad portrait stays stacked. Do not change that breakpoint.

### 2.1 Nesting

```
html (Next default viewport: width=device-width, initial-scale=1)
  body.min-h-screen.flex.flex-col          layout.tsx
    div.flex-1                             children
      tenant layout (passthrough)
        checkout layout.min-h-screen       [productSlug]/layout.tsx
          header.sticky.h-14               “Powered by Lazuar”
          main.flex-1
            CheckoutView  or  CheckoutSuccessView.min-h-screen
    footer.py-6                            Terms / Privacy / Refund
```

No `export const viewport`. No `viewport-fit=cover`. No `env(safe-area-inset-*)`. No `overflow-x` guard on `body` / checkout.

`hooks/use-mobile.ts` (`max-width: 767px`) is only used by unused shadcn `sidebar.tsx`. Checkout must stay **CSS-only**. Do not import that hook.

### 2.2 Width budget (CSS pixels)

`CheckoutView` is `px-4`. Form card and summary card are `p-6` below `sm`.

| Device width | After `px-4` | After card `p-6` (inner) |
|-------------:|-------------:|-------------------------:|
| 320 (SE 1 / very small Android) | 288 | **240** |
| 360 (common Android) | 328 | 280 |
| 390 (iPhone 14) | 358 | 310 |
| 430 (Pro Max class) | 398 | 350 |

Two-column address (`grid-cols-2 gap-4`) at 320: **(240 − 16) / 2 = 112 px** per field.

### 2.3 Vertical chrome (short path: name + email only)

| Piece | Approx. |
|-------|--------:|
| Sticky header | 56 |
| View `py-8` | 64 |
| Summary card (title + subtotal + promo + total) | ~280 |
| Layout `gap-6` | 24 |
| Form account block + legal + `h-14` CTA + card padding | ~400 |
| Root footer (`py-6`, wraps to two rows on xs) | ~80–96 |

Already **> 1 screen** on a 667–844 phone. That is acceptable if the extra pixels are content. It is not acceptable if they are a second `100vh` (success) or 14 px inputs that zoom the whole page.

---

## 3. Current files

| Path | Role on small viewports |
|------|-------------------------|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-portal/src/app/layout.tsx` | Geist variables; `body.min-h-screen`; **global footer always rendered** |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-portal/src/app/globals.css` | Tokens only. No checkout, safe-area, or 16 px input rule |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-portal/src/app/[tenantSlug]/layout.tsx` | Passthrough. ADR 017 “Fetches Tenant Theme/Colors” is still untrue (LP-025) |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-portal/src/app/[tenantSlug]/checkout/[productSlug]/layout.tsx` | Blind chrome. `min-h-screen` + sticky `h-14` header, padlock + “Powered by Lazuar” right-aligned. No merchant mark |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-portal/src/app/[tenantSlug]/checkout/[productSlug]/page.tsx` | SSR product + optional cookie auth. No layout classes |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-portal/src/app/[tenantSlug]/checkout/[productSlug]/success/page.tsx` | `Suspense` fallback is another `min-h-screen` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-portal/src/modules/checkout/components/CheckoutView.tsx` | `max-w-5xl mx-auto px-4 py-8 md:py-12`. Cancel / error banners |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-portal/src/modules/checkout/components/CheckoutLayout.tsx` | `flex-col-reverse lg:flex-row gap-6 items-start`. Form card `p-6 sm:p-8`. Summary `w-full lg:w-[380px]` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-portal/src/modules/checkout/components/CheckoutForm.tsx` | Name / email / optional phone / optional address. Raw `<input class="h-12 … text-sm">`. Address `grid-cols-2` **with no `sm:`**. CTA `w-full h-14`. `quantity` is submitted but **never rendered** |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-portal/src/modules/checkout/components/OrderSummaryCard.tsx` | Title, subtotal, optional PWYW `w-20 h-8 text-sm`, promo slot, “Total Due Today” single row `text-xl` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-portal/src/modules/checkout/components/PromoCodeInput.tsx` | `flex gap-2` + `h-10 text-sm` input + Apply/Remove |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-portal/src/modules/checkout/components/IdentityBanner.tsx` | `flex justify-between` + `text-[11px] uppercase tracking-widest` on **both** sides. No wrap / `min-w-0` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-portal/src/modules/checkout/components/CheckoutSuccessView.tsx` | Every state wraps `min-h-screen` + `max-w-md` card `p-8 sm:p-12` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-portal/src/modules/checkout/components/QuoteView.tsx` | Unrouted. Ignore |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-portal/next.config.ts` | `basePath` only. No headers / viewport |

Portal `package.json` scripts: `dev` / `build` / `lint` only. **No test runner.**

---

## 4. What is already correct

1. **Amount before identity on small screens.** `CheckoutLayout` `flex-col-reverse` until `lg`. Right order (see `09` § “Two-hop tax on mobile”).
2. **Blind checkout (ADR 017 Rule 3).** No sidebar, no marketing nav, no “log in to continue” wall. Cookie identity is optional sugar via `IdentityBanner`.
3. **Primary fields are full width.** Name, email, phone, street, CTA are `w-full`. CTA `h-14` (56 px) meets the 44 px minimum.
4. **Account inputs are `h-12` (48 px).** Height is fine; **font-size is not** (§5 G2).
5. **Phone uses `type="tel"`.** Number pad on iOS/Android when `requires_phone`.
6. **Summary and form are `w-full` below `lg`.** No fixed 380 px column on a phone.
7. **Legal links open in a new tab** (`target="_blank"`), so the form is not lost.
8. **Success cards are `max-w-md w-full` + `p-4` page pad.** Horizontally they fit; the bug is the extra `100vh`, not the card width.
9. **SSR product fetch, small tree, no product image LCP.** Keep it that way (no new hero, no method-logo strip).
10. **Payment truth on success is already honest (LP-024).** Do not touch poll logic, copy, or `sub_id` handling except the wrapper classes.

`09` §15 “Helps” list still holds. This ticket only removes the “Hurts” that are **layout**.

---

## 5. Exact gaps

### G1 — Horizontal overflow from `IdentityBanner` (cookie session only)

`flex items-center justify-between` with no `flex-wrap`, no `min-w-0`, and `text-[11px] font-bold uppercase tracking-widest` on **both** children.

`tracking-widest` is `0.1em`. At 11 px, “VIEWING AS WORKSPACE ADMIN” + “CHECKOUT AS GUEST” plus `p-3` wants **~360 px**. Guest-mode “CHECKING OUT AS GUEST” + “USE MY LAZUAR ACCOUNT” is the same. Inner width at 320 is 240–288. The row overflows the card and the page.

Logged-in “✓ Logged in as {userName}” overflows as soon as the name is a normal Malay / company string.

Guest-only buyers (the common path) never mount the banner. Still a real bug for cookie sessions and for the workspace-admin preview path the banner exists to serve.

### G2 — iOS Safari zooms on focus (`text-sm` = 14 px)

Every hop-1 text field is `text-sm`:

- `CheckoutForm` name, email, tel, street, city, postal, state, country
- `PromoCodeInput` code
- `OrderSummaryCard` PWYW `type="number"`

iOS Safari zooms the viewport when a focused control is **&lt; 16 px**. The buyer then pans a zoomed form to reach “Proceed to Payment.” This is the highest-frequency mobile checkout defect in the tree.

Do **not** “fix” it with `user-scalable=no` / `maximum-scale=1` (a11y, and Apple ignores it on recent iOS anyway). Set the input text to **16 px** (`text-base`).

### G3 — Address grid is two columns at every width

```201:216:apps/lazuar-portal/src/modules/checkout/components/CheckoutForm.tsx
              <div className="grid grid-cols-2 gap-4">
                ...
                    placeholder="City"
                ...
                    placeholder="Postal Code"
              </div>
              <div className="grid grid-cols-2 gap-4">
                ...
                    placeholder="State"
                ...
                    placeholder="Country Code (e.g. MY)"
```

112 px fields at 320. Placeholders clip. Country is a raw ISO-2 text box (honesty issue for LP-020; **here** only the grid is in scope). Hidden TIN block already uses `grid-cols-1 sm:grid-cols-2` — live address fields should match.

Fires only when `checkout_configuration.requires_address` is true. Default products may hide it; any address-required product is hostile on a 390 px phone (`09` §15 already called this).

### G4 — Nested `min-h-screen` + root footer = extra blank viewport

Three stacked 100 vh claims:

1. `body.min-h-screen`
2. Blind checkout root `min-h-screen` (includes the sticky header)
3. `CheckoutSuccessView` (and the `Suspense` fallback) `min-h-screen` **inside** `main`

On success / verifying / timeout / expired / invalid:

`header (56) + 100vh card column + footer (~80) ≈ 100vh + 136 px`

The card is “centered” in a column taller than the phone, then the Lazuar footer sits below that. The buyer scrolls to empty zinc. Short hop-1 (name + email, no banner, no address) hits the same pattern whenever content + header &lt; 100 vh: the checkout wrapper still forces 100 vh **and then** the footer.

Form-heavy pages (address on) already exceed 100 vh, so G4 is a no-op there. G4 is the success-path bug.

### G5 — Single-row money / promo / PWYW can overflow 240 px

- “Total Due Today” (`text-base`) + `{currency} {amount}` (`text-xl tracking-tighter`) is `flex justify-between` with no wrap. At 240 px inner it is ~238 px for `MYR 1234.00` and overflows for `MYR 10000.00` or a long ISO code.
- Product title has no `min-w-0` / `break-words` (long unspaced names).
- Promo is `flex gap-2` + `h-10` Apply/Remove. Fits if the button is `shrink-0` and the input is `min-w-0` (input is `w-full` but the flex item is not `min-w-0` — default `min-width: auto` can overflow).
- PWYW control is `h-8` (32 px) and `text-sm` (G2 again).

### G6 — Tap targets under 44 px

| Control | Now | Problem |
|---------|-----|---------|
| Identity banner text buttons | 11 px text, no `min-h` | Hard to hit; also G1 |
| Promo Apply / Remove | `h-10` (40 px) | 4 px under Apple HIG |
| PWYW amount | `h-8` (32 px) | Miss-taps |

Account fields and the pay CTA are already ≥ 48 px.

### G7 — No autofill / inputMode hints

Not CSS, but one-thumb conversion. Fields have `id` + `htmlFor` on name/email/phone only. Address city/postal/state/country have **no** `<label>` (placeholder only) and **no** `autoComplete`. PWYW has no `inputMode="decimal"`. Postal has no `inputMode="numeric"`.

Browser heuristics will fill name/email some of the time. They will not fill MY postcodes or map “Country Code (e.g. MY)” to `country`.

### G8 — Padding tax on 320 px (minor)

`px-4` + `p-6` + `py-8` + `gap-6` is a lot of zinc around two white cards. Not broken; just shrinks G1/G5’s budget. A one-step tighten on xs (not a new spacing scale) is enough.

### Not gaps for this ticket

| Observation | Why not LP-021 |
|-------------|----------------|
| Header says Lazuar, not the merchant | LP-025 |
| No BM, `lang="en"` | LP-020 |
| Quantity state with no control | LP-014 |
| PWYW summary ≠ server charge (`09` Appendix C.1) | Honesty / pricing bug, not layout |
| “Total Due Today” on `mo`/`yr` with no interval line | Honesty copy; not viewport |
| Promo always visible | Product choice; hiding it is a redesign |
| Pay CTA below the form (not sticky) | Sticky bar + iOS keyboard is a redesign; skip |
| Two origins (portal → Billplz/Stripe/CHIP) | Architecture; Wave 4 / LP-018 |
| No method logos / QR on hop 1 | Rails / LP-037 / tracker extra phrase |
| System dark mode on checkout | Do not force light here (branding-adjacent) |
| Geist downloaded via `next/font` | Bytes, not layout; `next/font` is self-hosted |
| `QuoteView` table needs `overflow-x-auto` | Unrouted |
| Update-payment also uses `min-h-screen` | Adjacent journey; same *pattern* optional only |
| Success “Go to Dashboard” has no token | LP-024 / policy; do not mint |

---

## 6. Minimal code changes

Class-only (plus native input attributes in 6.2). No new components, no `useIsMobile`, no new routes, no API.

### 6.1 Must change

| File | What | Change |
|------|------|--------|
| `…/checkout/[productSlug]/layout.tsx` | Root wrapper + `main` | Replace `min-h-screen` with `flex-1 flex flex-col min-h-0` so the blind chrome fills **remaining** space above the root footer. Make `main` `flex-1 flex flex-col w-full min-h-0`. Leave the sticky header as-is (no logo, no copy change). |
| `CheckoutSuccessView.tsx` | All five wrappers + do not touch poll/copy | Replace `min-h-screen flex flex-col items-center justify-center p-4` with `flex-1 flex flex-col items-center justify-center p-4`. Card: `p-6 sm:p-8 md:p-12` (today `p-8 sm:p-12` is heavy on 320). |
| `success/page.tsx` | `Suspense` fallback | Same `flex-1 …` as success; drop `min-h-screen`. |
| `CheckoutForm.tsx` | Inputs | `text-sm` → `text-base` on every visible `<input>`. Address grids: `grid-cols-1 sm:grid-cols-2 gap-4`. |
| `CheckoutForm.tsx` | Address a11y (needed for G3 to be usable) | Add a visible `<label>` per city / postal / state / country (they are placeholder-only today). Do not invent a country `<select>` (that is LP-020). |
| `IdentityBanner.tsx` | Row | `flex flex-col items-stretch gap-2 sm:flex-row sm:items-center sm:justify-between`. Text: `min-w-0 break-words` (drop forced single-line). Buttons: `min-h-11 shrink-0 self-start sm:self-auto`. Keep the three color skins. |
| `OrderSummaryCard.tsx` | Title + total + PWYW | Title: `min-w-0 break-words`. Total row: `flex flex-wrap items-baseline justify-between gap-x-3 gap-y-1`. Amount: `tabular-nums shrink-0`. PWYW input: `h-11 w-24 text-base`. |
| `PromoCodeInput.tsx` | Row | Input wrapper `min-w-0 flex-1`; input `h-11 text-base`. Buttons `h-11 shrink-0`. |
| `CheckoutView.tsx` | Page pad | `px-4 py-8 md:py-12` → `px-4 py-4 sm:py-8 md:py-12`. Banners: `break-words`. |
| `CheckoutLayout.tsx` | Gap + card pad | `gap-6` → `gap-4 lg:gap-6`. Form card `p-6 sm:p-8` → `p-4 sm:p-6 lg:p-8`. |

Do not change `lg:flex-row` / `lg:w-[380px]`. Desktop two-column must look the same.

### 6.2 Should change (same ticket, still tiny)

| File | Change |
|------|--------|
| `CheckoutForm.tsx` | `autoComplete`: `name`, `email`, `tel`, `address-line1`, `address-level2` (city), `postal-code`, `address-level1` (state), `country`. `inputMode="numeric"` on postal. `autoCapitalize="words"` on name (optional). |
| `OrderSummaryCard.tsx` | PWYW: `inputMode="decimal"` (keep `type="number"`). |
| `src/app/layout.tsx` footer | `py-6` → `py-4 sm:py-6`. Optionally `pb-[max(1rem,env(safe-area-inset-bottom))]`. This is the only root-file touch; it also helps portal/legal. Do not hide the footer on checkout (legal is linked from the CTA block). |
| `globals.css` (only if you refuse to touch every `text-sm`) | A scoped rule is worse than repeating `text-base` on the seven fields. Prefer the class change. |

### 6.3 Do not change

- Sticky / duplicated pay bar
- `user-scalable=no`, `maximum-scale=1`, `viewport-fit=cover` (cover without rewriting safe-areas is a notch regression)
- Header copy, merchant logo, theme tokens, forced `.light`
- Promo visibility, quantity UI, TIN block uncomment, interval / SST lines
- `CheckoutSuccessView` poll constants, status mapping, copy
- `CheckoutForm` submit / zero-amount navigation
- `QuoteView`, `/pay/[sessionId]`, update-payment, community portal
- `hooks/use-mobile.ts`, new media-query JS
- API / TypeSpec / sample cashier

### 6.4 Optional later (not required to close LP-021)

- Sticky CTA above the keyboard (`lg:static sticky bottom-0` + safe-area). Revisit only if smoke shows the pay button is still a second-screen hunt after G4/G8. iOS keyboard + sticky is the reason it is **not** in 6.1.
- `export const viewport` with `viewportFit: "cover"` **only** if a future sticky bar needs it.
- Apply G4’s `flex-1` pattern to `update-payment/[subId]/page.tsx` (not a checkout route).
- `text-size-adjust` hacks — unnecessary once inputs are 16 px.

---

## 7. Suggested class diffs (illustrative, not a patch)

Blind layout — fill the column above the footer, do not claim a second `100vh`:

```tsx
<div className="flex flex-1 flex-col min-h-0 bg-zinc-50 dark:bg-black">
  <header className="sticky top-0 z-40 …">{/* unchanged */}</header>
  <main className="flex-1 flex flex-col w-full min-h-0">{children}</main>
</div>
```

Success shell:

```tsx
<div className="flex-1 flex flex-col items-center justify-center p-4">
  <div className="bg-card … p-6 sm:p-8 md:p-12 rounded-none max-w-md w-full text-center">
```

Identity banner:

```tsx
<div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between p-3 … mb-4">
  <p className="text-[11px] font-bold uppercase tracking-widest … min-w-0 break-words">…</p>
  <button type="button" className="text-[11px] font-bold uppercase tracking-widest … min-h-11 shrink-0">
```

Address:

```tsx
<div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
```

Inputs (repeat on each field):

```tsx
className="flex h-12 w-full … px-3 py-1 text-base …"
autoComplete="email"
```

---

## 8. Tests / smoke

Portal has no unit or e2e harness. **Do not add Playwright / Vitest for CSS.** `pnpm --filter lazuar-portal lint` + device-mode smoke is the suite.

### 8.1 Widths

Chrome (or Safari) device mode, no zoom, after a hard reload:

| Width | Hop 1 (guest, no address) | Hop 1 (cookie banner + `requires_address`) | Success (each of VERIFYING / SUCCESS / TIMEOUT / EXPIRED / ERROR) |
|------:|---------------------------|--------------------------------------------|-------------------------------------------------------------------|
| 320 | no `scrollWidth > clientWidth` | address stacked; banner stacked; no overflow | card visible without a blank second screen below the header |
| 360 | same | same | same |
| 390 | same | same | same |
| 430 | same | same | same |
| 768 | still **one** column | same | same |
| 1024 | form left, 380 px summary right, `p-8` form pad | same | card centered in remaining column, footer just below |

How to check overflow: DevTools console `document.documentElement.scrollWidth > document.documentElement.clientWidth` must be `false`.

### 8.2 iOS focus-zoom

On a real iPhone (or Safari + iOS user agent is **not** enough — use a device or BrowserStack):

1. Tap name, email, promo, PWYW (if product is PWYW), city (if address).
2. Page must **not** snap-zoom. `window.visualViewport.scale` stays 1.

### 8.3 Regression (must still be true)

1. Guest, no cookie: no identity banner; name + email + full-width CTA.
2. `?cancelled=true`: amber banner wraps; does not overflow.
3. Submit still `POST /public/commerce/checkout` then `window.location.assign(result.url)`.
4. Success still unlocks only on `COMPLETED` (LP-024). Invalid / expired / timeout still do not say paid.
5. `lg` two-column order: form left, summary right (DOM is form then summary; `flex-col-reverse` only below `lg`).

### 8.4 Autofill (if 6.2 is done)

iOS Keychain / Chrome autofill offers name, email, phone, street, city, postcode. Country may still be weak because the value is ISO-2 — acceptable.

---

## 9. Risks

| Risk | Severity | Mitigation |
|------|----------|------------|
| `text-base` on `h-12` looks slightly large on desktop | Low | Accept. 16 px is the mobile constraint. Do not split sizes with a JS hook. |
| `flex-1` on success does not fill if an ancestor is not a flex column | Med | Root `body` is already `flex flex-col`; children wrapper is `flex-1 flex flex-col`. Checkout layout **must** be `flex-1 flex flex-col` (6.1). Smoke 320 success. |
| Removing `min-h-screen` from checkout layout makes a short page look “unfinished” (footer rides up) | Low | That is correct: footer should follow content, not sit under a phantom viewport. |
| Autocomplete `country` vs ISO-2 `MY` | Low | Do not swap in a select. LP-020 can fix the field. |
| Touching root footer spacing affects portal + legal | Low | `py-4 sm:py-6` is safe. Do not remove links. |
| Implementer “also adds” QR / sticky / logo | High (scope) | Refuse. Those are other IDs. |
| `user-scalable=no` as a zoom “fix” | High (a11y) | Forbidden. 16 px inputs only. |
| Dark iOS + `dark:` tokens | None for this ticket | Leave. |

---

## 10. Acceptance criteria

Close the **layout** slice of LP-021 when all of the following are true:

1. At 320, 360, 390, and 430 CSS px, hop 1 and success have **no horizontal page scroll**, including with `IdentityBanner` visible and with `requires_address`.
2. Every hop-1 `<input>` (including promo and PWYW) computes to **≥ 16 px** font-size. iOS focus does not zoom the page.
3. Address fields are **one column below `sm`**, two columns at `sm+`. City / postal / state / country have real labels.
4. Identity banner **stacks** below `sm`; the name wraps; both the label and the guest toggle remain fully on-screen and ≥ 44 px tall.
5. Promo Apply/Remove and PWYW are ≥ 44 px tall. Pay CTA stays `w-full` and ≥ 56 px.
6. Success / verifying / timeout / expired / invalid do **not** force `header + 100vh + footer`. The card sits in the remaining column; one short flick to the footer is OK, a blank extra screen is not.
7. At `lg` (≥ 1024) the page is still form left / summary 380 px right. No new components, no QR, no logo, no copy rewrite, no poller change.
8. §8.1–8.3 smoke is done (note the widths in the done file).
9. Tracker: treat layout as shipped (`Y` for the implement-ids meaning). **Do not** flip a mental “wallet QR on our page” bit — that work is not this ticket.

---

## 11. Suggested implement order

1. Chrome: checkout layout `flex-1` + success / Suspense drop `min-h-screen` (G4).  
2. Inputs `text-base` (G2).  
3. Address `grid-cols-1 sm:grid-cols-2` + labels (G3).  
4. Identity banner stack + `min-w-0` (G1).  
5. Summary / promo / PWYW overflow + 44 px (G5, G6).  
6. xs padding tighten (G8).  
7. Autocomplete / `inputMode` (G7) if still in the same PR.  
8. Lint + §8 smoke.

That is the whole ticket.
