# R24 — Payments OpenAPI security schemes

**Date:** 2026-08-09  
**Branch:** `chore/remaining-005`  
**Track:** TypeSpec · Wave B  
**Checklist:** `checklists/r24-tsp-payments-security-schemes.md`  
**Analysis:** `08-typespec-wave-b.md` §4  
**Scope this pass:** Mirror LHDN docs `@useAuth` pattern on Payments product OpenAPI so Scalar/OpenAPI emit `security` + `securitySchemes`. No runtime auth change.

---

## Summary

| Concern | State |
|---------|--------|
| Pre-fix `dist/payments/openapi.yaml` | No `security` / `securitySchemes` (prose-only in `@doc`) |
| `docs-payments.tsp` | `@useAuth(BearerAuth \| ApiKeyAuth<ApiKeyLocation.header, "Authorization">)` |
| `modules/payments/routes.tsp` | `@useAuth(BearerAuth)` on `IntegrationCheckoutOperations` (monolith + product ops) |
| Scopes in OpenAPI | Still prose (`payments.checkouts:write\|read`) — same as LHDN; no OAuth2 scopes object |
| Runtime middleware | Unchanged (Bearer `sk_test_\|sk_live_` + scope checks) |
| Clients / NSwag DTOs | Unaffected (security is OpenAPI metadata only) |

---

## Pattern (mirror LHDN)

| Surface | LHDN | Payments (this PR) |
|---------|------|--------------------|
| Product docs package | `docs-lhdn.tsp` dual Bearer + ApiKeyAuth | `docs-payments.tsp` dual Bearer + ApiKeyAuth |
| Module routes interface | `routes.tsp` `@useAuth(BearerAuth)` | `payments/routes.tsp` `@useAuth(BearerAuth)` |
| Why dual on docs | Scalar “Try it” accepts raw key in `Authorization` | Same M2M DX class |

---

## Spot-check (`pnpm --filter @repo/api-spec build`)

**`packages/api-spec/dist/payments/openapi.yaml`:**

| Check | Result |
|-------|--------|
| Top-level `security` | `BearerAuth: []` **and** `ApiKeyAuth: []` |
| Per-op `security` (POST/GET checkouts) | `BearerAuth: []` |
| `components.securitySchemes.BearerAuth` | `type: http`, `scheme: Bearer` |
| `components.securitySchemes.ApiKeyAuth` | `type: apiKey`, `in: header`, `name: Authorization` |

**Monolith `dist/openapi.yaml`:**

| Check | Result |
|-------|--------|
| `IntegrationCheckoutOperations_*` ops | `security: - BearerAuth: []` (was missing) |
| Monolith `securitySchemes` | Still Bearer only at package root (expected; dual only on product docs packages like LHDN) |

---

## Code change

1. **`packages/api-spec/docs-payments.tsp`**  
   After `@server` lines, added dual `@useAuth` + comments matching `docs-lhdn.tsp`.

2. **`packages/api-spec/modules/payments/routes.tsp`**  
   `@useAuth(BearerAuth)` on `IntegrationCheckoutOperations` so monolith OpenAPI marks M2M checkouts as authenticated.

3. **Gen**  
   `pnpm --filter @repo/api-spec build` — all product packages compile clean.

---

## Residual

| Item | Owner | Note |
|------|-------|------|
| OAuth2 scope object for `payments.checkouts:*` | Optional | Nice-to-have; not blocking (§4.3) |
| OrgAdmin cookie vs Bearer DX lie | Known | Console products; same as Commerce/Billing |
| Scalar Authorize UI on developers hub | Manual | After docs hub rebuild / deploy |
| Path honesty CI | R25 | Next TypeSpec track item |

---

## Files

| Action | Path |
|--------|------|
| Edited | `packages/api-spec/docs-payments.tsp` |
| Edited | `packages/api-spec/modules/payments/routes.tsp` |
| Regenerated (local) | `packages/api-spec/dist/payments/openapi.yaml` |
| Regenerated (local) | `packages/api-spec/dist/openapi.yaml` (checkout ops security) |
| Notes | `plans/005-remaining/r24-notes.md` |
| Checklist | `plans/005-remaining/checklists/r24-tsp-payments-security-schemes.md` |
| FULL-CHECKLIST | R24 section checked |
