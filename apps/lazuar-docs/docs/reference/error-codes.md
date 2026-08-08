# Error codes (Payments M2M)

Stable codes appear on ProblemDetails (title and/or `extensions.code`). Prefer codes over free-text matching.

| Code | Typical HTTP | Meaning |
|------|--------------|---------|
| `PAYMENTS_NOT_CONFIGURED` | 422 | No active BYOK gateway on workspace |
| `AMOUNT_INVALID` | 400 | Amount missing/invalid |
| `AMOUNT_BELOW_MINIMUM` | 400 | Below gateway minimum |
| `CURRENCY_INVALID` | 400 | Unsupported currency |
| `URLS_REQUIRED` | 400 | success/cancel URL missing |
| `METADATA_INVALID` | 400 | Metadata too large / invalid keys |
| `IDEMPOTENCY_CONFLICT` | 409 | Idempotency key reuse conflict |
| `GATEWAY_ERROR` | 502 | Upstream processor failure |
| `CHECKOUT_NOT_FOUND` | 404 | Unknown checkout id (or wrong workspace) |
| `UNAUTHORIZED` | 401 | Missing/invalid API key |
| `FORBIDDEN` | 403 | Scope or tenant mismatch |

## Provision errors

| Situation | Typical HTTP |
|-----------|--------------|
| Bad provision secret | 401 |
| Rate limited | 429 |
| Invalid webhook URL | 400 |
| Conflict / validation | 400 / 409 |

Map these to your app’s user-safe messages; never expose Hub internal stack traces.
