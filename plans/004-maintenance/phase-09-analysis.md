# Phase 09 — Analysis (`ProvisionAuraWorkspaceCommand` split)

**Date:** 2026-08-09  
**Branch:** `chore/backend-maintenance-004`  
**Goal:** Readable provision flow; same MediatR contract. Mechanical-ish extract with named steps.  
**Evidence:** `checklists/phase-09-provision-command-split.md`, `02-large-files-chunking.md` §3.2

---

## 1. Behavioral inventory (pre-split)

**Path:** `Modules/One/Application/Commands/ProvisionAuraWorkspaceCommand.cs`  
**Size before:** **647 LOC** (command + result + handler monolith)

### 1.1 Steps

| Step | Create path | Idempotent ensure path |
|------|-------------|------------------------|
| Validate / normalize | product, org id, display name, owner role, webhook URL/events | Same inputs reused |
| Tenant | Create org + bind external ref + slug resolve | Load by external ref |
| Owner | `TryAttachOwnerAsync` (new membership, no save yet) | `EnsureOwnerAsync` (heal + immediate save if new) |
| Entitlement | PAYMENTS grant + integration event | No re-grant |
| API keys | Mint bootstrap credential (plain once) | Select existing bootstrap; no remint; `plainKey=null` |
| Webhook | Create if URL given (secret once) | Exact URL match → metadata only; missing → create once |
| Persist | Single `SaveChanges`; unique-violation → ensure race recovery | Webhook heal save only when created |
| Result | `BuildResult(created: true, plainKey set)` | `BuildResult(created: false, plainKey null)` |

### 1.2 Public static helpers + callers

| Member | Callers |
|--------|---------|
| `ProductAura`, constants | Endpoints (`IntegrationProvisionEndpoints`), tests |
| `DefaultKeyName`, owner status constants | Tests |
| `NormalizeAuraOrgId` / `NormalizeExternalProduct` / `NormalizeExternalOrgId` | Tests |
| `DefaultKeyNameFor`, `NormalizeOwnerRole`, webhook normalizers | Handler only (tests cover via command) |
| Command / Result type names | MediatR + endpoints + tests |

**Decision:** Keep all public statics and constants on `ProvisionAuraWorkspaceCommandHandler` (partial `Normalizers`) so call sites need **zero** renames.

### 1.3 Idempotent ensure vs create-new

- **Create-new:** no existing external ref → create org/entitlement/key/(optional webhook) → save; on unique race re-enter ensure.
- **Ensure:** existing org → owner heal + webhook ensure (never remint secret) → return bootstrap key metadata without plain secret.

---

## 2. Target layout (implemented)

Prefer Commerce-style **partials** (low ceremony; same type surface) over free-floating collaborator DI types.

```
Modules/One/Application/Commands/
  ProvisionAuraWorkspaceCommand.cs                    # command record only (~22 LOC)
  ProvisionAuraWorkspaceResult.cs                     # result record (~31 LOC)
  ProvisionAuraWorkspaceCommandHandler.cs             # ctor, constants, Handle + Ensure orchestration (~213 LOC)
  ProvisionAuraWorkspaceCommandHandler.Tenant.cs      # org bind, entitlement, slug, unique-violation
  ProvisionAuraWorkspaceCommandHandler.Owner.cs       # TryAttach / EnsureOwner
  ProvisionAuraWorkspaceCommandHandler.Keys.cs        # mint bootstrap + select bootstrap
  ProvisionAuraWorkspaceCommandHandler.Webhook.cs     # create / ensure webhook + secret helpers
  ProvisionAuraWorkspaceCommandHandler.Mapping.cs     # BuildResult
  ProvisionAuraWorkspaceCommandHandler.Normalizers.cs # pure Normalize* + DefaultKeyNameFor
```

Largest single file after split: handler orchestration **~213 LOC** (was 647 monolith).

---

## 3. Move rules applied

- [x] MediatR request/response type names unchanged (`ProvisionAuraWorkspaceCommand`, `ProvisionAuraWorkspaceResult`)
- [x] Handler type name unchanged (`ProvisionAuraWorkspaceCommandHandler`)
- [x] Public constants / static normalizers remain on handler type
- [x] Idempotency: unique-violation catch + `EnsureAndBuildExistingAsync` preserved
- [x] Secret once-only semantics preserved (webhook + API key remint rules)
- [x] No new external dependencies / no DI registration changes

---

## 4. Extracted step helpers (orchestration calls)

| Concern | Helpers |
|---------|---------|
| Tenant | `CreateBoundOrganization`, `GrantPaymentsEntitlementAsync`, `ResolveSlugAsync`, `IsUniqueViolation` |
| Owner | `TryAttachOwnerAsync`, `EnsureOwnerAsync` |
| Keys | `MintBootstrapCredential`, `SelectBootstrapCredential` |
| Webhook | `TryCreateWebhookEndpoint`, `EnsureWebhookAsync`, `MintWebhookSecret`, `SecretHint` |
| Mapping | `BuildResult` |
| Normalizers | all prior public statics |

---

## 5. Verification

| Check | Result |
|-------|--------|
| `dotnet test` filter `ProvisionAuraWorkspaceTests` | **33 passed**, 0 failed |
| Public surface for endpoints/tests | Unchanged (no test edits required) |

---

## 6. Explicit non-goals

- Behavior / auth / scope changes  
- New domain service types with DI  
- Splitting `ProvisionAuraWorkspaceTests` (758 LOC) — deferred  
- Moving normalizers off handler type into a separate public type  
