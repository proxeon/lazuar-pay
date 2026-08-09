# Phase 09 — Done

**Date:** 2026-08-09  
**Branch:** `chore/backend-maintenance-004`  
**Commit subject:** `refactor(one): decompose ProvisionAuraWorkspace command (phase 09)`

## What landed

### Contracts (stable names)

| File | Responsibility |
|------|----------------|
| `ProvisionAuraWorkspaceCommand.cs` | MediatR command record only |
| `ProvisionAuraWorkspaceResult.cs` | Result DTO record |

### Handler partials (`Modules/One/Application/Commands/`)

| File | Responsibility |
|------|----------------|
| `ProvisionAuraWorkspaceCommandHandler.cs` | Fields, constants, ctor, `Handle` + `EnsureAndBuildExistingAsync` orchestration |
| `…Handler.Tenant.cs` | Org create/bind, PAYMENTS entitlement, slug resolve, unique-violation detect |
| `…Handler.Owner.cs` | Create-path attach + ensure-path heal |
| `…Handler.Keys.cs` | Bootstrap mint + idempotent bootstrap select |
| `…Handler.Webhook.cs` | Create/ensure webhook; secret mint/hint; never remint on match |
| `…Handler.Mapping.cs` | `BuildResult` |
| `…Handler.Normalizers.cs` | Public static `Normalize*` / `DefaultKeyNameFor` (stable call sites) |

### Size

- Pre-split monolith: **647 LOC**
- Post-split largest file: handler orchestration **~213 LOC**
- No single file owns all provision steps at 600+ LOC

### Plans

- `phase-09-analysis.md` — inventory, layout, rules  
- `checklists/phase-09-provision-command-split.md` — criteria marked done  

## Verification

| Check | Result |
|-------|--------|
| `ProvisionAuraWorkspaceTests` | **33/33 passed** |
| Endpoint / test renames | None required (handler type + public statics stable) |

## Exit criteria

| Criterion | Status |
|-----------|--------|
| Readable provision flow | Yes — steps in named partials |
| MediatR contract stable | Yes |
| Idempotency / secret-once behavior | Unchanged; full provision suite green |
| No 600+ LOC god handler file | Yes |

## Explicitly not done

- Split of `ProvisionAuraWorkspaceTests.cs`  
- Separate DI collaborator types (partials preferred)  
- Behavior changes to provision API  

## Next

Phase 10 dunning engine split (or next checklist item on the maintenance track).
