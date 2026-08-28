# @repo/pay-types-ts

Generated TypeScript types from `packages/pay-spec` (focused Pay OpenAPI). Not Hub `@repo/api-types-ts`.

```
pnpm --filter @repo/pay-spec build
pnpm --filter @repo/pay-types-ts generate
```

`examples/pay-node` stays on `fetch`. Merchant/checkout may keep hand DTOs.
