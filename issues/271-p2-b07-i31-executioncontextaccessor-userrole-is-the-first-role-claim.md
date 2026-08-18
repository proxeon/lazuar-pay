---
number: "271"
id: B07-I31
severity: P2
status: open
source: plans/009-bugs/07-one-identity-invites-keys.md
head: "297ba98"
---

# 271 — B07-I31 — `ExecutionContextAccessor.UserRole` is the first role claim

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/07-one-identity-invites-keys.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B07-I31 — P2 — `ExecutionContextAccessor.UserRole` is the first role claim

**Where.** `ExecutionContextAccessor.cs:38`.

**What.** After injection the first claim is JWT `CLIENT`, the second is `ADMIN`. Anything that reads `UserRole` thinks the owner is a CLIENT. Policies use `IsInRole` and are fine. New code that switches on `UserRole` will be wrong.

## Evaluation (current tree, 2026-08-18)

### What the bug is
`IExecutionContextAccessor.UserRole` is implemented as `FindFirst(ClaimTypes.Role)`. After a normal ops login the JWT/cookie always carries `CLIENT` (or `SUPER_ADMIN` for genesis admins). `TenantSecurityMiddleware` then **appends** the workspace membership role (`ADMIN` / `MEMBER` / `VIEWER` / `SUPER_ADMIN`) as a second `ClaimTypes.Role` when `X-Tenant-Id` resolves. `FindFirst` returns the JWT value, so `UserRole` is `CLIENT` for the owner of the workspace. Authorization policies use `IsInRole` / `RequireRole` and see both claims, so `OrgAdmin` still works. The property is a loaded foot-gun: any new command that switches on `ctx.UserRole == "ADMIN"` will treat every human as a client. Today **no production caller reads `UserRole`** (grep hits only the accessor, the interface, and the fake). The bug is still in the API surface waiting for the next handler.

### Still present?
**STILL BROKEN**

```37:39:apps/lazuar-api/src/Lazuar.Api/ExecutionContextAccessor.cs
    public string UserRole => _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Role)?.Value ?? "";

    public bool IsSystemAdmin => _httpContextAccessor.HttpContext?.User?.FindFirst("is_system_admin")?.Value == "true";
```

Cookie still writes JWT `CLIENT` first (`AuthEndpoints.cs:273–277`). Middleware still injects membership second (`TenantSecurityMiddleware.cs:90–94`). `IsSystemAdmin` correctly reads the bool claim, not the role string (load-bearing vs B07-I20). `FakeExecutionContextAccessor.UserRole` defaults to `"OrgAdmin"` (`FakeExecutionContextAccessor.cs:13`) — a third string that is neither JWT nor staff role, which would hide a future `UserRole` switch in tests.

### Related files
- `apps/lazuar-api/src/Lazuar.Api/ExecutionContextAccessor.cs` — the one-line getter.
- `apps/lazuar-api/BuildingBlocks/Application/IExecutionContextAccessor.cs` — public contract.
- `apps/lazuar-api/src/Lazuar.Api/Middleware/TenantSecurityMiddleware.cs` — second role claim.
- `apps/lazuar-api/Modules/One/Infrastructure/Endpoints/AuthEndpoints.cs` `IssueCookie` — first role claim.
- `apps/lazuar-api/tests/Lazuar.TestSupport/FakeExecutionContextAccessor.cs` — misleading default.
- `apps/lazuar-api/Modules/One/Infrastructure/Services/OneQueryService.cs` `GetTenantRoleAsync` — the value callers actually want.
- Dual-role teaching hole also lives in issue 259 / B07-I15 (`CLIENT` JWT vs staff membership).

### Tests
- Existing: none assert `ExecutionContextAccessor.UserRole` after injection. Policy tests (`ApiKeyAuthenticationTests`, `WorkspaceCreateAuthorizationTests`) use `IsInRole` / source scrape.
- No test fails today because nothing reads the property.
- First regression: build an `HttpContext` with claims `Role=CLIENT` then `Role=ADMIN`; `new ExecutionContextAccessor(...).UserRole` must not be `CLIENT` if the property is defined as “workspace role.” Alternatively delete the property and assert it is gone from the interface.

### Reproduction today
Arrange: register a workspace owner, log in (cookie role `CLIENT`), call any tenant-required route with `X-Tenant-Id` of that workspace (middleware adds `ADMIN`). Act: inspect `HttpContext.User.FindAll(ClaimTypes.Role)` vs `IExecutionContextAccessor.UserRole`. Assert today: two role claims; `UserRole == "CLIENT"`. Policies `IsInRole("ADMIN")` still succeed. No current endpoint returns `UserRole` to the client (`GET /one/auth/me` uses the JWT role at `AuthEndpoints.cs:222`).

### Blast radius
None in production paths today. The blast is the next feature that authorizes on `UserRole` (keys, refunds, legal, archive). That would silently down-scope every owner to CLIENT or, if someone “fixes” it by trusting JWT CLIENT as staff, re-open invite `CLIENT` (invite tests currently reject that string). Dual-role confusion is already how B07-I20 happened (`IsInRole("SUPER_ADMIN")`).

### Suggested fix
Pick one: (1) change `UserRole` to prefer a membership claim when present (`GetTenantRoleAsync` / last `ClaimTypes.Role` that is `ADMIN|MEMBER|VIEWER|SUPER_ADMIN`, else JWT), and document it; or (2) remove `UserRole` from the interface and force callers through `GetTenantRoleAsync` / `IsInRole`. Update the fake’s default away from `"OrgAdmin"`. Do not collapse JWT `CLIENT` into a staff role. Do not TypeSpec-regen.

### Evaluation notes
Still P2, honesty/foot-gun, not an open steal. Sibling of 259 (B07-I15 dual role + README CLIENT) and 123 (B07-I20 provision `SUPER_ADMIN`). Residual after 161–200: nobody started using `UserRole`, which is why this has not paged yet.

