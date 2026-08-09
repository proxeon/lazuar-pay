# Phase 09 — Split `ProvisionAuraWorkspaceCommand`

**Goal:** Readable provision flow; same MediatR contract if possible.  
**File:** `Modules/One/Application/Commands/ProvisionAuraWorkspaceCommand.cs` (~646 LOC)  
**Evidence:** `../02-large-files-chunking.md` §3.2

---

## 09.1 Behavioral inventory

- [x] List steps: validate → create/ensure tenant → owner → credentials → webhook → result DTO  
  → see `../phase-09-analysis.md` §1.1
- [x] List static helpers: `Normalize*`, `DefaultKeyNameFor`, etc. and their callers (endpoints, tests)  
  → analysis §1.2; kept on handler type
- [x] Identify idempotent ensure vs create-new branches  
  → analysis §1.3

## 09.2 Split design (idiomatic)

Prefer private collaborators or partial classes in same folder, e.g.:

- [x] Keep command + result types stable (public names)  
  → `ProvisionAuraWorkspaceCommand.cs` + `ProvisionAuraWorkspaceResult.cs`
- [x] Extract `ProvisionAuraWorkspaceHandler` steps into:
  - [x] Tenant ensure/create → `…Handler.Tenant.cs`
  - [x] Owner membership → `…Handler.Owner.cs`
  - [x] API key mint → `…Handler.Keys.cs`
  - [x] Webhook registration → `…Handler.Webhook.cs`
  - [x] Response mapping → `…Handler.Mapping.cs`
- [x] Move pure normalizers to `ProvisionAuraWorkspaceNormalizers.cs` (or keep public static façade if endpoints depend)  
  → partial `…Handler.Normalizers.cs` (stable public static surface; no call-site churn)

## 09.3 Rules

- [x] MediatR request/response types **not renamed** without test updates
- [x] Idempotency behavior unchanged
- [x] No new external dependencies

## 09.4 Tests

- [x] `ProvisionAuraWorkspaceTests` green → **33/33**
- [x] Related API key / webhook tests green → covered in same suite (bootstrap mint, secret-once, ensure)
- [x] Endpoint provision still works → no endpoint renames; MediatR types stable

## 09.5 Exit criteria

- [x] No single file owns all provision steps at 600+ LOC → largest ~213 LOC orchestration
- [x] Behavior parity with pre-split → full provision suite green
