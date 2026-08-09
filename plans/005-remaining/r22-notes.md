# R22 — Broadcast preview/status contract honesty

**Date:** 2026-08-09  
**Branch:** `chore/remaining-005`  
**Track:** TypeSpec · Wave B  
**Checklist:** `checklists/r22-tsp-broadcast-preview-status.md`  
**Analysis:** `08-typespec-wave-b.md` §3.2  
**Scope this pass:** Add impl-only broadcast GET preview/status into TypeSpec; `task gen`; bind endpoints to `Lazuar.ApiTypes`; delete Contracts edge DTOs.

---

## Decision

| Option | Chosen | Why |
|--------|--------|-----|
| **A** — Add to TypeSpec + generated types | **Yes** | Real OrgAdmin product routes under `/admin/communications` (same surface as templates/email-config). Already registered in Minimal API; ops/manual consumers and future UI need honest OpenAPI. |
| **B** — Internalize/allowlist | No | Not clearly internal; not HTML/Svix machine-only. |

Matches analysis recommendation **B-BC-Add**.

---

## Summary

| Concern | State |
|---------|--------|
| TSP models | `BroadcastStatusDto`, `BroadcastCostPreviewDto` in `models.tsp` |
| TSP routes | `GET /broadcasts/preview`, `GET /broadcasts/{id}` on `AdminCommunicationsOperations` |
| Credits fields | Documented as reserved; v1 always 0 / sufficient |
| `task gen` | Clean (spec + TS + C# + LHDN SDKs) |
| Endpoint bind | `Lazuar.ApiTypes` NSwag props (`Total_recipients`, …) |
| Local Contracts DTOs | **Deleted** `BroadcastDtos.cs` |
| Unused `ICreditCostService` on preview | Dropped (costs hard-coded free) |

---

## Wire shapes (unchanged JSON)

`BroadcastStatusDto`: `id`, `status`, `total_recipients`, `sent_count`, `suppressed_count`, `failed_count`, `credits_reserved`, `credits_used`, `created_at`, `completed_at?`, `failure_reason?`

`BroadcastCostPreviewDto`: `recipient_count`, `credits_per_recipient`, `total_credits`, `sufficient_credits`, `available_credits`

---

## Property mapping (local → generated)

| Local (Contracts) | Generated (`Lazuar.ApiTypes`) |
|-------------------|-------------------------------|
| `TotalRecipients` | `Total_recipients` |
| `SentCount` | `Sent_count` |
| `SuppressedCount` | `Suppressed_count` |
| `FailedCount` | `Failed_count` |
| `CreditsReserved` | `Credits_reserved` |
| `CreditsUsed` | `Credits_used` |
| `CreatedAt` | `Created_at` |
| `CompletedAt` | `Completed_at` |
| `FailureReason` | `Failure_reason` |
| `RecipientCount` | `Recipient_count` |
| `CreditsPerRecipient` | `Credits_per_recipient` |
| `TotalCredits` | `Total_credits` |
| `SufficientCredits` | `Sufficient_credits` |
| `AvailableCredits` | `Available_credits` |

JSON wire names unchanged (snake_case via NSwag `JsonPropertyName`).

---

## Verification

| Check | Result |
|-------|--------|
| `task gen --force` | Succeeded |
| OpenAPI paths | `/admin/communications/broadcasts/preview`, `/admin/communications/broadcasts/{id}` present in TS client + commerce/main OpenAPI |
| `dotnet build` Lazuar.Api | **0 warnings / 0 errors** |
| `dotnet test` filter `FullyQualifiedName~Broadcast` | **Passed 9 / 9** |
| `rg BroadcastDtos` / Contracts dual | File deleted; endpoints only use `Lazuar.ApiTypes` |

```
Passed!  - Failed: 0, Passed: 9, Skipped: 0, Total: 9
```

---

## Residual

| Item | Owner | Note |
|------|-------|------|
| Billing final signed PDF | R23 | Separate honesty decision |
| Payments security schemes | R24 | `@useAuth` |
| Path honesty CI gate | R25 | After more Wave B adds, allowlist smaller |
| Broadcast targeting filters | Product | Still not productized (Wave A honesty) |
| Credit charging for broadcasts | Product | Fields reserved at 0 |

---

## Files

| Action | Path |
|--------|------|
| Edited | `packages/api-spec/modules/communications/models.tsp` |
| Edited | `packages/api-spec/modules/communications/admin-routes.tsp` |
| Regenerated | `packages/api-types-dotnet/Lazuar.ApiContracts.cs` |
| Regenerated | `packages/api-types-ts/src/index.ts` |
| Regenerated | LHDN SDK trees (gen side-effect; LHDN TSP unchanged) |
| Edited | `apps/lazuar-api/Modules/Communications/Infrastructure/Endpoints/BroadcastEndpoints.cs` |
| Deleted | `apps/lazuar-api/Modules/Communications/Contracts/BroadcastDtos.cs` |
| Edited | `docs/contracts/openapi-vs-minimal-api.md` |
| Notes | `plans/005-remaining/r22-notes.md` |
| Checklist | `plans/005-remaining/checklists/r22-tsp-broadcast-preview-status.md` |
| FULL-CHECKLIST | R22 section checked |

**No commit** (per task instruction).
