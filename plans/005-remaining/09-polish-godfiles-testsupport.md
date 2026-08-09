# 005-remaining / 09 — FW-7 residual polish: god files, TestSupport, outbox DI, ProblemDetails

**Status:** Analysis only — **do not implement from this file alone**  
**Date:** 2026-08-09  
**Repo:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay`  
**Scope:** Backend residual polish tracked as **FW-7** (`plans/004-maintenance/FUTURE-WORK.md`) / future checklist **F14** (`plans/004-maintenance/checklists-future/phase-f14-polish-god-files-testsupport.md`)  
**Out of scope for this analysis:** app code changes, product features, FW-1…FW-6 workstreams  

**Sources of truth used (read, not re-derived from memory):**

| Artifact | Path |
|----------|------|
| FW-7 definition | `plans/004-maintenance/FUTURE-WORK.md` § FW-7 |
| F14 checklist | `plans/004-maintenance/checklists-future/phase-f14-polish-god-files-testsupport.md` |
| Large-file inventory (baseline) | `plans/004-maintenance/02-large-files-chunking.md` |
| Duplication / outbox / errors | `plans/004-maintenance/09-duplication-tech-debt.md` |
| Phase 11 done (P0/P1 splits landed) | `plans/004-maintenance/phase-11-done.md` |
| Phase 13 done (TestSupport pilot + ProblemDetails codes) | `plans/004-maintenance/phase-13-done.md` |
| Phase 13 analysis | `plans/004-maintenance/phase-13-analysis.md` |
| TestSupport library | `apps/lazuar-api/tests/Lazuar.TestSupport/` |
| Arch gate for outbox jobs | `apps/lazuar-api/tests/Lazuar.ArchitectureTests/ModuleBoundaryTests.cs` |

---

## 0. Executive summary

Highest-ROI god-file work from the 004 maintenance track is **done** (One endpoints, Program composition, provision command, dunning, public commerce maps, payment-completed handler, webhook handler). FW-7 is **opportunistic residual polish**:

1. **God files still multi-responsibility or oversized** — primarily `LhdnGatewayAdapter`, residual bulk inside `LlmOrchestratorService` (stream loop), payment-gateway glue duplication, and opportunistic splits of `BillingQueryService` / `B2cConsolidationJob`.
2. **TestSupport exists but is barely adopted** — 2 pilot ModuleTests only; ~15+ ModuleTests still re-roll `UseInMemoryDatabase` + `Substitute.For<IExecutionContextAccessor>()` + `Substitute.For<IMediator>()`; one suite even hand-implements a second `NoopMediator` / `TestExecutionContext`.
3. **Outbox/inbox DI is correct but mechanical** — 9 modules × (keyed bus + outbox job + inbox job + thin subclasses + identical EF fluent config). A shared `AddModuleOutboxInbox<T>` + `ApplyOutboxInbox` is high clarity / low risk **if** it preserves concrete job type names (arch test + Lhdn registration test depend on them).
4. **ProblemDetails** — global handler already emits stable `code` for uncaught throws; **Payments integration** is the M2M exemplar with domain-specific codes. Most endpoint-local catches (LHDN, One auth/credentials/provision, Ops execute-action) still return ProblemDetails **without** `code`. Pagination helper exists; only Ops chat uses it.

**Done-when philosophy (from FW-7):** no global deadline. Prefer house style when the file is already in a PR. At least one polish PR **or** an explicit skip of F14 is enough to close a wave.

---

## 1. What already landed (do not redo)

### 1.1 God-file splits already shipped (Phases 07–11)

| Former monolith | Outcome |
|-----------------|---------|
| One `Endpoints.cs` (~766) | Composer + domain endpoint files |
| `ProvisionAuraWorkspaceCommand` | Command/handler/helpers split |
| `DunningEngineJob` | Partials / stage files |
| `Program.cs` composition | Extracted registration |
| Commerce `PublicEndpoints` (~372) | Composer + product/portal/checkout/custom/arrears maps |
| `GatewayPaymentCompletedIntegrationEventHandler` (~376) | Router + OpenCheckout + Subscription + Helpers partials |
| `ProcessGatewayWebhookCommandHandler` (~306) | Orchestration + Metadata + Logging + Idempotency partials |

House-style templates to **reuse**, not invent:

- **Endpoint composition:** thin `MapXEndpoints` composer + `MapY` static classes in same Infrastructure namespace (folder-only nav).
- **Handler partials:** same type name + ctor + DI surface; private methods move to `TypeName.Stage.cs`.
- **Query partials:** `CommerceQueryService` + `CommerceQueryService.{Area}.cs` (ledger-style reads).

### 1.2 TestSupport pilot (Phase 13)

Library (not a runner):

```
apps/lazuar-api/tests/Lazuar.TestSupport/
  Lazuar.TestSupport.csproj   # IsTestProject=false; refs BB.Application + BB.Infrastructure
  FakeExecutionContextAccessor.cs
  InMemoryDb.cs               # CreateOptions<T> + NullMediator
  README.md
