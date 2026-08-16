# W1-LP-137 — done

Key-auth Hub Commerce subscription admin:

`GET/POST /api/v1/integrations/commerce/subscriptions` list/get/cancel (immediate). Scopes `commerce.subscriptions:read|write` (write implies read). Payments-only keys 403. Already canceled → 400. Reuses `CancelAdminSubscriptionCommand` and subscriber DTOs.

## Files

- `PlatformApiScopes` + `AuthAndCorsExtensions` policies
- `IntegrationSubscriptionEndpoints` + TypeSpec `integration-routes.tsp`
- Ops API key catalog group
- Policy tests in `ApiKeyAuthenticationTests`

## Tests run

- `ApiKeyAuthenticationTests` commerce policy case — **passed**

Not committed. Not pushed.

Tracker `LP-137` **N → Y** (narrow: list/get/cancel only).
