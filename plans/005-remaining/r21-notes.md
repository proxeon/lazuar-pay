# R21 — TypeSpec dual DTO: record refund

**Date:** 2026-08-09  
**Branch:** `chore/remaining-005`  
**Track:** TypeSpec · Wave B  
**Checklist:** `checklists/r21-tsp-dual-dto-refund.md`  
**Analysis:** `08-typespec-wave-b.md` §2.2 B  
**Scope this pass:** Bind transaction refund admin endpoint to generated `Lazuar.ApiTypes` DTO; remove local dual record. No TypeSpec change.

---

## Summary

| Concern | State |
|---------|--------|
| Generated `RecordRefundRequestDto` | Present in `packages/api-types-dotnet/Lazuar.ApiContracts.cs` |
| Field parity local ↔ generated | Match (amount/gateway_name/subscription_id/tax_amount); tax nullability differs |
| TypeSpec edit / `task gen` | **Not required** |
| Endpoint bind | `RecordRefundRequestDto?` (body optional) |
| ACL | `double?` → `decimal?` amount; tax defaults to `0m` when null |
| Local record deleted | `RecordRefundRequest` |
| GET transactions already on `TransactionLogDto` | Unchanged |

---

## Diff (local → generated)

| Local | Generated | Notes |
|-------|-----------|--------|
| `RecordRefundRequest` record | `RecordRefundRequestDto` class | Wire JSON unchanged (snake_case props) |
| `decimal? Amount` | `double? Amount` | Cast at command ACL |
| `string? Gateway_name` | `string? Gateway_name` | OK |
| `string? Subscription_id` | `string? Subscription_id` | Parse Guid in endpoint (unchanged) |
| `decimal Tax_amount = 0m` | `double? Tax_amount` | Null → `0m` (preserve prior omit behavior) |

TSP model already in `packages/api-spec/modules/commerce/models/subscriber.tsp` with optional `float64` fields — no gap.

---

## Code change

**File:** `apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints/TransactionEndpoints.cs`

1. Removed local `RecordRefundRequest` record.
2. POST `/transactions/{id}/refund` binds `RecordRefundRequestDto?`.
3. Maps to `RecordRefundCommand` with:
   - `req?.Amount is double a ? (decimal)a : null`
   - `req?.Tax_amount is double t ? (decimal)t : 0m`
4. `Lazuar.ApiTypes` already imported (`TransactionLogDto`, `StatusResponse`, `PaginatedResponse<>`).

---

## Verification

| Check | Result |
|-------|--------|
| `rg 'RecordRefundRequest' apps/lazuar-api` (local dual) | Only `RecordRefundRequestDto` bind in `TransactionEndpoints.cs`; command names remain |
| `dotnet build` Commerce Infrastructure | Succeeded 0 warnings / 0 errors |
| `dotnet build` `Lazuar.Api` host | Succeeded 0 errors (1 transient file-lock warning unrelated) |
| `dotnet test` filter `RecordRefund|GatewayRefundCompleted|PaymentThenFullRefund|Matrix_PaymentRefund` | **Passed 8 / 8** |

```
Passed!  - Failed: 0, Passed: 8, Skipped: 0, Total: 8
```

Includes `RecordRefund_ForeignOrg_ThrowsNotFound` (tenant isolation), `GatewayRefundCompletedHandlerTests`, and ledger refund matrix tests.

---

## Residual

| Item | Owner | Note |
|------|-------|------|
| Broadcast preview/status | R22 | Impl-only TSP gaps, not duals |
| Re-intro dual DTOs | Review | Prefer bind `Lazuar.ApiTypes` or add TSP first |

Commerce product + refund dual DTOs are now both gone (R20 + R21).

---

## Files

| Action | Path |
|--------|------|
| Edited | `apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints/TransactionEndpoints.cs` |
| Notes | `plans/005-remaining/r21-notes.md` |
| Checklist | `plans/005-remaining/checklists/r21-tsp-dual-dto-refund.md` |
| FULL-CHECKLIST | R21 section checked |
