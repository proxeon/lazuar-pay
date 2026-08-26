---
number: "289"
id: B08-M19
severity: P2
status: open
source: plans/009-bugs/08-communications-messaging-crm.md
head: "297ba98"
---

# 289 — B08-M19 — `POST /messaging/notify` trusts body.TenantId

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/08-communications-messaging-crm.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B08-M19 — P2 — `POST /messaging/notify` trusts body.TenantId

**Where:** `Endpoints.cs` 23–27; `SendTenantNotificationCommand` / handler 22–29.

**What:** OrgAdmin of tenant A can pass tenant B’s id. Sink is `ConsoleMessagingService` with B’s slug. Authz test only checks the policy name, not the id binding.

---

## Evaluation (current tree, 2026-08-18)

### What the bug is
`POST /api/v1/messaging/notify` binds the MediatR command `SendTenantNotificationCommand(TenantId, Message)` **from the JSON body**. The endpoint is `RequireAuthorization("OrgAdmin")` and `TenantSecurityMiddleware` requires a tenant context on `/api/v1/messaging/*`, but neither the endpoint nor the handler compares `command.TenantId` to `IExecutionContextAccessor.TenantId`. An OrgAdmin of tenant A (valid JWT + `X-Tenant-Id: A`) can POST `tenantId: B` and the handler loads B’s `TenantReplica` and logs `[System Alert for {B.Name}]` to `ConsoleMessagingService` using B’s **slug** as the recipient. The sink is console/SMS-stand-in (Decision 00.4, not WhatsApp product). Authz tests only assert the policy name on endpoint metadata.

### Still present?
**STILL BROKEN**

```23:27:apps/lazuar-api/Modules/Messaging/Infrastructure/Endpoints.cs
        group.MapPost("/notify", async (SendTenantNotificationCommand command, IMediator mediator) =>
        {
            await mediator.Send(command);
            return Results.Accepted();
        }).RequireAuthorization("OrgAdmin");
```

```21:29:apps/lazuar-api/Modules/Messaging/Application/SendTenantNotificationCommandHandler.cs
    public async Task Handle(SendTenantNotificationCommand request, CancellationToken cancellationToken)
    {
        var tenant = await _tenantReplicaRepository.GetByIdAsync(request.TenantId);
        if (tenant == null || !tenant.IsActive)
        {
            throw new InvalidOperationException("Tenant is not active or does not exist inside local replicas.");
        }

        await _messagingService.SendMessageAsync(tenant.Slug, $"[System Alert for {tenant.Name}]: {request.Message}");
    }
```

`MessagingEndpointsAuthorizationTests.MapMessagingEndpoints_Notify_Requires_OrgAdmin` (`MessagingEndpointsAuthorizationTests.cs:16–43`) only inspects `IAuthorizeData`. `TenantIsolationArchitectureTests` only asserts `RequiresTenantContext("/api/v1/messaging/notify")` (`TenantIsolationArchitectureTests.cs:89`). Contrast `GET /messaging/delivery-logs`, which **does** filter `l.OrganizationId == ctx.TenantId` (`Endpoints.cs:31–38`).

### Related files
- `apps/lazuar-api/Modules/Messaging/Infrastructure/Endpoints.cs` — body-bound command; delivery-logs is the correct binding pattern.
- `apps/lazuar-api/Modules/Messaging/Application/SendTenantNotificationCommandHandler.cs` — trusts `request.TenantId`.
- `apps/lazuar-api/Modules/Messaging/Infrastructure/Messaging/ConsoleMessagingService.cs` — current sink (log line, `IsBillable = false`).
- `apps/lazuar-api/src/Lazuar.Api/Middleware/TenantSecurityMiddleware.cs` — requires *a* tenant, not that it matches the body.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Messaging/MessagingEndpointsAuthorizationTests.cs` — policy-name only.
- `apps/lazuar-api/tests/Lazuar.ArchitectureTests/TenantIsolationArchitectureTests.cs` — path requires tenant header.

### Tests
- Existing: `MessagingEndpointsAuthorizationTests.MapMessagingEndpoints_Notify_Requires_OrgAdmin`; `TenantIsolationArchitectureTests.TenantSecurityMiddleware_Requires_Tenant_For_OrgAdmin_Modules`.
- Neither would fail if body `TenantId` still disagrees with ambient tenant. A comment next to `RequireAuthorization` would still pass the metadata test (same class of lie the 009 audit called out for other One tests).
- First regression: host-level or handler-level test — OrgAdmin of A + `X-Tenant-Id: A` + body `{ tenantId: B, message: "x" }` → 403/400 and **no** `SendMessageAsync` for B’s slug. Happy path: body omitted or forced to `ctx.TenantId`.

### Reproduction today
Arrange: two tenants in Messaging `TenantReplica`, user who is OrgAdmin of A only. Act: `POST /api/v1/messaging/notify` with cookie/JWT for A, header `X-Tenant-Id: A`, body `{ "tenantId": "<B>", "message": "ping" }`. Assert: 202; logs `[Local Dispatch] [MESSAGING/SMS] To: <B-slug> | Text: [System Alert for <B-name>]: ping`. Delivery-logs GET for A does not show it (different table); the leak is the console line + replica read of B.

### Blast radius
OrgAdmin (not anonymous — that 001 gap is closed). Sink is a log line today, not buyer SMS, so this is tenancy/authz honesty rather than a PII blast. If WhatsApp is ever wired behind `IMessagingService`, this becomes a cross-tenant send. Frequency: only if someone calls this internal notify (honesty-allowlisted, not TypeSpec product). Still **P2** while the sink is console. Do not “fix” by building real WhatsApp.

### Suggested fix
Ignore body `TenantId`; take `IExecutionContextAccessor.TenantId` in the endpoint (same as email-config / broadcasts). Optionally drop `TenantId` from the command. Keep OrgAdmin. Do not regenerate TypeSpec (route is honesty-allowlisted / internal). Do not implement Wave 5 WhatsApp to make the bug “more real.”

### Evaluation notes
Not 165/292. Early gap docs that said notify was anonymous are stale (`RequireAuthorization("OrgAdmin")` is live). Still P2. Not blocked.