```

**Pilots using it today:**

| File | Pattern |
|------|---------|
| `tests/Lazuar.ModuleTests/Communications/BroadcastClaimTests.cs` | `InMemoryDb.CreateOptions` + `FakeExecutionContextAccessor.EmptyTenant()` + `InMemoryDb.NullMediator` |
| `tests/Lazuar.ModuleTests/Billing/Commands/DeductTenantCreditIdempotencyTests.cs` | same on `BillingDbContext` |

**Intentionally not in TestSupport yet:**

- Per-module factory methods (`CreateCommerceDb()`) — constructors differ; would fan-in every Infrastructure project into TestSupport.
- Configured NSubstitute mid-test tenant mutation (use Fake properties or keep Substitute).
- MediatR `Send` through DbContext pipeline (NullMediator throws on Send).

### 1.3 ProblemDetails pilot (Phase 13)

`BuildingBlocks/Infrastructure/GlobalExceptionHandler.cs` now maps:

| Exception | Status | Title | `extensions.code` |
|-----------|--------|-------|-------------------|
| `BusinessRuleValidationException` | 400 | Business Rule Violation | `business_rule_violation` |
| `InvalidOperationException` | 400 | Validation Error | `invalid_operation` |
| other | 500 | An unexpected error occurred | `internal_error` |

Comment in handler explicitly points to **Payments `IntegrationEndpoints`** as the exemplar for endpoint-local ProblemDetails with stable codes.

### 1.4 Paging helper (Phase 13)

`BuildingBlocks.Application.Paging`:

- `Normalize(page, limit)` → page/limit/skip
- `NormalizeOffset(limit, offset)` → limit/offset/currentPage

**Only consumer found:** `Ops/Infrastructure/Endpoints/ChatEndpoints.cs` (`GET /ops/chat/conversations`). Commerce/Billing/Messaging still hand-clamp `page`/`limit`.

---

## 2. Residual god files — current inventory & prioritized split list

Line counts below are **current end-of-file** sizes from source reads (2026-08-09). Priorities are **FW-7 residual**, not reopening Phase 11 P0 work.

### 2.1 Prioritized split list

| Prio | File (absolute under repo) | ~LOC | Multi-resp? | Recommended technique | When |
|:----:|----------------------------|-----:|:-----------:|-----------------------|------|
| **P1** | `apps/lazuar-api/Modules/Lhdn/Infrastructure/Gateways/LhdnGatewayAdapter.cs` | **384** | Yes (by operation) | **partials by MyInvois operation** | When touching LHDN gateway / rate limits / sandbox |
| **P1** | `apps/lazuar-api/Modules/Ops/Infrastructure/Services/LlmOrchestratorService.cs` | **434** main + existing Prompts/Tools partials | Yes (stream loop bulk) | **partial: Stream** (and optionally setup helpers) | When touching Ops chat stream / tool loop |
| **P2** | Payment gateway adapters (shared glue only) | see §2.4 | Glue only | **`GatewayCommon` static helpers** — **no** mega base class | When touching any of Billplz/Chip/Razorpay/Stripe amount/name |
| **P2** | `…/Billing/Infrastructure/Services/BillingQueryService.cs` | **330** | Partially (ledger/summary/credits/profile) | Partials like CommerceQueryService | When editing billing reads / SQL |
| **P2** | `…/Billing/Infrastructure/Workers/B2cConsolidationJob.cs` | **310** | Partially (schedule + catch-up + per-org) | Partials: Schedule / CatchUp / ProcessOrg | When editing B2C consolidation |
| **P3** | Payment adapters individually (Stripe 352, Chip 357, Billplz 303, Razorpay 277) | — | Cohesive per gateway | **Do not** split unless a single method grows further | Only if file pain returns |
| **P3** | Endpoint monoliths ~210–246 (Lhdn/Billing endpoints already partially split) | — | Mild | Composer pattern if a route family is edited | Opportunistic |
| **Skip** | EF `*ModelSnapshot.cs` / Designer / generated OpenAPI clients | huge | N/A | Never hand-chunk | — |

### 2.2 `LhdnGatewayAdapter` — detailed split design

**Path:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Lhdn/Infrastructure/Gateways/LhdnGatewayAdapter.cs`  
**Implements:** `ILhdnGatewayAdapter` (`Modules/Lhdn/Application/Ports/ILhdnGatewayAdapter.cs`)  
**DI:** `services.AddScoped<ILhdnGatewayAdapter, LhdnGatewayAdapter>()` in Lhdn `DependencyInjection.cs`  
**Consumers:** submission job, status polling job, taxpayer validation service, cancel command, refund credit-note path.

#### Current responsibilities (single class)

| Lines (approx) | Members | Concern |
|----------------|---------|---------|
| 21–42 | fields + ctor | HttpClientFactory, MemoryCache, Configuration, Logger |
| 26–30 | 5 static rate-limiter registries | login / submit / poll / tin / cancel |
| 44–90 | `GetBaseUrl`, `EnforceRateLimitAsync`, `TryAddIntermediaryHeader`, `ExtractRetryAfterSeconds` | shared HTTP plumbing |
| 92–132 | `GetTokenAsync` | OAuth client_credentials + 55m cache |
| 134–216 | `SubmitDocumentAsync` | documentsubmissions + rate limit 100/min |
| 218–322 | `GetDocumentStatusAsync` | poll / parse status (largest method) |
| 324–358 | `ValidateTaxpayerTinAsync` | TIN validation |
| 360–383 | `CancelDocumentAsync` | document state cancel |

This is **one cohesive port** (correct behind `ILhdnGatewayAdapter`) but **multi-operation navigability** is poor — same shape as pre-split webhook handlers.

#### Target layout (behavior-preserving partials)

```
Modules/Lhdn/Infrastructure/Gateways/
  LhdnGatewayAdapter.cs                 # fields, ctor, shared helpers (base URL, rate limit, intermediary, retry-after)
  LhdnGatewayAdapter.Token.cs           # GetTokenAsync
  LhdnGatewayAdapter.Submit.cs          # SubmitDocumentAsync
  LhdnGatewayAdapter.Status.cs          # GetDocumentStatusAsync
  LhdnGatewayAdapter.Tin.cs             # ValidateTaxpayerTinAsync
  LhdnGatewayAdapter.Cancel.cs          # CancelDocumentAsync
```

All remain `public partial class LhdnGatewayAdapter` in namespace `Modules.Lhdn.Infrastructure.Gateways`.

#### Move rules

