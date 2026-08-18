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

## Evaluation (current tree, 2026-08-18)

### What the bug is
A catalog of tests that lie: a storage “contract” that asserts `Guid.Empty == Guid.Empty`, an LHDN handler test that ends in `Assert.Pass`, a sandbox fixture that is `[Ignore]` forever, Docker/Postgres suites that skip or build a toy schema, an InMemory test living in `Lazuar.IntegrationTests`, and gaps that pin an incomplete world (no pause/change-plan IDOR, no outbox/inbox loop, no two-worker `SKIP LOCKED`). CI can be green while the named behaviors are unproven.

### Still present?
**STILL BROKEN**

The named liars are still in the tree.

Tautology unchanged:

```335:343:apps/lazuar-api/tests/Lazuar.ModuleTests/TenantIsolation/TenantIsolationHardeningTests.cs
    public void Presigned_Storage_Rejects_Empty_Tenant_Contract()
    {
        var tenantId = Guid.Empty;
        tenantId.Should().Be(Guid.Empty);
        // Endpoint returns 400 when ctx.TenantId == Empty before key construction.
        Assert.That(tenantId == Guid.Empty, Is.True);
    }
```

The real guard is still `StorageEndpoints.cs` 27–32 (`if (tenantId == Guid.Empty) return BadRequest(...)`). This test never calls it.

`Assert.Pass` still stands in for a behavior test (`LhdnDocumentSubmittedIntegrationEventHandlerTests.HandleAsync_CompletesWithoutWalletOrMediatorDependencies`, 16–32). The sibling `HandlerType_HasNoMediatorOrBillingRepositoryConstructorDeps` (36–43) is still the only real lock.

`LhdnSandboxE2ETests` is still `[Ignore("Requires active Sandbox credentials...")]` (20–21) with two `[Test]` methods (`GetTokenAsync_ShouldReturnValidJwt_FromLhdnSandbox`, `GetDocumentStatusAsync_ShouldReturnStatus_ForKnownSubmission`). `SetUp` still throws if env vars are missing.

Skip / toy-schema / throw-on-Docker:

- `CreditDeductionConcurrencyTests` (3 tests): Testcontainers; `_postgresReady` false → tests Ignore (`OneTimeSetUp` 33–56).
- `BillingQueryServiceTests` (2): opens `LAZUAR_TEST_PG` or `localhost:5432`, `Assert.Ignore` if down, then `CREATE TABLE IF NOT EXISTS` a two-table toy `LedgerEntries`/`LedgerLines` (39–64) — not the EF model.
- `CommerceQueryServiceTests` (4): Testcontainers **without** try/catch (`OneTimeSetUp` 26–37). Docker down ⇒ fixture throws. `DapperQueries_ShouldMatchEntityFrameworkSchema` (74–87) is still `Assert.DoesNotThrowAsync`.

`BillingDbContextTests` (1) is still EF InMemory inside `Lazuar.IntegrationTests` (`UseInMemoryDatabase`, 27–29).

Incomplete world (partially improved, not closed):

- `SubscriptionLifecycleWebhookTests.Payload_FiveEventTypes_ShareRequiredFields` still parametrizes only ACTIVE/PAST_DUE/CANCELED/SUSPENDED (108–112). 173 added `Payload_ActivateTrial_EmitsTrialingAndZeroAmount` (137–151) — catalog/status honesty is better; the five-type name still omits TRIALING.
- `CrossTenantIdorTests` still has 8 methods: cancel (immediate + period-end), keep, anonymize, refund, update/delete coupon, billing ledger filter. **No** `PauseCollectionCommand` / `ResumeCollectionCommand` / `ChangePlanCommand` / LHDN / Payments / Communications IDOR. Production pause handler **does** re-check org (`ChangePlanCommandHandler.cs` 100–104); `SubscriptionCollectionPauseTests` is domain-only.
- `LhdnOutboxPublisherJobRegistrationTests` still asserts only `LhdnOutboxPublisherJob` as `IHostedService` (16–33). Inbox is registered via the helper (Lhdn DI 40) but this test does not say so. ArchitectureTests `Every_Module_Registers_OutboxInbox_Via_Helper` is the real lock (issue 327).
- `ModuleBoundaryTests.All_Modules_Should_Have_OutboxPublisherJob_In_Infrastructure` still checks type existence, not `AddHostedService`.
- No test drives `OutboxPublisherJob` / `InboxConsumerJob` `ExecuteAsync` (SKIP LOCKED, poison, TypeResolver, mid-list throw). 160 added `InboxNotificationRequirementTests`; 162 added `TypeResolverTests`; 161 added `InMemoryEventBusTests.Publish_With_No_Handlers_Throws`. Those are unit locks, not the job loop.
- No two-worker claim test in `Lazuar.IntegrationTests` (still 10 `[Test]` methods: 4 commerce + 2 billing query + 3 credit + 1 InMemory).

