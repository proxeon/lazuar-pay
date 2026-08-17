---
number: "328"
id: B10-X26
severity: P2
status: open
source: plans/009-bugs/10-tenancy-workers-contracts-tests.md
head: "297ba98"
---

# 328 — B10-X26 — Tests that pin bugs, tautologies, or never run

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/10-tenancy-workers-contracts-tests.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B10-X26 — P2 — Tests that pin bugs, tautologies, or never run

**Tautology**

```279:288:apps/lazuar-api/tests/Lazuar.ModuleTests/TenantIsolation/TenantIsolationHardeningTests.cs
    public void Presigned_Storage_Rejects_Empty_Tenant_Contract()
    {
        var tenantId = Guid.Empty;
        tenantId.Should().Be(Guid.Empty);
        Assert.That(tenantId == Guid.Empty, Is.True);
    }
```

The real guard is `StorageEndpoints.cs` 27–32. This test does not call it. Name claims a contract. Body is `true == true`.

**Assert.Pass as a stand-in for a behavior test**

`LhdnDocumentSubmittedIntegrationEventHandlerTests.HandleAsync_CompletesWithoutWalletOrMediatorDependencies` ends `Assert.Pass("... does not call wallet...")`. The sibling test that inspects constructor parameters is the real lock. The Pass test is a comment.

**Always skipped**

`LhdnSandboxE2ETests` is `[Ignore("Requires active Sandbox credentials...")]`. Two `[Test]` methods never run in CI. The class `SetUp` throws if env vars are missing — dead code under Ignore.

**Skip when Docker / Postgres missing**

- `CreditDeductionConcurrencyTests` (3): Testcontainers; `_postgresReady` → `Assert.Ignore`. The **only** suite that proves Billing migrations + real `xmin`-adjacent concurrency.
- `BillingQueryServiceTests` (2): opens `localhost:5432` or `LAZUAR_TEST_PG`; Ignore if down. Then **creates ad-hoc** `LedgerEntries` / `LedgerLines` tables if missing — **not** the EF model. A Dapper query can pass against a toy schema that production migrations would have altered.
- `CommerceQueryServiceTests` (4): Testcontainers **without** try/catch. Docker down ⇒ fixture **throws**, not Ignore. CI `dotnet` job has a Postgres service but this fixture starts **its own** container. Depending on runner Docker-in-Docker, this either proves Wave 3 commerce migrations or reds the whole class.

**InMemory mis-shelved as Integration**

`BillingDbContextTests` (1): EF InMemory. Lives in `Lazuar.IntegrationTests` because of history. It does not integrate.

**Pins an incomplete world**

- `SubscriptionLifecycleWebhookTests` five-type matrix (B10-X17).
- `CrossTenantIdorTests`: 8 handlers. **No** `PauseCollectionCommand` / `ResumeCollectionCommand` / `ChangePlanCommand` / LHDN / Payments / Communications IDOR. Pause handlers have the org guard in production and **zero** tests.
- `LhdnOutboxPublisherJobRegistrationTests` does not assert inbox job registration.
- `ModuleBoundaryTests` does not assert outbox **registration**.
- No test of `OutboxPublisherJob` / `InboxConsumerJob` loop (SKIP LOCKED, poison, TypeResolver, non-INotification).
- No two-worker claim test.

**Test inventory (this tree, `rg [Test]`, bin/obj excluded)**

| Project | `[Test]` |
|---------|----------|
| `Lazuar.ArchitectureTests` | 14 |
| `Lazuar.IntegrationTests` | 10 |
| `Lazuar.ModuleTests` | 972 |
| `Modules.Billing.Tests` | 20 |
| `Modules.Ops.Tests` | 5 |
| **Sum** | **1021** |

008 counted 993. The delta is Wave-fix tests (pause sibling, accept-invite, honesty-adjacent), not a new integration spine.