- [x] Type name `LhdnGatewayAdapter` unchanged (DI registration unchanged).
- [x] Interface method signatures unchanged.
- [x] Static limiter dictionaries stay on the primary partial (shared state).
- [x] No HTTP client / base URL / cache-key format changes.
- [x] Rate limits (12 login, 100 submit, etc.) stay as today unless a product change is intentional.
- [x] Prefer **partials over inheritance** (matches ProcessGatewayWebhook / GatewayPaymentCompleted).

#### Risk

| Risk | Level | Mitigation |
|------|-------|------------|
| Token cache key / TTL drift | Medium | Mechanical move only; smoke Lhdn sandbox scripts if available |
| Rate-limiter statics split incorrectly | Medium | Keep all registries + `EnforceRateLimitAsync` on core partial |
| Test breakage | Low | Few/no unit tests hit adapter internals; integration/sandbox is the gate |
| Over-fragmentation | Low | Cap at 6 files matching interface operations |

#### Verification

- Build `Modules.Lhdn.Infrastructure`
- Existing ModuleTests: `LhdnRateLimitingTests`, `LhdnSingleCreditPathTests`, sandbox E2E if env present
- No DI/registration test changes expected

---

### 2.3 `LlmOrchestratorService` — residual partial cleanup

**Already split:**

| File | ~role |
|------|-------|
| `LlmOrchestratorService.cs` | ctor, `ProcessChatAsync` (~25 LOC), **entire stream loop** (~315 LOC), `GetValidatedTenantId`, `TrackAndLogCost` |
| `LlmOrchestratorService.Prompts.cs` | `BuildInitialMessages`, `BuildChatOptions` |
| `LlmOrchestratorService.Tools.cs` | `BuildProposedAction`, `ExecuteReadToolAsync` (~87 LOC) |
| `ToolCallAccumulator.cs` | byte-stream accumulator (historical BinaryData bug fix — **do not “simplify”**) |

**Main file still owns:**

1. Conversation setup (create vs load, user message persist) — duplicated shape between non-stream and stream.
2. Streaming tool-call loop with `MemoryStream` accumulation (documented historical bug — header block must stay).
3. Tool execution iteration (write propose vs read execute), failure counts, UI request / proposed action emission.
4. Final assistant message persistence + title generation.
5. Cost logging helper.

#### Target residual layout

```
LlmOrchestratorService.cs                 # fields, ctor, ProcessChatAsync, GetValidatedTenantId, TrackAndLogCost
LlmOrchestratorService.Stream.cs          # ProcessChatStreamAsync entire body (or Stream + ToolLoop)
LlmOrchestratorService.Conversation.cs    # optional: shared setup for load/create conversation + persist user msg
LlmOrchestratorService.Prompts.cs         # keep
LlmOrchestratorService.Tools.cs           # keep
ToolCallAccumulator.cs                    # keep + keep historical comment block (or move comment to Stream partial)
```

#### Move rules

- [ ] `ILlmOrchestratorService` surface unchanged.
- [ ] **Do not** “refactor” `BinaryData` → string accumulation (see file header comment).
- [ ] Keep `_maxIterations` / tool failure budget semantics.
- [ ] DI registration / Modules.Ops.Tests ctor mocks stay valid (same public type).

#### Risk

| Risk | Level | Mitigation |
|------|-------|------------|
| Stream regression / JSON corruption | **High** if logic changes | Pure move; run `Modules.Ops.Tests` + `LlmOrchestratorServiceTests` |
| Iterator / yield across partials | Low | C# allows yield in partial methods of async iterators on same type |
| Over-split of tool loop | Medium | Prefer one Stream partial first; Conversation extract only if setup duplication is clear |

**Recommendation:** P1 only when Ops chat is already under edit. Do **not** open a pure cosmetics PR unless stream is actively confusing a bugfix.

---

### 2.4 Payment gateway adapters — shared helpers (not a base class)

**Paths:**

| Adapter | ~LOC | ExtractName? | amount×100? | default name/email? |
|---------|-----:|:------------:|:-----------:|:-------------------:|
| `ChipCollectGatewayAdapter.cs` | 357 | yes (identical) | yes | `"Lazuar Payment"` / `"customer@example.com"` |
| `StripeGatewayAdapter.cs` | 352 | no (SDK-shaped) | yes (`amount * 100`) | `"Lazuar Payment"` |
| `BillplzGatewayAdapter.cs` | 303 | yes (identical) | yes | same defaults |
| `RazorpayGatewayAdapter.cs` | 277 | yes (identical) | yes | name from email |

**Identical private helper today (Billplz / Chip / Razorpay):**

```csharp
private static string ExtractName(string? email)
{
    if (string.IsNullOrWhiteSpace(email)) return "Customer";
    var atIndex = email.IndexOf('@');
    return atIndex > 0 ? email[..atIndex] : "Customer";
}
```

#### Target

```
Modules/Payments/Infrastructure/Gateways/GatewayCommon.cs
```

```csharp
internal static class GatewayCommon
{
    public const string DefaultProductName = "Lazuar Payment";
    public const string PlaceholderEmail = "customer@example.com";

    public static string ExtractName(string? email) { /* same */ }

    public static int ToMinorUnits(decimal amount, int quantity = 1) =>
        (int)Math.Round(amount * quantity * 100m, 0, MidpointRounding.AwayFromZero);

    public static string ProductDescription(string? productName, int quantity) =>
        quantity > 1
            ? $"{productName} (x{quantity})"
            : (string.IsNullOrWhiteSpace(productName) ? DefaultProductName : productName);
}
```

#### Explicit non-goals

- **No** abstract `PaymentGatewayAdapterBase` with virtual HTTP.
- **No** unifying Stripe SDK path with Billplz form posts.
- **No** changing minor-unit rounding semantics without payment tests (Billplz/Chip use `Math.Round`; Razorpay uses cast `(int)(amount * quantity * 100)` — **normalize carefully** or preserve per-adapter casting if money tests depend on truncation).

