# Lazuar.TestSupport

Shared test helpers for `Lazuar.ModuleTests` / `Lazuar.IntegrationTests` (and optional module unit projects).

## Contents

| Type | Purpose |
|------|---------|
| `FakeExecutionContextAccessor` | Real `IExecutionContextAccessor` stand-in (no NSubstitute) |
| `InMemoryDb.CreateOptions<T>()` | Unique EF InMemory options |
| `InMemoryDb.NullMediator` | Publish-only no-op; throws on `Send` so misuse is loud |

## Pilot adoption

**Phase 13** migrated two ModuleTests as a pilot:

- `Lazuar.ModuleTests/Communications/BroadcastClaimTests.cs`
- `Lazuar.ModuleTests/Billing/Commands/DeductTenantCreditIdempotencyTests.cs`

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

### Expanding adoption

1. Prefer Fake over `Substitute.For<IExecutionContextAccessor>()` when only ambient values matter.
2. Keep module-specific seed helpers in the test class or a module folder under ModuleTests.
3. Do **not** add production module project references to TestSupport unless a shared factory is clearly worth the coupling.

## Not a test project

`IsTestProject=false` — no `[Test]` assemblies here; referenced by test projects only.
