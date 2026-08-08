# API versioning policy (Hub public integrator surfaces)

**Status:** Phase 19 (plan 663 Track B)  
**Applies to:** TypeSpec / OpenAPI under `packages/api-spec` for external integrators (One provision, Payments M2M, Commerce public, LHDN, webhooks).

## Current version

- Path prefix: `/api/v1/…`
- Documented services are versioned as **1.0.0** in TypeSpec `@info`.

## What is non-breaking (allowed without major bump)

- Adding optional request fields (callers may omit).
- Adding response fields (clients must ignore unknown keys).
- Adding new scopes to the closed catalog (existing keys unchanged).
- Adding new webhook event types (endpoints with empty filter receive them; filtered endpoints need opt-in).
- Softening validation only when it expands acceptance, not when it rejects previously valid payloads.

## What is breaking (requires `/api/v2` or explicit deprecation)

- Removing or renaming fields.
- Changing field types or semantics (e.g. amount units).
- Changing auth scheme (Bearer `sk_` → something else) without dual support.
- Changing webhook signature algorithm or header names without dual-verify window.
- Changing idempotency conflict rules for the same key.
- Making previously optional required fields required.

## Deprecation process

1. Announce in Developers hub + release notes with sunset date (≥90 days for external partners when live).
2. Dual-support old and new during window when feasible.
3. Metrics / logs on deprecated path usage before hard cut.

## Source of truth

| Artifact | Role |
|----------|------|
| `packages/api-spec/**/*.tsp` | Authoring |
| `packages/api-spec/dist/**/openapi.yaml` | Published OpenAPI (Scalar) |
| Runtime Minimal API | Must match contracts; contract-first for new integrator routes |

Internal Ops console routes may change faster and are marked Internal on the Developers hub.
