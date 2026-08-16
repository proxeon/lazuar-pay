# W1-LP-037 — done

Apple Pay / Google Pay are a **Stripe wrap**. Hosted Checkout Session create now sends `payment_method_types: ['card']`. Wallets tokenize as cards; Stripe shows Apple Pay / Google Pay on hop 2 when the account can take cards and the device qualifies. Lazuar does not host wallet buttons, register merchant domains, or send `apple_pay` / `google_pay` (invalid on Checkout / PaymentIntent types).

The child PaymentIntent inherits session types. Off-session `ChargeOffSessionAsync` is unchanged (already bound to a vaulted PM). Billplz / CHIP / Razorpay untouched. No hop-1 chrome.

Tracker `LP-037` Lazuar = **`W`**. Not `Y`.

## Files changed

### Payments

- `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/StripeGatewayAdapter.cs` — `CardPaymentMethodType` + `ApplyCardWalletPaymentMethodTypes` sets `["card"]`. Called from `GenerateCheckoutAsync` before `CreateAsync`. Does not write `PaymentIntentData` types.

### Ops / admin copy (one paragraph, no toggle)

- `apps/lazuar-ops/src/modules/workspace/pages/PaymentSettingsPage.tsx` — Stripe-hosted wallets appear when cards are on in Dashboard; not on Billplz; no Lazuar domain verify.
- `apps/lazuar-admin/src/modules/platform/pages/PlatformPaymentSettingsPage.tsx` — same twin.

### Tracker

- `plans/007-feats/00-checklist-tracker.md` — `LP-037` Lazuar `N` → `W`.

### Tests

- `apps/lazuar-api/tests/Lazuar.ModuleTests/Payments/StripeGatewayAdapterTests.cs` — list is exactly `{ "card" }` (no `apple_pay` / `google_pay` / `fpx`); `PaymentIntentData` stays null or existing metadata; composes with `ApplySetupFutureUsage`.
- Same file (follow-up) — `CreateCheckoutSessionOptions` (the Checkout Session create payload) includes `card` and never `apple_pay` / `google_pay`. Billplz / CHIP / Razorpay adapter sources stay free of `apple_pay`, `google_pay`, `PaymentMethodTypes`, and `payment_method_types`.

## Tests run

- `Lazuar.ModuleTests` filter `StripeGatewayAdapterTests` — **15 passed**, 0 failed, 0 skipped. Duration 418 ms.
- Follow-up: `Lazuar.ModuleTests` filter `StripeGatewayAdapterTests` — **19 passed**, 0 failed, 0 skipped. Duration 468 ms.
- Follow-up: `Lazuar.ModuleTests` filter `StripeGatewayAdapterTests|BillplzGatewayAdapterTests|ChipCollectGatewayAdapterTests|RazorpayGatewayAdapterTests` — **41 passed**, 0 failed, 0 skipped. Duration 2 s.

Manual Safari / Wallet check **not run** here.

Not committed. Not pushed.

Hop-1 logos / domain verify remain reserved `LP-UX-010`. Stripe FPX / GrabPay / Link are not on this allow-list. `04-stripe.md` / `13-payments-refunds-rails.md` still describe Dashboard-dynamic PMs; they should say the adapter now sends `card` on a later doc pass.
