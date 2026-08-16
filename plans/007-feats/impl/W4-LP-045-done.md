# W4-LP-045 — done (wrap)

`XenditGatewayAdapter` is a BYOK hosted-invoice wrap: POST `/v2/invoices`, `x-callback-token` webhooks, invoice refunds. Off-session always returns false (reminder-only). Factory, webhook allow-list, M2M checkout, ops/admin dropdown include `XENDIT`.

Optional `xendit_payment_methods` metadata is filtered to documented hosted channels. Unknown codes are dropped. Empty list = merchant dashboard defaults.

## Tests

`XenditGatewayAdapterTests` + capabilities — passed.

Tracker `LP-045` **N → W**.
