# Lazuar.TestSupport

Shared test helpers for `Lazuar.ModuleTests` / `Lazuar.IntegrationTests` (and optional module unit projects).

## Contents

| Type | Purpose |
|------|---------|
| `FakeExecutionContextAccessor` | Real `IExecutionContextAccessor` stand-in (no NSubstitute) |
| `InMemoryDb.CreateOptions<T>()` | Unique EF InMemory options |
| `InMemoryDb.NullMediator` | Publish-only no-op; throws on `Send` so misuse is loud |

## Adoption

**Phase 13 pilots** + **R50 Batch A** ModuleTests:

| File | Module |
|------|--------|
| `Communications/BroadcastClaimTests.cs` | Communications |
| `Communications/DocumentPublishedIntegrationEventHandlerTests.cs` | Communications |
| `Billing/Commands/DeductTenantCreditIdempotencyTests.cs` | Billing |
| `Billing/EventHandlers/GatewayRefundCompletedHandlerTests.cs` | Billing (R50) |
| `Billing/EventHandlers/ChargebackClawbackHandlerTests.cs` | Billing (R50) |
| `Billing/EventHandlers/PlatformTopUpEventHandlerTests.cs` | Billing (R50) |
| `One/OutboundWebhookClaimTests.cs` | One (R50) |
| `One/OutboundWebhookTests.cs` | One (R50; deleted local `NoopMediator` / `TestExecutionContext`) |
| `One/PlatformAdminAuthQueryTests.cs` | One (partial: InMemory query path) |
| `Commerce/GatewayPaymentFailedIntegrationEventHandlerTests.cs` | Commerce (R50) |

### Pattern

```csharp
using Lazuar.TestSupport;

var options = InMemoryDb.CreateOptions<CommunicationsDbContext>();
var ctx = FakeExecutionContextAccessor.EmptyTenant();
return new CommunicationsDbContext(options, ctx, InMemoryDb.NullMediator, new DatabaseJobTrigger());
```

### When **not** to use yet

- Tests that need **configured NSubstitute returns** on `TenantId` mid-test (use Substitute or mutate Fake properties).
- Tests that **Send** MediatR commands through the same DbContext pipeline (use a real mediator / substitute that implements `Send`).
- Full per-module factory methods (`CreateCommerceDb()`) — intentionally deferred until more pilots prove the shape; constructors differ and pulling every module Infrastructure into TestSupport would create a heavy fan-in project.
- WebApplicationFactory auth suites (`*EndpointsAuthorizationTests`) — DI substitutes stay; Fake adds little.

### Expanding adoption

1. Prefer Fake over `Substitute.For<IExecutionContextAccessor>()` when only ambient values matter.
2. Keep module-specific seed helpers in the test class or a module folder under ModuleTests.
3. Do **not** add production module project references to TestSupport unless a shared factory is clearly worth the coupling.
4. See remaining high-copy suites in `plans/005-remaining/r50-notes.md`.

## Not a test project

`IsTestProject=false` — no `[Test]` assemblies here; referenced by test projects only.
