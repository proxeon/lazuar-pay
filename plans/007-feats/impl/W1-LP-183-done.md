# W1-LP-183 — done

Ops dashboard shows a 5-step getting-started checklist (workspace, BYOK, Resend, first product, copy pay link) until complete or dismissed 30 days. Replaces the scattered rose/amber banners. Create-product already allowed without Resend (archived draft); initiate checkout still gated. Copy-link uses entitlements slug + first product.

## Files

- `apps/lazuar-ops/src/modules/commerce/pages/DashboardPage.tsx`

## Tests run

- Ops `tsc` — clean

Manual stopwatch not run here (needs merchant Billplz/Resend accounts).

Not committed. Not pushed.

Tracker `LP-183` **P → Y** assuming the merchant already has gateway + Resend accounts.
