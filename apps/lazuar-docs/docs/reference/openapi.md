# OpenAPI & Scalar

## Where contracts live

| Artifact | Path |
|----------|------|
| TypeSpec sources | `packages/api-spec/` |
| Payments product docs | `packages/api-spec/docs-payments.tsp` |
| Generated OpenAPI (after build) | `packages/api-spec/dist/payments/openapi.yaml` (and other products) |
| .NET contracts | `packages/api-types-dotnet/` |
| TS types | `packages/api-types-ts/` |

## Developers page (Scalar)

Run **developers-page** in the monorepo:

```bash
# typical local
pnpm --filter developers-page dev
```

Useful routes (when running):

| Path | Content |
|------|---------|
| `/payments` | Payments OpenAPI Scalar |
| `/payments-cashier` | Link/card into cashier narrative |
| `/auth` | API keys & scopes copy |
| `/webhooks` | Webhook UI notes |

Point production docs site nav at your deployed developers host when publishing.

## Versioning (v1)

- **Additive** fields / optional scopes → non-breaking  
- **Breaking** rename/remove fields, auth, signature, idempotency → new major or deprecation window  
- Policy notes: monorepo `docs/api-versioning.md`  

## This VitePress site vs OpenAPI

| VitePress (here) | OpenAPI |
|------------------|---------|
| Human guide, flows, do/don’t | Exact request/response schemas |
| Onboarding narrative | Codegen input |

Keep both in sync when APIs change.
