# R25 — OpenAPI ↔ Minimal API path honesty CI

**Date:** 2026-08-09  
**Branch:** `chore/remaining-005`  
**Track:** TypeSpec · Wave B  
**Checklist:** `checklists/r25-tsp-path-honesty-ci.md`  
**Analysis:** `08-typespec-wave-b.md` §6  
**Scope this pass:** Static path honesty gate (OpenAPI ⊆ Minimal; Minimal ⊆ OpenAPI ∪ allowlist). No runtime reflection host.

---

## Summary

| Concern | State |
|---------|--------|
| Allowlist | `packages/api-spec/honesty-allowlist.yaml` (R23 seed expanded) |
| Script | `scripts/check-openapi-minimal-honesty.mjs` (Node, no extra deps) |
| Task | `task contracts:honesty` (deps: `gen:spec`) |
| CI | `contracts` job step after client dirty check |
| Scrape strategy | Static: MapGroup + MapGet/Post/Put/Delete/Patch + extension-method call chain from `MapAllModuleEndpoints` |
| Green snapshot | 125 OpenAPI ops, 132 Minimal ops, 7 `impl_only` |

---

## Assertions

1. **OpenAPI ⊆ Minimal ∪ `openapi_only_exceptions`** — no phantom TypeSpec paths.  
2. **Minimal ⊆ OpenAPI ∪ `impl_only`** — no silent product-ish Map* without contract or allowlist reason.

Paths normalized relative to `/api/v1`; `{param:guid}` constraints stripped for compare.

---

## `impl_only` rows (this ship)

| Method | Path | Why |
|--------|------|-----|
| GET | `/public/billing/{tenantSlug}/documents/{ledgerEntryId}` | R23 HMAC email PDF redirect |
| POST | `/webhooks/payments/{gatewayType}/{tenantId}` | Gateway inbound |
| POST | `/messaging/notify` | Internal fan-in |
| GET | `/messaging/delivery-logs` | Messaging diagnostics |
| GET | `/public/communications/unsubscribe` | HTML compliance |
| POST | `/public/communications/webhooks/resend` | Resend/Svix |
| DELETE | `/admin/communications/templates/legacy-cleanup` | Temp ops utility |

Out of scope: host `/health*`, Scalar static, migrator CLIs (no HTTP maps).

Platform auth + payment-config **are** in OpenAPI (`/platform/*`) — not allowlisted.

---

## How to update allowlist

```bash
task gen:spec
node scripts/check-openapi-minimal-honesty.mjs --verbose
# Prefer TypeSpec + task gen for product routes.
# Else add impl_only { method, path, reason } and document in docs/contracts/openapi-vs-minimal-api.md
```

---

## Residual / non-goals

| Item | Note |
|------|------|
| Body/schema dual-DTO parity | Not this gate (Wave B dual-DTO PRs) |
| Auth policy mismatch | Not this gate |
| Runtime `EndpointDataSource` dump | Optional later if static scrape false-negatives appear |
| Stale allowlist rows | Soft warning only (does not fail CI) |

---

## Files

| Action | Path |
|--------|------|
| Expanded | `packages/api-spec/honesty-allowlist.yaml` |
| Created | `scripts/check-openapi-minimal-honesty.mjs` |
| Edited | `.github/workflows/ci.yml` (`contracts` job) |
| Edited | `Taskfile.yml` (`contracts:honesty`) |
| Edited | `docs/contracts/openapi-vs-minimal-api.md` |
| Edited | `plans/004-maintenance/FUTURE-WORK.md` (FW-6 CI item) |
| Notes | `plans/005-remaining/r25-notes.md` |
| Checklist | `plans/005-remaining/checklists/r25-tsp-path-honesty-ci.md` |
| FULL-CHECKLIST | R25 section checked |