#### Risk

| Risk | Level | Mitigation |
|------|-------|------------|
| Off-by-one minor units | **High** for money | Prefer extracting only `ExtractName` + default strings first; amount helper only after comparing each adapter’s rounding |
| Webhook parse regressions | Medium | Keep parse methods local |

**Recommended PR shape:** tiny — `GatewayCommon` + replace triple `ExtractName` + string constants. Leave amount conversion to a second PR if any doubt.

---

### 2.5 `BillingQueryService` — partials when editing

**Path:** `…/Billing/Infrastructure/Services/BillingQueryService.cs` (~330 LOC)  
**Pattern to copy:** Commerce `CommerceQueryService` + area partials under same folder.

| Method group | Suggested partial |
|--------------|-------------------|
| `GetLedgerEntriesAsync` | `BillingQueryService.Ledger.cs` |
| `GetFinancialSummaryAsync`, `GetNetProfitAsync` | `BillingQueryService.Summary.cs` |
| credit balance helpers | `BillingQueryService.Credits.cs` |
| `GetBillingProfileAsync` | `BillingQueryService.Profile.cs` |
| ctor + private raw DTO records | primary `BillingQueryService.cs` |

**Do not** introduce Dapper abstractions or rewrite hand-rolled SQL in a “split” PR.

---

### 2.6 `B2cConsolidationJob` — partials when editing

**Path:** `…/Billing/Infrastructure/Workers/B2cConsolidationJob.cs` (~310 LOC)

| Members | Suggested partial |
|---------|-------------------|
| ctor, `ExecuteAsync`, `CalculateTimeToNextConsolidation`, TZ resolve | primary |
| `CatchUpClosedPeriodsAsync`, `RunOnceAsync` | `.CatchUp.cs` |
| `ProcessPeriodAsync`, `ProcessOrgPeriodAsync` | `.Process.cs` |

**Stability:** `RunOnceAsync` / `ProcessPeriodAsync` are `internal` and used by `B2cConsolidationJobTests` — keep accessibility and type name.

---

### 2.7 “Is it a god file?” residual checklist

Mark **god-file** if ≥2 of:

1. >300 LOC hand-maintained production code  
2. Multiple public operations that change for independent reasons  
3. Mix of I/O + policy + mapping in one type without partials  
4. Edit risk: reviewers cannot load the whole file into working memory  

**Not** god-files merely because large: single cohesive gateway adapter HTTP surface *after* partials by operation; EF snapshots; generated clients.

---

## 3. TestSupport rollout

### 3.1 What exists (API surface)

```csharp
// FakeExecutionContextAccessor
FakeExecutionContextAccessor.EmptyTenant()
FakeExecutionContextAccessor.ForTenant(tenantId, userId?)
// mutable: TenantId, UserId, UserRole, IsSystemAdmin, IsTestMode, AuditSignature

// InMemoryDb
InMemoryDb.CreateOptions<TContext>()   // unique Guid database name
InMemoryDb.NullMediator                // Publish no-op; Send/CreateStream throw
```

Pilot pattern (canonical):

```csharp
using Lazuar.TestSupport;

var options = InMemoryDb.CreateOptions<CommunicationsDbContext>();
var ctx = FakeExecutionContextAccessor.EmptyTenant();
return new CommunicationsDbContext(options, ctx, InMemoryDb.NullMediator, new DatabaseJobTrigger());
```

### 3.2 Adoption matrix (ModuleTests)

| Status | Count (approx) | Notes |
|--------|----------------:|-------|
| **Migrated** | **2** | BroadcastClaim, DeductTenantCreditIdempotency |
| **Still copy-paste InMemory + Substitute** | **~15+** ModuleTest fixtures | see candidates below |
| **Authorization WebApplicationFactory tests** | several | DI `AddSingleton(Substitute.For<IExecutionContextAccessor>())` — **low value** to Fake; leave |
| **Domain-only unit tests** (no DbContext) | many | N/A |
| **IntegrationTests** | 0 TestSupport refs | no project reference yet |
| **Modules.Ops.Tests** | uses NSubstitute | separate project; optional later |

### 3.3 Migration candidates — prioritized batches

#### Batch A — mechanical InMemory fixtures (highest ROI, safest)

These construct a module DbContext with empty ambient tenant + no-op mediator and **do not** `Send` through the context pipeline.

| File | Module DbContext | Why easy |
|------|------------------|----------|
| `One/OutboundWebhookClaimTests.cs` | `OneDbContext` | private `CreateDb()` clone of pilot |
| `One/OutboundWebhookTests.cs` | `OneDbContext` | **also deletes** local `TestExecutionContext` + `NoopMediator` (~40 LOC pure debt) |
| `Billing/Workers/B2cConsolidationJobTests.cs` | `BillingDbContext` | SetUp pattern |
| `Billing/EventHandlers/GatewayRefundCompletedHandlerTests.cs` | `BillingDbContext` | SetUp pattern |
| `Billing/EventHandlers/ChargebackClawbackHandlerTests.cs` | `BillingDbContext` | SetUp pattern (still needs real `IMediator` substitute for handler under test — only DbContext uses NullMediator) |
| `Billing/EventHandlers/PlatformTopUpEventHandlerTests.cs` | `BillingDbContext` | same |
| `Billing/EventHandlers/LedgerBalanceMatrixTests.cs` | `BillingDbContext` | same |
| `Commerce/Workers/BillingEngineJobTests.cs` | `CommerceDbContext` | SetUp pattern |
| `Commerce/GatewayPaymentFailedIntegrationEventHandlerTests.cs` | `CommerceDbContext` | CreateDb helper |
| `Commerce/CommerceProductCompletenessTests.cs` | `CommerceDbContext` | CreateDb helper |

