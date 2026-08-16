# W1-LP-131 — done

Mint without `scopes` is **400**. No implicit LHDN default. `payments.config:read` removed from the closed catalog and policies (Option A). `/me` still works with any `payments.checkouts:*` scope. Ops catalog + VitePress + Developers auth say “scopes required.”

## Files

- `PlatformApiScopes.NormalizeAndValidate`
- `AuthAndCorsExtensions` (dropped `IntegrationPaymentsConfigRead`)
- `ApiKeysPage`, `integrations/api-keys.md`, Developers `/auth`
- `GenerateAndListApiCredentialsTests`, `ApiKeyAuthenticationTests`, `GetPaymentsMeTests`

## Tests run

- `GenerateAndListApiCredentialsTests|ApiKeyAuthenticationTests|GetPaymentsMeTests` — **passed**

Not committed. Not pushed.

Tracker `LP-131` **P → Y**. Commerce scopes added on LP-137 in the same slice.
