# Phase 09 — Split `ProvisionAuraWorkspaceCommand`

**Goal:** Readable provision flow; same MediatR contract if possible.  
**File:** `Modules/One/Application/Commands/ProvisionAuraWorkspaceCommand.cs` (~646 LOC)  
**Evidence:** `../02-large-files-chunking.md` §3.2

---

## 09.1 Behavioral inventory

- [ ] List steps: validate → create/ensure tenant → owner → credentials → webhook → result DTO
- [ ] List static helpers: `Normalize*`, `DefaultKeyNameFor`, etc. and their callers (endpoints, tests)
- [ ] Identify idempotent ensure vs create-new branches

## 09.2 Split design (idiomatic)

Prefer private collaborators or partial classes in same folder, e.g.:

- [ ] Keep command + result types stable (public names)
- [ ] Extract `ProvisionAuraWorkspaceHandler` steps into:
  - [ ] Tenant ensure/create
  - [ ] Owner membership
  - [ ] API key mint
  - [ ] Webhook registration
  - [ ] Response mapping
- [ ] Move pure normalizers to `ProvisionAuraWorkspaceNormalizers.cs` (or keep public static façade if endpoints depend)

## 09.3 Rules

- [ ] MediatR request/response types **not renamed** without test updates
- [ ] Idempotency behavior unchanged
- [ ] No new external dependencies

## 09.4 Tests

- [ ] `ProvisionAuraWorkspaceTests` green
- [ ] Related API key / webhook tests green
- [ ] Endpoint provision still works

## 09.5 Exit criteria

- [ ] No single file owns all provision steps at 600+ LOC
- [ ] Behavior parity with pre-split