**Suggested Batch A size for first PR:** **4–6 files** (One webhook pair + 2–3 Billing event handlers + one Commerce). Mark F14.2 batch of N = that count.

#### Batch B — tenant isolation fixtures

| File | Notes |
|------|-------|
| `TenantIsolation/TenantIsolationHardeningTests.cs` | multiple CreateDb helpers (Commerce/Billing/etc.) — good Fake.ForTenant use when ambient tenant is set |
| `TenantIsolation/CrossTenantIdorTests.cs` | same |

**Caution:** these intentionally set `TenantId` and sometimes rely on query filters. Prefer `FakeExecutionContextAccessor.ForTenant(id)` over Substitute `.TenantId.Returns(...)`.

#### Batch C — leave alone (or only partial adoption)

| File | Why not (yet) |
|------|----------------|
| `*EndpointsAuthorizationTests.cs` | WebApplicationFactory + DI substitutes; Fake adds little |
| `Lhdn/LhdnRateLimitingTests.cs`, `LhdnSingleCreditPathTests.cs` | need configured mediator/gateway substitutes |
| `One/ProvisionAuraWorkspaceTests.cs` | large; multiple Substitute contexts; migrate only when touching provision |
| `Payments/ProcessGatewayWebhookCommandHandlerTests.cs` (if present) / gateway unit tests | often no DbContext |
| Pure domain tests under Billing/Domain, Commerce Dunning, etc. | no fixture |
| `Lhdn/LhdnOutboxPublisherJobRegistrationTests.cs` | DI registration only |

#### Batch D — IntegrationTests (optional second wave)

`Lazuar.IntegrationTests` has **no** TestSupport project reference. Candidates with InMemory:

- `BillingDbContextTests.cs`
- `CreditDeductionConcurrencyTests.cs` (partial — still needs real Postgres for race cases)
- `CommerceQueryServiceTests.cs` (may be Testcontainers — inspect before forcing InMemory helpers)

**Gate:** add ProjectReference to TestSupport only when Batch A proves stable; do not fan-out IntegrationTests and ModuleTests in the same PR unless trivial.

### 3.4 Migration recipe (per test class)

1. Add `using Lazuar.TestSupport;`
2. Replace:
   - `new DbContextOptionsBuilder<T>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options`  
     → `InMemoryDb.CreateOptions<T>()`
   - `Substitute.For<IExecutionContextAccessor>()` used only for empty/default ambient  
     → `FakeExecutionContextAccessor.EmptyTenant()` or `.ForTenant(...)`
   - `Substitute.For<IMediator>()` **only when** no Send is expected on SaveChanges domain events  
     → `InMemoryDb.NullMediator`
3. Delete local `NoopMediator` / `TestExecutionContext` private classes if present.
4. **Keep** NSubstitute for the **SUT’s collaborators** (handlers, gateways, IMediator when the test asserts `Received().Send`).
5. Run the filtered test class; then ModuleTests smoke.

### 3.5 What not to add to TestSupport yet

| Idea | Verdict |
|------|---------|
| `CreateBillingDb()` / `CreateCommerceDb()` in TestSupport | **Defer** — pulls all Infrastructure into TestSupport; place module helpers under `ModuleTests/{Module}/Support/` if needed |
| Testcontainers fixture | **Out of FW-7** — IntegrationTests own Docker policy (documented Phase 13) |
| Endpoint auth assert helpers | Optional later; not blocking |
| Shared `DatabaseJobTrigger` singleton | unnecessary — `new DatabaseJobTrigger()` is fine |

### 3.6 Success metrics for TestSupport rollout

| Metric | Target for “batch done” |
|--------|-------------------------|
| ModuleTests files using `using Lazuar.TestSupport` | pilots 2 → **≥ 8** after Batch A |
| Local `NoopMediator` / duplicate InMemory options builders | decrease; OutboundWebhookTests local types **gone** |
| Behavior change | **none** — fixture only |
| README | update pilot list under `tests/Lazuar.TestSupport/README.md` |

---

## 4. Optional `AddModuleOutboxInbox` design

### 4.1 Current pattern (9 modules)

Every module Infrastructure DI does the same trio (example Commerce):

```csharp
services.AddKeyedScoped<IEventBus, OutboxEventBus<CommerceDbContext>>("CommerceEventBus");
services.AddHostedService<CommerceInboxConsumerJob>();
services.AddHostedService<CommerceOutboxPublisherJob>();
```

Modules: Billing, Commerce, Communications, CRM, Lhdn, Messaging, One, Ops, Payments.

**Thin job subclasses** (~10–15 LOC each × 18 types) exist only for:

1. Concrete hosted-service type for DI  
2. Typed logger category (`ILogger<CommerceOutboxPublisherJob>`)  
3. **Architecture test** name convention  

```csharp
// ModuleBoundaryTests.All_Modules_Should_Have_OutboxPublisherJob_In_Infrastructure
// requires non-abstract type name ending with "OutboxPublisherJob" in each module Infrastructure assembly
```

**Lhdn registration unit test** asserts exact implementation type:

```csharp
d.ImplementationType == typeof(LhdnOutboxPublisherJob)
```

**EF fluent config** (identical × 9 DbContexts):

```csharp
modelBuilder.Entity<OutboxMessage>(builder =>
{
    builder.ToTable("OutboxMessages");
    builder.HasKey(x => x.Id);
    builder.HasIndex(x => new { x.NextAttemptAt, x.OccurredOn }).HasFilter("\"ProcessedAt\" IS NULL");
});
modelBuilder.Entity<InboxMessage>(builder =>
{
    builder.ToTable("InboxMessages");
    builder.HasKey(x => x.Id);
    builder.HasIndex(x => new { x.NextAttemptAt, x.ReceivedAt }).HasFilter("\"ProcessedAt\" IS NULL");
});
```

