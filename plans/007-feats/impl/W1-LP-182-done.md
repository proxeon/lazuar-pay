# W1-LP-182 — done

Billplz host follows **workspace payment-config `environment`** (`test`|`live`), not Hub hostname. New rows default `test`; existing rows backfilled `live`. `sk_test_` vs live config (and inverse) → `409 KEY_MODE_MISMATCH`. Hosted checkout (null K1) uses the config flag only. `App:BillplzEnvironment` remains an ops override. Stripe K1 vs K2 prefix guard unchanged. VitePress environments page rewritten.

## Files

- `TenantPaymentConfiguration.Environment` + migration `20260818120000_AddPaymentConfigEnvironment`
- `CheckoutSessionCashier.EnsureKeyModeMatchesConfigEnvironment`
- `BillplzPublicBase.IsProductionApi` + adapter metadata
- Ops payment settings toggle
- `PaymentGatewayEnvironmentTests`, `BillplzPublicBaseTests`

## Tests run

- `PaymentGatewayEnvironmentTests|BillplzPublicBaseTests|CreateIntegrationCheckoutTests` — **passed**

Not committed. Not pushed.

Tracker `LP-182` **P → Y**.
