# R50 notes — TestSupport migration batch

**Track:** Polish (FW-7 / F14.2)  
**Checklist:** `checklists/r50-polish-testsupport-batch.md`  
**Analysis:** `09-polish-godfiles-testsupport.md` §3  
**Date:** 2026-08-09  
**Commit:** none (workspace only)

---

## Goal

Migrate a first Batch A of ModuleTests off copy-paste `UseInMemoryDatabase` + `Substitute.For<IExecutionContextAccessor>()` / local `NoopMediator` onto `Lazuar.TestSupport`.

---

## Batch A — migrated (N = 6)

| File | Change |
|------|--------|
| `One/OutboundWebhookClaimTests.cs` | `InMemoryDb.CreateOptions` + `FakeExecutionContextAccessor.EmptyTenant` + `NullMediator` |
| `One/OutboundWebhookTests.cs` | same; **deleted** local `TestExecutionContext` + `NoopMediator` (~40 LOC) |
| `Billing/EventHandlers/GatewayRefundCompletedHandlerTests.cs` | SetUp fixture → TestSupport |
| `Billing/EventHandlers/ChargebackClawbackHandlerTests.cs` | DbContext → TestSupport; **kept** `Substitute.For<IMediator>()` for handler SUT `Send` asserts |
| `Billing/EventHandlers/PlatformTopUpEventHandlerTests.cs` | SetUp fixture → TestSupport |
| `Commerce/GatewayPaymentFailedIntegrationEventHandlerTests.cs` | SetUp fixture → TestSupport; **kept** `ILogger` substitute |

### Recipe applied

```csharp
using Lazuar.TestSupport;

_db = new XDbContext(
    InMemoryDb.CreateOptions<XDbContext>(),
    FakeExecutionContextAccessor.EmptyTenant(),
    InMemoryDb.NullMediator,
    new DatabaseJobTrigger());
```

### Verification

```text
dotnet test tests/Lazuar.ModuleTests --filter "...(6 classes)..."
Passed!  Failed: 0, Passed: 40, Skipped: 0
```

### Adoption metric

ModuleTests files with `using Lazuar.TestSupport`: **10** (target ≥ 8 after Batch A).

Prior pilots (Phase 13 / earlier): BroadcastClaim, DeductTenantCreditIdempotency, DocumentPublished, PlatformAdminAuthQuery (partial).

---

## Remaining high-copy suites (next batches)

### Batch B candidates — mechanical InMemory (same recipe)

| File | Notes |
|------|-------|
| `Billing/Workers/B2cConsolidationJobTests.cs` | SetUp clone |
| `Billing/EventHandlers/LedgerBalanceMatrixTests.cs` | SetUp clone |
| `Commerce/Workers/BillingEngineJobTests.cs` | SetUp clone |
| `Commerce/CommerceProductCompletenessTests.cs` | CreateDb helper |
| `Messaging/DispatchMessageIntegrationEventHandlerTests.cs` | SetUp clone |

### Batch C — tenant isolation (prefer `ForTenant`)

| File | Notes |
|------|-------|
| `TenantIsolation/TenantIsolationHardeningTests.cs` | multiple CreateDb helpers (Commerce/Billing/…) |
| `TenantIsolation/CrossTenantIdorTests.cs` | ambient tenant + query filters |

### Leave alone (or partial only)

| File | Why |
|------|-----|
| `*EndpointsAuthorizationTests.cs` | WebApplicationFactory + DI substitutes |
| `Lhdn/LhdnRateLimitingTests.cs`, `LhdnSingleCreditPathTests.cs` | configured mediator/gateway substitutes |
| `One/ProvisionAuraWorkspaceTests.cs` | large multi-context substitutes |
| `One/PlatformAdminAuthQueryTests.cs` | already partial TestSupport; remaining InMemory/DI auth path low value |
| Pure domain tests | no DbContext fixture |
| `Lazuar.IntegrationTests` | no TestSupport ProjectReference yet — second wave after ModuleTests Batch B |

---

## Out of scope (R50)

- Per-module factories inside `Lazuar.TestSupport` (fan-in)
- IntegrationTests project reference
- Behavior changes (fixture-only)
- Commit / PR (local workspace)