Base workers already shared and good:

- `OutboxPublisherJob<TDbContext>` (abstract)
- `InboxConsumerJob<TDbContext>` (abstract)
- `OutboxEventBus<TDbContext>`
- `MessageProcessingResultApplier`

### 4.2 Design options

#### Option A — registration helper only (recommended pilot)

```csharp
// BuildingBlocks.Infrastructure/ModuleOutboxInboxServiceCollectionExtensions.cs
public static class ModuleOutboxInboxServiceCollectionExtensions
{
    public static IServiceCollection AddModuleOutboxInbox<TDbContext, TOutboxJob, TInboxJob>(
        this IServiceCollection services,
        string eventBusKey)
        where TDbContext : DbContext
        where TOutboxJob : OutboxPublisherJob<TDbContext>
        where TInboxJob : InboxConsumerJob<TDbContext>
    {
        services.AddKeyedScoped<IEventBus, OutboxEventBus<TDbContext>>(eventBusKey);
        services.AddHostedService<TOutboxJob>();
        services.AddHostedService<TInboxJob>();
        return services;
    }
}
```

**Call site (Commerce after pilot):**

```csharp
services.AddModuleOutboxInbox<CommerceDbContext, CommerceOutboxPublisherJob, CommerceInboxConsumerJob>(
    "CommerceEventBus");
```

**Keeps:** thin subclasses, arch test, Lhdn registration test, typed loggers.  
**Saves:** 3-line copy-paste consistency; harder to forget inbox or bus.  
**Does not delete:** 18 job files (acceptable module tax per `09-duplication-tech-debt.md`).

#### Option B — open generic hosted services (not recommended now)

Make `OutboxPublisherJob<T>` non-abstract; register `AddHostedService<OutboxPublisherJob<CommerceDbContext>>()`.

**Breaks / needs rewrite:**

- Arch test (no `*OutboxPublisherJob` type in module assemblies)
- `LhdnOutboxPublisherJobRegistrationTests`
- Logger category becomes `OutboxPublisherJob\`1[[CommerceDbContext]]` (ops dashboards worse)

Only pursue if deleting 18 files is a product requirement.

#### Option C — factory that still generates typed wrappers

Over-engineered for 9 modules. Reject.

### 4.3 Companion: EF `ApplyOutboxInbox` (pair with Option A)

```csharp
// BuildingBlocks.Infrastructure/OutboxInboxModelBuilderExtensions.cs
public static class OutboxInboxModelBuilderExtensions
{
    public static void ApplyOutboxInbox(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OutboxMessage>(builder =>
        {
            builder.ToTable("OutboxMessages");
            builder.HasKey(x => x.Id);
            builder.HasIndex(x => new { x.NextAttemptAt, x.OccurredOn })
                .HasFilter("\"ProcessedAt\" IS NULL");
        });

        modelBuilder.Entity<InboxMessage>(builder =>
        {
            builder.ToTable("InboxMessages");
            builder.HasKey(x => x.Id);
            builder.HasIndex(x => new { x.NextAttemptAt, x.ReceivedAt })
                .HasFilter("\"ProcessedAt\" IS NULL");
        });
    }
}
```

Each DbContext `OnModelCreating`: `modelBuilder.ApplyOutboxInbox();`

**Critical:** filter SQL string and index columns must stay **byte-identical** to avoid noisy EF migrations. After swap, run `dotnet ef migrations add` dry-check or compare model snapshot — expect **no** model diff. If a snapshot changes, **stop** and fix the extension.

### 4.4 Pilot module choice

| Candidate | Why |
|-----------|-----|
| **CRM** (recommended) | Smallest DI surface; fewest extra hosted jobs; low blast radius |
| Messaging | Also small, but actively uses inbox writers — better as **second** rollout |
| Lhdn | Has explicit registration test — good **verification** target for second module, not first experimental API tweak |

**Pilot steps:**

1. Add BB extensions (`AddModuleOutboxInbox` + `ApplyOutboxInbox`).  
2. Convert **CRM** DI + `CrmDbContext` only.  
3. Run ArchitectureTests + CRM-related smoke.  
4. Confirm **zero** new EF migration.  
5. Document call-site in module DI README or api README one-liner.  
6. Roll out remaining modules in a follow-up PR (or one PR per 3 modules).

### 4.5 Optional later (not part of pilot)

- Stop registering **inbox** hosted services for modules with zero inbox writers (CRM, Ops, Payments, …) — measure first; docs currently bless empty consumers for symmetry.
- `InboxEnvelope.FromIntegrationEvent` for Messaging’s three near-identical enqueue handlers (`09-duplication` §5.3) — separate small PR.
- Do **not** build multi-schema single outbox worker.

### 4.6 Exit criteria for outbox DI helper

| Criterion | Pass |
|-----------|------|
| Pilot module registers bus + both jobs via helper | yes |
| Arch test still green | yes |
| Lhdn registration test still green after Lhdn rollout | yes |
| No EF migration noise | yes |
| Behavior (SKIP LOCKED poll, retry) | unchanged |

---

## 5. ProblemDetails expansion

### 5.1 Current state matrix

| Layer | Shape | Stable `code`? |
|-------|-------|----------------|
| `GlobalExceptionHandler` | RFC7807 ProblemDetails | yes — generic codes only |
| Payments `IntegrationEndpoints` | ProblemDetails + `PaymentErrorCodes` / `PaymentIntegrationException` | **yes — domain codes** (exemplar) |
| LHDN Document / Admin / Config endpoints | ProblemDetails Status+Detail | **no code** |
| One Auth / ApiCredential / IntegrationProvision | ProblemDetails (sometimes) | **no code** |
| Ops ExecuteAction | ProblemDetails | **no code** |
| Communications Broadcast | **plain string** BadRequest | no |
| Commerce admin transactions | `StatusResponse { Status = ex.Message }` | anti-pattern |
| Payments webhooks | anonymous `{ error = ... }` | different contract |
| Many happy paths | uncaught → global handler | generic codes only |

