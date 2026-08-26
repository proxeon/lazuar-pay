---
number: "273"
id: B07-I33
severity: P2
status: open
source: plans/009-bugs/07-one-identity-invites-keys.md
head: "297ba98"
---

# 273 — B07-I33 — Storage presign is any member; path is tenant-required

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/07-one-identity-invites-keys.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B07-I33 — P2 — Storage presign is any member; path is tenant-required

**Where.** `StorageEndpoints.cs:27–48`; `TenantSecurityMiddleware.cs:160–164`.

**What.** Empty tenant 400s (pre-wave hole closed). VIEWER can still upload. Not OrgAdmin.

## Evaluation (current tree, 2026-08-18)

### What the bug is
`POST /one/storage/presigned-url` is `RequireAuthorization()` only — any authenticated human whose tenant middleware accepted the header. The pre-wave empty-tenant hole is closed: `ctx.TenantId == Guid.Empty` returns 400, and `TenantSecurityMiddleware.RequiresTenantContext` is true for `/api/v1/one/storage`, so a missing header 400s before the endpoint. The remaining hole is authorization level. API keys use `OrgAdmin` (`ApiCredentialEndpoints.cs:21`). Storage does not. A VIEWER or MEMBER who can call the route gets a presigned PUT under `vault/{tenantId}/{uuid}{ext}` and can overwrite branding/legal images the ADMIN did not pick. Ops `GeneralSettingsPage` and `BillingProfilePage` both call this from the browser with the workspace cookie.

### Still present?
**STILL BROKEN** (empty-tenant half **ALREADY FIXED**)

```16:32:apps/lazuar-api/Modules/One/Infrastructure/Endpoints/StorageEndpoints.cs
        group.MapPost("/storage/presigned-url", Task<Results<Ok<GetPresignedUrlResponseDto>, BadRequest<string>>> (
            ...
        {
            ...
            var tenantId = ctx.TenantId;
            if (tenantId == Guid.Empty)
            {
                return Task.FromResult<Results<Ok<GetPresignedUrlResponseDto>, BadRequest<string>>>(
                    TypedResults.BadRequest("Tenant context is required to create a presigned storage URL."));
            }
```

The map ends with `.RequireAuthorization()` only (`StorageEndpoints.cs:48`), not `"OrgAdmin"`. Middleware still requires tenant (`TenantSecurityMiddleware.cs:169–174`). Architecture tests lock that (`TenantIsolationArchitectureTests.cs:90, 111`). `TenantIsolationHardeningTests.Presigned_Storage_Rejects_Empty_Tenant_Contract` (`:335–343`) only asserts `Guid.Empty == Guid.Empty` — it does not host the endpoint.

### Related files
- `apps/lazuar-api/Modules/One/Infrastructure/Endpoints/StorageEndpoints.cs` — policy + key prefix.
- `apps/lazuar-api/src/Lazuar.Api/Middleware/TenantSecurityMiddleware.cs` — `RequiresTenantContext` for `/one/storage`.
- `apps/lazuar-ops/src/modules/workspace/pages/GeneralSettingsPage.tsx` (`handleLogoUpload` ~60) — VIEWER can open settings depending on nav; the API will mint if they can POST.
- `apps/lazuar-ops/src/modules/workspace/pages/BillingProfilePage.tsx` (`handleFileUpload` ~156).
- `apps/lazuar-api/tests/Lazuar.ArchitectureTests/TenantIsolationArchitectureTests.cs` — tenant required, not role.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/TenantIsolation/TenantIsolationHardeningTests.cs` — lying empty-tenant “contract” test.
- `apps/lazuar-api/Modules/One/Infrastructure/Endpoints/ApiCredentialEndpoints.cs` — the OrgAdmin pattern to copy.

### Tests
- Existing: `TenantSecurityMiddleware_Requires_Tenant_For_OrgAdmin_Modules` (path true); `TenantSecurityMiddleware_Exempts_Public_Auth_Webhooks_And_Workspace_Surfaces` (storage not exempt); `Presigned_Storage_Rejects_Empty_Tenant_Contract` (does not exercise the handler).
- None fail if VIEWER receives a URL. The architecture test would still pass after an OrgAdmin policy.
- First regression: VIEWER + valid `X-Tenant-Id` → 403; ADMIN → 200 with `upload_url` whose key starts `vault/{thatTenant}/`; empty tenant → 400. Do not allow `API_CLIENT` unless you add a storage scope (today keys are not OrgAdmin).

### Reproduction today
Arrange: invite a VIEWER, accept, cookie + `X-Tenant-Id`. Act: `POST /api/v1/one/storage/presigned-url` `{ "file_name": "x.png", "content_type": "image/png" }`. Assert today: 200 `upload_url` / `final_url` under `vault/{tenant}/…`. Act: same POST without `X-Tenant-Id`. Assert: 400 (middleware or endpoint). Act: as ADMIN on tenant A with header A, body unchanged. Assert: key is A’s vault, not a caller-supplied path (path is not client-controlled — good).

### Blast radius
Workspace branding and billing-profile images (PII-adjacent if someone uploads IDs). Not a cross-tenant write: key is always `ctx.TenantId`. VIEWER is the least-privilege staff role the Team page advertises as read-only. Frequency: any VIEWER who can discover the route (TypeSpec + ops pages).

### Suggested fix
`.RequireAuthorization("OrgAdmin")` on `MapStorageEndpoints`, same as `/one/api-keys`. Keep the empty-tenant 400. Replace the lying hardening test with a real handler/policy test. Do not take a client-supplied key prefix. No TypeSpec regen required (auth is host policy). No Wave 5 / WhatsApp storage.

### Evaluation notes
Still P2. Empty-tenant half was a pre-wave P1 and is closed; do not reopen it. Not a duplicate of 269 (HasTenantAccess) — storage never calls `HasTenantAccessAsync`; it trusts middleware membership injection. Residual after 161–200.

