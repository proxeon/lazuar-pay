# W4-LP-044 — done (honesty pipe)

Razorpay/Curlec wrap: dummy `billing@lazuar.com` removed from off-session create (buyer email/phone only if present in notes). `payment.failed` / `invoice.expired` map to `PAYMENT_FAILED`. Missing currency fails closed (no invented MYR). Ops label is “Razorpay / Curlec”. `SupportsOffSession` stays **false** until a sandbox token soak.

## Files

- `RazorpayGatewayAdapter.cs`
- `RazorpayGatewayAdapterTests` — 5 passed
- Ops/admin payment settings labels

Tracker `LP-044` **P → W**. E-mandate registration is still card-only; LP-032 stays N until mandate tokens work.