### 5.2 Exemplar to copy (do not invent a second style)

From `Modules/Payments/Infrastructure/IntegrationEndpoints.cs`:

```csharp
private static ProblemDetails Problem(string code, string detail, int status) =>
    new()
    {
        Status = status,
        Title = code,
        Detail = detail,
        Extensions = { ["code"] = code }
    };
```

Domain exception: `PaymentIntegrationException` + `PaymentErrorCodes` constants (`PAYMENTS_NOT_CONFIGURED`, `AMOUNT_INVALID`, …).

### 5.3 Expansion strategy (opportunistic, not a rewrite epic)

**Rule:** when an endpoint file is already open for feature/bugfix, upgrade its catch blocks to include `Extensions["code"]`. Do **not** open a repo-wide “all endpoints return codes” PR unless product requires SDK contract freeze.

#### Priority surfaces (M2M / SDK first)

| Priority | Surface | Suggested codes (illustrative) |
|:--------:|---------|--------------------------------|
| **P0** | LHDN document submit/cancel/TIN validate | `IDEMPOTENCY_KEY_REQUIRED`, `INSUFFICIENT_CREDITS` (402), `BUSINESS_RULE`, `DOCUMENT_NOT_FOUND`, `TIN_VALIDATION_FAILED` |
| **P0** | One integrator provision | `UNAUTHORIZED`, `FORBIDDEN`, `RATE_LIMITED`, `CONFLICT`, `VALIDATION_ERROR` |
| **P1** | One OrgAdmin API credentials | `VALIDATION_ERROR`, `NOT_FOUND` |
| **P1** | Payments integration (already done) | maintain only |
| **P2** | Ops execute-action | `INVALID_TOOL`, `INVALID_PAYLOAD`, `TENANT_REQUIRED` |
| **P3** | Communications / Commerce admin string errors | convert when touching; prefer ProblemDetails over strings |

#### Shared helper options

1. **Per-module private `Problem(...)`** (Payments style) — fine for 1–2 files.  
2. **BuildingBlocks helper** (only if ≥3 modules copy the same method):

```csharp
// BuildingBlocks.Application or Infrastructure
public static class ProblemDetailsFactory
{
    public static ProblemDetails Create(string code, string detail, int status) => new()
    {
        Status = status,
        Title = code,
        Detail = detail,
        Extensions = { ["code"] = code }
    };
}
```

Do **not** force all modules onto one exception hierarchy in FW-7. Optional later: `NotFoundException` → 404 in global handler (called out in `09-duplication` §7.4) — that is a **contract change**; gate behind product/docs.

### 5.4 Global handler residual risks (document, optional fix)

| Issue | Today | Optional polish |
|-------|-------|-----------------|
| 500 `Detail = exception.Message` | possible info leak | map to generic detail in Production; log server-side |
| not-found as 400 | `InvalidOperationException("… not found")` | introduce `NotFoundException` + 404 mapping when product wants it |
| 402 LHDN credits | endpoint special-cases message prefix `"402"` | stable code `INSUFFICIENT_CREDITS` + proper status (TypedResults may need non-BadRequest path) |

### 5.5 Pagination companion (F14.3)

`Paging` exists; expand usage when list endpoints are touched:

| Endpoint area | Today | Target |
|---------------|-------|--------|
| Ops chat conversations | `Paging.NormalizeOffset` | keep (legacy offset) |
| Commerce products/subscribers/transactions | hand clamp | `Paging.Normalize` |
| Billing ledger admin | `page ?? 1`, `limit ?? 50` | `Paging.Normalize` |
| Messaging list | `Math.Clamp(limit ?? 50, 1, 200)` | either adopt helper with maxLimit=200 or leave (different max) |

---

## 6. PR sequence (recommended)

Principles from F14: **one file family per PR preferred**; no behavior change without tests; opportunistic.

### Wave order

| PR | Title (copy-paste) | Scope | Risk | Depends on |
|----|--------------------|-------|------|------------|
| **PR-1** | `test(api): migrate ModuleTests batch to Lazuar.TestSupport` | Batch A: 4–6 fixtures (must include `OutboundWebhookTests` NoopMediator deletion) | Low | none |
| **PR-2** | `refactor(payments): extract GatewayCommon name/defaults` | `GatewayCommon` + replace ExtractName + string constants (not amount math yet) | Low–Med | none |
| **PR-3** | `refactor(lhdn): partial LhdnGatewayAdapter by operation` | Token/Submit/Status/TIN/Cancel partials | Low if pure move | none |
| **PR-4** | `refactor(ops): move LlmOrchestrator stream to partial` | `LlmOrchestratorService.Stream.cs` | Med (stream) | none; run Ops tests |
| **PR-5** | `refactor(bb): AddModuleOutboxInbox + ApplyOutboxInbox pilot (CRM)` | BB helpers + CRM only | Med (EF) | none |
| **PR-6** | `refactor(bb): roll out AddModuleOutboxInbox to remaining modules` | 8 modules DI + DbContext | Med | PR-5 green, zero migrations |
| **PR-7** | `fix(lhdn): ProblemDetails codes on document endpoints` | LHDN M2M error codes | Med (client-visible) | optional after PR-3 |
| **PR-8** | opportunistic | BillingQueryService / B2cConsolidationJob partials; Paging adoption; amount helper | Low | when those areas edit |

### Parallelism

- PR-1 ∥ PR-2 ∥ PR-3 ∥ PR-5 are independent.  
- PR-4 independent but protect stream carefully.  
- PR-6 after PR-5.  
- PR-7 can ship without PR-3 but nicer after navigability.

### Explicit skip path