Inventory today (`[Test]`, bin/obj excluded): ArchitectureTests **22**, IntegrationTests **10**, ModuleTests **1198**, `Modules.Billing.Tests` **20**, `Modules.Ops.Tests` **5**, sum **1255** (audit was 1021). The delta is Wave-fix + 001–200 work, not a new integration spine.

### Related files
- `apps/lazuar-api/tests/Lazuar.ModuleTests/TenantIsolation/TenantIsolationHardeningTests.cs` — tautology.
- `apps/lazuar-api/Modules/One/Infrastructure/Endpoints/StorageEndpoints.cs` — the uncalled guard.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Billing/EventHandlers/LhdnDocumentSubmittedIntegrationEventHandlerTests.cs` — Pass-as-comment.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Lhdn/LhdnSandboxE2ETests.cs` — always skipped.
- `apps/lazuar-api/tests/Lazuar.IntegrationTests/{CreditDeductionConcurrencyTests,BillingQueryServiceTests,CommerceQueryServiceTests,BillingDbContextTests}.cs` — skip / toy schema / throw / InMemory.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/TenantIsolation/CrossTenantIdorTests.cs` and `Commerce/SubscriptionCollectionPauseTests.cs` — IDOR gap vs domain-only pause.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Lhdn/LhdnOutboxPublisherJobRegistrationTests.cs` — name overclaims drain.
- Issues 160, 161, 162, 173 (closed sibling honesty); 327 (arch-test holes); 332 (untested composition).

### Tests
- Existing tests that touch this path: listed above. They **pass** while lying.
- Whether any test would fail if the bug is still there: **no** — that is the bug. `Presigned_Storage_Rejects_Empty_Tenant_Contract` cannot go red if `StorageEndpoints` drops the Empty check.
- First regression test: host `MapStorageEndpoints`, call POST with `TenantId == Guid.Empty`, expect 400 and no `IR2StorageService` call. Delete or rewrite the tautology. Replace `Assert.Pass` with a spy that wallet/Deduct is never resolved. Give Commerce Testcontainers the same `_postgresReady` Ignore as credit tests. Point `BillingQueryServiceTests` at a migrated `BillingDbContext` (or move it to ModuleTests). Add `PauseCollection_ForeignOrg_ThrowsNotFound` next to cancel. Add one Testcontainers two-worker `FOR UPDATE SKIP LOCKED` claim.

### Reproduction today
Arrange: `dotnet test --filter Presigned_Storage_Rejects_Empty_Tenant_Contract` (green). Delete the Empty check in `StorageEndpoints.cs`. Re-run the same filter. Assert: still green. Run `LhdnSandboxE2ETests` in CI: ignored, never red. Stop Docker and run `CommerceQueryServiceTests`: fixture throws (not Ignore). Stop Postgres and run `BillingQueryServiceTests`: Ignore, or if a leftover toy table exists, the Dapper query can pass against a schema production migrations would have altered.

### Blast radius
CI confidence. Credit xmin races and Wave 3 commerce migrations are proven only when Docker is up. A storage Empty-tenant regression ships with a green “contract” test. Frequency: every main build. Not a buyer-facing money bug until the untested path is the one that breaks.

### Suggested fix
Rewrite the tautology against the endpoint. Turn `Assert.Pass` into a constructor/DI assertion only (the sibling already is — delete the Pass test). Keep sandbox `[Ignore]` but rename the class so it is not “E2E proof.” Harmonize IntegrationTests: Ignore (don’t throw) without Docker; migrate real models, don’t `CREATE TABLE` toys; move `BillingDbContextTests` to ModuleTests. Add pause/change-plan IDOR and one SKIP LOCKED two-worker test. Do not TypeSpec-regen. Do not treat 327’s new scrapes as a substitute for these bodies.

### Evaluation notes
Still P2. 173 closed the “four-status webhook matrix” half; the five-type test name remains. 160–162 added unit tests around inbox/EventBus/TypeResolver — they do not replace a job-loop test (332). 180 unified DI; `LhdnOutboxPublisherJobRegistrationTests` still overclaims. Not blocked; can be sliced per liar. Do not mark resolved while the tautology and Pass tests remain.