If capacity is zero: mark F14 “wave skips residual polish” with a note pointing to this file; FW-7 remains opportunistic.

---

## 7. Verification matrix (per workstream)

| Workstream | Build | Tests | Extra |
|------------|-------|-------|-------|
| TestSupport Batch A | ModuleTests | filtered migrated classes + full ModuleTests smoke | update TestSupport README pilot list |
| GatewayCommon | Payments.Infrastructure | `BillplzGatewayAdapterTests` + any checkout tests | manual amount checks if amounts extracted |
| LhdnGatewayAdapter | Lhdn.Infrastructure | Lhdn ModuleTests; sandbox scripts if credentials | DI unchanged |
| LlmOrchestrator Stream | Ops.Infrastructure | `Modules.Ops.Tests` (all) | do not alter ToolCallAccumulator semantics |
| AddModuleOutboxInbox pilot | BB + CRM | ArchitectureTests; no new CRM migration | compare model snapshot |
| Outbox rollout | all modules | ArchitectureTests + `LhdnOutboxPublisherJobRegistrationTests` | zero migrations |
| ProblemDetails LHDN | Lhdn.Infrastructure | endpoint/auth tests; contract consumers if any | document codes in api README if public SDK |

---

## 8. Non-goals (FW-7 / F14)

- Re-splitting already-done Phase 07–11 files for taste  
- Hand-editing EF snapshots / generated OpenAPI clients  
- Mega `PaymentGatewayAdapterBase`  
- Full ModuleTests → TestSupport migration in one PR  
- Per-module factories inside TestSupport (fan-in)  
- Multi-schema single outbox worker  
- Removing empty inbox jobs without measurement  
- Repo-wide endpoint error rewrite / new global exception taxonomy as a single epic  
- Frontend / TypeSpec Wave B (FW-6) / LLM factory move to Ops (FW-3)  

---

## 9. Mapping to checklists

### F14 checklist fill-in

| Item | Analysis conclusion |
|------|---------------------|
| F14.1 LhdnGatewayAdapter partials | **Do** — P1; design in §2.2 |
| F14.1 LlmOrchestrator remaining partials | **Do when touching** — Stream partial §2.3 |
| F14.1 Payment gateway shared helpers | **Do** — ExtractName/defaults first §2.4 |
| F14.1 BillingQueryService / B2cConsolidationJob | **Opportunistic P2** §2.5–2.6 |
| F14.2 TestSupport batch of N | **N = 4–6** first PR; candidates §3.3 Batch A |
| F14.2 Document remaining high-copy suites | §3.3 Batches B–D |
| F14.3 `AddModuleOutboxInbox<T>` pilot | **Option A + ApplyOutboxInbox on CRM** §4 |
| F14.3 Expand ProblemDetails `code` | M2M first (LHDN, provision) §5 |
| F14.3 Pagination shared helper | expand on touch §5.5 |
| F14.4 Exit | ≥1 polish PR **or** explicit skip |

### FW-7 “Done when”

Opportunistic. Prefer house style when the file is already in the PR. This analysis is the **how**; implementation is optional residual capacity after FW-1…FW-6 product work.

---

## 10. Suggested ticket titles

1. `test(api): migrate ModuleTests batch to Lazuar.TestSupport (FW-7)`  
2. `refactor(payments): extract GatewayCommon name/defaults (FW-7)`  
3. `refactor(lhdn): partial LhdnGatewayAdapter by MyInvois operation (FW-7)`  
4. `refactor(ops): LlmOrchestratorService stream partial (FW-7)`  
5. `refactor(bb): AddModuleOutboxInbox + ApplyOutboxInbox pilot on CRM (FW-7)`  
6. `refactor(bb): roll out module outbox/inbox DI helper (FW-7)`  
7. `fix(lhdn): stable ProblemDetails codes on document SDK routes (FW-7)`  

---

## 11. Appendix — absolute paths quick index

### God files / adapters

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Lhdn/Infrastructure/Gateways/LhdnGatewayAdapter.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Ops/Infrastructure/Services/LlmOrchestratorService.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Ops/Infrastructure/Services/LlmOrchestratorService.Prompts.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Ops/Infrastructure/Services/LlmOrchestratorService.Tools.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/BillplzGatewayAdapter.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/ChipCollectGatewayAdapter.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/RazorpayGatewayAdapter.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/StripeGatewayAdapter.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Billing/Infrastructure/Services/BillingQueryService.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Billing/Infrastructure/Workers/B2cConsolidationJob.cs`

### TestSupport

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/tests/Lazuar.TestSupport/`
- Pilots: `…/ModuleTests/Communications/BroadcastClaimTests.cs`, `…/ModuleTests/Billing/Commands/DeductTenantCreditIdempotencyTests.cs`

### Outbox DI exemplars

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/DependencyInjection.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/CRM/Infrastructure/DependencyInjection.cs` (pilot candidate)
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/BuildingBlocks/Infrastructure/OutboxPublisherJob.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/BuildingBlocks/Infrastructure/InboxConsumerJob.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/BuildingBlocks/Infrastructure/OutboxEventBus.cs`

### ProblemDetails exemplars

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Infrastructure/IntegrationEndpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Application/Exceptions/PaymentIntegrationException.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/BuildingBlocks/Infrastructure/GlobalExceptionHandler.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Lhdn/Infrastructure/Endpoints/DocumentEndpoints.cs` (upgrade candidate)

### House-style templates already in-repo

- Commerce query partials: `…/Commerce/Infrastructure/Services/CommerceQueryService*.cs`
- Commerce public endpoint maps: `…/Commerce/Infrastructure/Endpoints/Public*.cs`
- Webhook handler partials: `…/Payments/Application/Commands/ProcessGatewayWebhookCommandHandler*.cs`

---

**End of analysis.** No application code was changed by this document.
