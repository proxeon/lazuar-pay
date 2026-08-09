# 07 — Backend tests & EF migrations hygiene

**Repo:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay`  
**Scope:** `apps/lazuar-api/tests/**`, all `Modules/*/Infrastructure/Migrations/**`, Taskfile migrate tasks  
**Date:** 2026-08-09  
**Constraint of this analysis:** read-only; no app code changes  

**Related prior work (stale in places):**
- `docs/001-gaps/16-testing-coverage.md` (2026-08-03, written against earlier “lazuar-hub” tree; understates current coverage — many Phase A/B/C tests were added after)
- `plans/001-backend/001-backend-solidification-checklist.md` Phase 0.4 / C.9 test residuals

---

## 1. Executive snapshot

| Area | Current state | Hygiene risk |
|---|---|---|
| Test projects | **5** projects in `Lazuar.slnx` and `task api:test` | **CI omits `Modules.Ops.Tests`** |
| Test source files | **~67** `.cs` test sources under `apps/lazuar-api/tests/` (excluding bin/obj) | Growing; no shared fixture library |
| Active `[Test]` methods | **~250–300** (order of magnitude; UBL golden suite fully commented; LHDN sandbox `[Ignore]`) | Hotspot-driven, not systematic |
| Production surface | **9 modules** + BuildingBlocks + host; hundreds of production `.cs` files | Large residual gaps (auth cookie login, DunningEngineJob, host E2E, most gateways) |
| EF migrations | **9 schemas**, **~49** forward migrations (Jun 27 → Aug 7 2026 window), **~107** files including Designers + Snapshots | Manageable now; Commerce is the long chain; Communications has add/remove churn |
| Apply path | Host **auto-migrates all 9 contexts on boot** + Taskfile `api:db:migrate` | Dual path OK; Taskfile migrate is optional/redundant when API boots |
| Docker dependency | Integration suite mixes **service Postgres**, **Testcontainers**, and **InMemory** | Documented inconsistently; Commerce Testcontainers **hard-fails** without Docker |

**Bottom line:** Test hygiene improved materially since the August gap doc (webhooks, credits concurrency, tenant isolation, integration checkout, ledger matrix, dunning domain, outbound webhooks). Remaining debt is structural: **no shared test harness**, **split Billing unit project vs ModuleTests**, **CI/Taskfile drift on Ops**, **dead golden-master asset**, **hand-rolled SQL in one integration test that can drift from EF**, and **migration chains that will eventually want squashing once environments are stable**.

---

## 2. Test project structure

### 2.1 Inventory (absolute paths)

| Project | Path | Framework / packages | In `Lazuar.slnx` | In `task api:test` | In `.github/workflows/ci.yml` `dotnet` job |
|---|---|---|---|---|---|
| Architecture | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/tests/Lazuar.ArchitectureTests/` | NUnit, NetArchTest.Rules, FluentAssertions, NSubstitute | Yes | Yes | Yes |
| Integration | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/tests/Lazuar.IntegrationTests/` | NUnit, EF InMemory, Testcontainers.PostgreSql, FluentAssertions, NSubstitute | Yes | Yes | Yes |
| Module (shared multi-module) | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/tests/Lazuar.ModuleTests/` | NUnit, FluentAssertions, NSubstitute, EF InMemory, ASP.NET Core shared framework | Yes | Yes | Yes |
| Billing pure domain | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/tests/Modules.Billing.Tests/` | NUnit, FluentAssertions; refs Billing.Infrastructure | Yes | Yes | Yes |
| Ops unit | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/tests/Modules.Ops.Tests/` | NUnit, FluentAssertions, NSubstitute, Configuration | Yes | Yes | **No** |

### 2.2 Architecture — `Lazuar.ArchitectureTests`

**Sources:**
- `ModuleBoundaryTests.cs` — modular layering + BuildingBlocks/SharedKernel purity + outbox job presence
- `TenantIsolationArchitectureTests.cs` — fail-closed filter source guards + middleware allowlist + document link signer
- `TestData/lhdn-golden-master.json` — **embedded resource, unused by any test code**

**Project references (healthy):** Explicit ProjectReferences to all 9 module Domain/Application/Infrastructure layers (CRM has no Application), BuildingBlocks, SharedKernel, and host. Static constructor **force-loads** assemblies via `typeof(...)` anchors so NetArchTest cannot vacuous-pass on unloaded modules.

**Tests (12 methods):**

| Method | Intent |
|---|---|
| `Domain_Should_Remain_Completely_Isolated` | Domain ↛ other modules / own App / own Infra |
| `Application_Should_Not_Reference_Infrastructure` | App ↛ own Infra (skips CRM) |
| `Outer_Layers_Should_Only_Reference_Other_Modules_Through_Contracts` | App/Infra only cross-module via Contracts |
| `All_Modules_Should_Have_OutboxPublisherJob_In_Infrastructure` | Concrete `*OutboxPublisherJob` per module |
| `BuildingBlocks_Must_Not_Reference_Module_Assemblies` | Domain-agnostic BB |
| `SharedKernel_Must_Not_Reference_Modules_Or_Contain_Entity_Types` | Marker purity |
| `Module_Domain_Should_Not_Reference_BuildingBlocks_Application_Or_Infrastructure` | Domain only BB.Domain |
| `PlatformDbContext_Filter_Must_Not_Treat_Empty_Tenant_As_All_Rows` | Source-scan fail-closed |
| `OpsDbContext_HasQueryFilter_Override_Must_Include_OrganizationId` | Ops soft-delete + tenant |
| `TenantSecurityMiddleware_Requires_Tenant_For_OrgAdmin_Modules` | Path predicates |
| `TenantSecurityMiddleware_Exempts_Public_Auth_Webhooks_And_Workspace_Surfaces` | Exempt list |
| `DocumentLinkSigner_Draft_And_Final_Payloads_Differ` | Draft vs final payload |

**Strengths vs older gap analysis:** Silent-skip of missing assemblies is **fixed**. Assembly load pins exist. BuildingBlocks/SharedKernel rules added (C.9).

**Residual architecture gaps:**
- No Contracts purity rule (Contracts must not reference Infrastructure/Application)
- No cyclic Contracts graph check
- No rule that handlers live only in Application/Infrastructure
- No rule forbidding Infrastructure referencing foreign DbContexts
- Tenant isolation tests are **source-string** guards (brittle to refactor rename, but cheap and useful)
- Golden master still orphaned in Architecture project (belongs next to LHDN UBL tests if restored)

### 2.3 Integration — `Lazuar.IntegrationTests`

**Sources (4 files):**

| File | Infra | What it proves | Skip / fail behavior |
|---|---|---|---|
| `BillingDbContextTests.cs` | EF **InMemory** | Child `CreditLedger` with pre-assigned Guid append after change-tracker clear (EF concurrency footgun regression) | Always runs |
| `BillingQueryServiceTests.cs` | **Live Postgres** via `LAZUAR_TEST_PG` or docker-compose defaults | Financial summary SQL: net revenue ignores TOPUP expense; second summary path | `Assert.Ignore` if connect fails |
| `CommerceQueryServiceTests.cs` | **Testcontainers** Postgres + `MigrateAsync` on Commerce only | Dapper query methods **do not throw** on empty org (schema smoke) | **Hard-fails** `OneTimeSetUp` if Docker unavailable (no try/catch) |
| `CreditDeductionConcurrencyTests.cs` | **Testcontainers** + full Billing `MigrateAsync` | Concurrent deduct + same/different idempotency keys under real Postgres | Soft `Assert.Ignore` if container start fails |

**Project references:** Billing.Infrastructure, Commerce.Infrastructure, BuildingBlocks.Infrastructure only — intentionally narrow.

**CI:** Job provides `postgres:16-alpine` + `LAZUAR_TEST_PG`. GitHub-hosted runners have Docker, so Testcontainers usually work, but this is **not documented** in the workflow comments. Local `task api:test` without Docker will fail Commerce fixture setup unless Testcontainers can pull/start images.

### 2.4 Module tests — `Lazuar.ModuleTests` (primary suite)

This is the **bulk** of behavioral coverage. Folder map:

```
Lazuar.ModuleTests/
  Billing/          Commands, Domain, EventHandlers, Workers
  BuildingBlocks/   AesSecretVault, MessageProcessingResultApplier, MessageRetryPolicy
  Commerce/         Domain, handlers, workers, endpoint auth metadata
  Communications/   Domain + template substitution + claim
  CRM/              ClientProfile anonymize event
  EventHandlers/    Host ApiKeyRevoked cache eviction
  Lhdn/             Submit/credit path, secrets, claim lease, endpoints auth, outbox registration, sandbox (ignored), UBL (commented)
  Messaging/        Delivery log domain + notify endpoint OrgAdmin metadata
  Observability/    CorrelationId, health readiness, metrics
  One/              API key middleware, platform credentials, outbound webhooks, Aura provision
  Payments/         Webhook handler, off-session charge, Billplz adapter, integration checkout, secrets/soft-disable
  TenantIsolation/  Cross-tenant IDOR + hardening (filters, handlers)
```

**Style mix:**
- Pure domain unit (FluentAssertions, no I/O)
- Handler unit with **NSubstitute** ports/repos
- EF **InMemory** for multi-entity orchestration (Billing handlers, Commerce jobs, claim leases)
- Endpoint **metadata** authorization tests (no full HTTP host)
- Middleware unit (ApiKeyAuthenticationMiddleware with `IMemoryCache`)

**Heavy reference surface** (`Lazuar.ModuleTests.csproj`): Host API, BuildingBlocks.Infrastructure, Messaging/One/Lhdn/Billing/Communications/CRM/Commerce/Payments (Domain+App+Infra as needed). This project is becoming a **monolithic test assembly** — flexible for cross-module event tests, expensive for build/restore isolation.

### 2.5 `Modules.Billing.Tests` (pure domain wallet/hold)

| File | Coverage |
|---|---|
| `TenantCreditBalanceTests.cs` | TopUp / Deduct / Clawback edges (~10 tests) |
| `CreditHoldTests.cs` | Construct / consume / settle / release / invalid amounts (~10 tests) |

**Overlap with ModuleTests:** Domain credit behavior is **not** re-tested in ModuleTests; ModuleTests covers **command/handler** idempotency (`DeductTenantCreditIdempotencyTests`) and event-driven top-up/clawback. This split is intentional but **undocumented** — newcomers may put Billing domain tests in either project.

**Smell:** Project references **Infrastructure** even though tests only need Domain (heavier than necessary; slows restores and widens surface).

### 2.6 `Modules.Ops.Tests`

| File | Coverage |
|---|---|
| `Services/LlmOrchestratorServiceTests.cs` | Reflection on private `ExecuteReadToolAsync`: empty JSON, malformed JSON, tenant inject, mediator exception → error string (~4 tests) |

**Not covered:** Tool registry, write tools, streaming, conversation persistence, Ops endpoints, rename/delete commands.

**CI gap:** Present in Taskfile and solution; **absent from CI `dotnet` job steps**. Local `task api:test` runs it; PR CI does not.

### 2.7 Runner matrix (current truth)

| Entry point | What runs |
|---|---|
| `task api:test` | All **5** test projects |
| `.github/workflows/ci.yml` `dotnet` job | Architecture + Integration + Module + Billing (**not Ops**) |
| `apps/lazuar-api/package.json` → `dotnet test` | Solution-wide discovery under cwd (effectively all test projects if run from `apps/lazuar-api`) |
| Root turbo/pnpm `test` | Invokes package scripts; API runs `dotnet test` |

**Inconsistency to fix (hygiene, not feature work):** Align CI with Taskfile for Ops; prefer single `dotnet test Lazuar.slnx --filter "Category!=Sandbox"` long-term.

---

## 3. Coverage map vs modules

Qualitative heat map (● = relative depth, not % line coverage):

| Module / area | Domain unit | Handler / app unit | Integration (DB/real) | Host / E2E |
|---|---|---|---|---|
| **Billing** credits wallet/hold | ●●●●● | ●●●○○ | ●●●○○ (concurrency + EF child) | ○○○○○ |
| **Billing** ledger accounting | ●●○○○ | ●●●○○ (matrix + refunds) | ●○○○○ (financial summary SQL) | ○○○○○ |
| **Billing** jobs / PDF / sequences | ○○○○○ | ●●○○○ (B2cConsolidationJob) | ○○○○○ | ○○○○○ |
| **Commerce** dunning domain | ●●●○○ | ○○○○○ | ●○○○○ (query smoke) | ○○○○○ |
| **Commerce** subscription recovery | ●●●●○ | ●●●○○ (payment failed handler) | ○○○○○ | ○○○○○ |
| **Commerce** billing engine job | ○○○○○ | ●●○○○ | ○○○○○ | ○○○○○ |
| **Commerce** DunningEngineJob | ○○○○○ | ○○○○○ | ○○○○○ | ○○○○○ |
| **Commerce** checkout / coupons / products | ●●○○○ | ●●○○○ | ●○○○○ (smoke) | ○○○○○ |
| **Payments** inbound webhooks | ○○○○○ | ●●●●○ | ○○○○○ | ○○○○○ |
| **Payments** gateways | ○○○○○ | ●○○○○ (Billplz only) | ○○○○○ | ○○○○○ |
| **Payments** integration checkout | ●●○○○ | ●●●●○ | ○○○○○ | ○○○○○ |
| **One** API keys / credentials | ●●○○○ | ●●●●○ | ○○○○○ | ○○○○○ |
| **One** outbound webhooks | ●●○○○ | ●●●○○ | ○○○○○ | ○○○○○ |
| **One** Aura provision | ○○○○○ | ●●●●○ | ○○○○○ | ○○○○○ |
| **One** cookie auth / register / password | ○○○○○ | ○○○○○ | ○○○○○ | ○○○○○ |
| **Lhdn** submit / credits / secrets | ●●○○○ | ●●●○○ | ○○○○○ | ●○○○○ (manual scripts + ignored sandbox) |
| **Lhdn** UBL strategies | ○○○○○ (commented) | ○○○○○ | ○○○○○ | ○○○○○ |
| **Communications** broadcast/suppression | ●●●●○ | ●●○○○ | ○○○○○ | ○○○○○ |
| **CRM** | ●●○○○ (anonymize event) | ○○○○○ | ○○○○○ | ○○○○○ |
| **Messaging** | ●●○○○ | ●○○○○ (endpoint metadata) | ○○○○○ | ○○○○○ |
| **Ops** LLM | ○○○○○ | ●●○○○ (private method) | ○○○○○ | ○○○○○ |
| **BuildingBlocks** outbox retry policy | ○○○○○ | ●●●○○ | ○○○○○ | ○○○○○ |
| **Architecture / tenancy guards** | n/a | n/a | ●●●●○ | n/a |
| **Frontend apps** | ○○○○○ | ○○○○○ | ○○○○○ | ○○○○○ |

### 3.1 Per-module file-level test inventory

#### Billing (strongest money coverage)

**Tests exist for:**
- Domain: `TenantCreditBalance`, `CreditHold`, `LedgerEntry` / account types balance validation
- Commands: `DeductTenantCredit` idempotency (InMemory)
- Handlers: Gateway payment completed (ordering), refund completed, platform top-up, chargeback clawback, LHDN document submitted → ledger, manual subscriber enrolled, ledger balance matrix (fee/tax/FX shapes)
- Worker: `B2cConsolidationJob`
- Integration: EF child append, financial summary SQL, concurrent deduct/idempotency on Postgres

**Still thin / missing:**
- `ReserveCredits` / `ConsumeCreditHold` / `ReleaseCreditHold` command handlers
- `ClawbackCreditsCommandHandler` (domain clawback tested; orchestration path less so)
- `ApiCreditPurchasedHandler`, `StarterCreditSeederHandler`, `ZeroAmountCheckoutHandler`, `CommissionAccruedHandler`, `InvoiceIssuedHandler`
- LHDN validated/cancelled → billing status handlers
- `RevenueRecognitionJob`, document PDF generation, sequence numbers
- Full double-entry property suite beyond current matrix

#### Commerce

**Tests exist for:**
- `DunningCampaign` domain (recovery/churn metrics, product/payment matching, archive)
- `Subscription` recovery / dunning state transitions (`SubscriptionRecoveryTests`)
- `Coupon` reserve/confirm/release
- `ChargeAttemptLog` multi-attempt model
- `GatewayPaymentFailedIntegrationEventHandler` (PAST_DUE / dunning entry)
- `BillingEngineJob` (subset)
- Product completeness / gateway provider constraints
- Subscription lifecycle → outbound webhook publish
- Endpoint OrgAdmin metadata smoke
- Integration: empty-schema Dapper smoke

**Still thin / missing:**
- **`DunningEngineJob`** (campaign matching over time, day offsets, final action SUSPEND/CANCEL, reminder dispatch) — highest remaining revenue-risk gap
- `CheckoutSessionExpiryJob`
- `GatewayPaymentCompletedIntegrationEventHandler` in Commerce (activation vs recovery vs one_time) — partially covered via domain + Billing side
- `GatewayRefundCompletedIntegrationEventHandler` Commerce side
- Coupon max-use races under concurrency (Postgres)
- Public checkout HTTP flow
- Most of `CommerceQueryService` correctness (pagination, filters, mapping) beyond no-throw

#### Payments

**Tests exist for:**
- `ProcessGatewayWebhookCommandHandler` (signature fail, unknown type, idempotency, PAYMENT_COMPLETED / FAILED / refund / dispute routing — solid unit suite)
- `ExecuteOffSessionChargeIntegrationEventHandler`
- Billplz adapter (subset)
- Integration checkout create/get, secrets vault + soft-disable config, outbound webhook fan-out, endpoint auth metadata

**Still thin / missing:**
- Stripe / CHIP / Razorpay adapter parsing & signature verification
- `GatewayRefundRequestedIntegrationEventHandler`
- Full webhook HTTP endpoint → handler chain
- Platform payment endpoints

#### One (identity / platform)

**Tests exist for:**
- `ApiKeyAuthenticationMiddleware` (cache hit, dual-read LHDN keys, scopes, revocation)
- Generate/list/revoke platform API credentials
- Outbound webhook endpoints domain + claim lease
- `ProvisionAuraWorkspace` command matrix (large file — entitlement, external ref, validation)

**Still thin / missing (critical product path):**
- Register / login / logout cookie JWT
- Forgot/reset password, email verify, change password + security stamp
- Workspace invite/accept/remove membership authorization beyond provision path
- Password hash upgrade-on-login (documented in `apps/lazuar-api/docs/008-password-hashing-compatibility-upgrade-on-login.md`)
- `WebApplicationFactory` host pipeline for auth cookies

#### Lhdn

**Tests exist for:**
- Submit path / single credit deduct path
- Secrets vault encrypt/decrypt roundtrip
- Tax document claim lease
- Developer API keys generate/list + middleware key parsing
- Endpoints authorization metadata
- Outbox publisher job DI registration
- Rate-limiting-named submit test (still primarily happy-path mock; name remains somewhat misleading)

**Still thin / missing:**
- Entire UBL golden suite **commented out** in `Strategies/UblStrategyTests.cs`
- Embedded golden master in ArchitectureTests **never loaded**
- Sandbox E2E fixture permanently `[Ignore]`
- XSD validator, cancel, self-billed, multi-document batch
- Manual scripts under `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/scripts/lhdn_sandbox/` remain the real E2E

#### Communications

**Tests exist for:**
- Broadcast lifecycle domain
- Broadcast claim (InMemory)
- SuppressionEntry validation/normalization
- TenantEmailConfiguration domain
- Default message templates presence/content
- Dunning template variable substitution (no unresolved `{{plan_name}}`)

**Still thin / missing:**
- Fan-out job, Resend webhook compliance, credit hold integration with billing, full send path

#### CRM / Messaging / Ops

| Module | Present | Missing |
|---|---|---|
| CRM | Anonymize domain event contract | Resolve/create profile handlers, query service, consent persistence under filters |
| Messaging | MessageDeliveryLog domain; notify endpoint `OrgAdmin` metadata | Dispatch pipeline, tenant seeding, delivery job |
| Ops | Private LLM tool JSON + tenant inject | Full agent loop, streaming, conversation CRUD, tool allowlists |

#### BuildingBlocks / Host / Observability

**Present:** Message retry/dead-letter policy unit tests; AES secret vault; correlation ID middleware; health readiness; metrics counters; ApiKeyRevoked host handler cache eviction; architecture outbox job presence.

**Missing:** OutboxPublisherJob SKIP LOCKED integration under failure; InboxConsumerJob drain; full multi-module event chain (Payments → outbox → Commerce inbox → Billing ledger).

### 3.2 Comparison to `docs/001-gaps/16-testing-coverage.md`

That document is **stale**. Specifically it claims:
- ModuleTests / Billing / Ops not in `task api:test` → **false now** (Taskfile includes all five)
- Payments / One / Commerce domain effectively zero → **false now** (substantial ModuleTests)
- Architecture silent-skip → **false now**

Keep the gap doc as historical evidence; this maintenance plan is the current hygiene baseline.

---

## 4. Test duplication & shared fixtures opportunity

### 4.1 Repeated patterns (high duplication)

Across ModuleTests + IntegrationTests the following setup is copy-pasted **~18+ times**:

```csharp
var options = new DbContextOptionsBuilder<TDbContext>()
    .UseInMemoryDatabase(Guid.NewGuid().ToString())
    .Options;

var executionContext = Substitute.For<IExecutionContextAccessor>();
executionContext.TenantId.Returns(tenantId);

return new TDbContext(
    options,
    executionContext,
    Substitute.For<IMediator>(),
    new DatabaseJobTrigger());
```

**Files repeating this (non-exhaustive):**
- `Billing/Commands/DeductTenantCreditIdempotencyTests.cs`
- `Billing/Workers/B2cConsolidationJobTests.cs`
- `Billing/EventHandlers/{PlatformTopUp,ChargebackClawback,LedgerBalanceMatrix,GatewayRefundCompleted}*.cs`
- `Commerce/GatewayPaymentFailedIntegrationEventHandlerTests.cs`
- `Commerce/CommerceProductCompletenessTests.cs`
- `Commerce/Workers/BillingEngineJobTests.cs`
- `Communications/BroadcastClaimTests.cs`
- `One/OutboundWebhookTests.cs`, `OutboundWebhookClaimTests.cs`
- `TenantIsolation/TenantIsolationHardeningTests.cs`, `CrossTenantIdorTests.cs`
- `Lazuar.IntegrationTests/BillingDbContextTests.cs`

**Testcontainers Postgres builder** is duplicated between:
- `CommerceQueryServiceTests` (fail-hard)
- `CreditDeductionConcurrencyTests` (fail-soft + MigrateAsync)

Different skip policies for the same Docker dependency.

### 4.2 Billing split across two projects

| Concern | Project |
|---|---|
| Pure domain wallet/hold | `Modules.Billing.Tests` |
| Domain ledger types | `Lazuar.ModuleTests/Billing/Domain` |
| Handlers / jobs / commands | `Lazuar.ModuleTests/Billing/*` |
| Postgres concurrency / SQL summary | `Lazuar.IntegrationTests` |

This is workable but causes:
- Two places for “Billing tests”
- Different reference weights (Infrastructure-only vs multi-module)
- No shared `BillingTestFactory`

### 4.3 Endpoint authorization tests

Nearly identical pattern for Minimal API metadata:
- `MessagingEndpointsAuthorizationTests`
- `LhdnEndpointsAuthorizationTests`
- `CommerceEndpointsAuthorizationTests`
- `IntegrationCheckoutEndpointsAuthorizationTests`

Candidate helper: `AssertEndpointRequiresPolicy(endpoints, method, path, policyName)`.

### 4.4 Recommended shared fixture shape (future work — not implemented)

```
tests/
  Lazuar.TestSupport/                 # NEW class library (not a test project)
    InMemoryDbContextFactory.cs       # generic PlatformDbContext factory + substitutes
    PostgresContainerFixture.cs       # shared Testcontainers + soft-skip policy
    EndpointAuthorizationAssert.cs
    TenantContextSubstitute.cs
```

Then:
1. Point Integration + ModuleTests at TestSupport.
2. Standardize **Category** attributes: `Unit`, `Integration`, `RequiresDocker`, `Sandbox`.
3. Document in README: `dotnet test --filter "Category!=RequiresDocker"` for no-Docker laptops.

### 4.5 Dead / misleading assets

| Asset | Issue |
|---|---|
| `Lazuar.ArchitectureTests/TestData/lhdn-golden-master.json` | Embedded, never read |
| `Lhdn/Strategies/UblStrategyTests.cs` | Entire body commented; fixture still compiles as empty |
| `Lhdn/LhdnRateLimitingTests.cs` | Name implies rate limits; primarily happy-path submit mock |
| `CommerceQueryServiceTests` | Name implies schema parity verification; only no-throw smoke |

Hygiene actions: restore UBL + golden master together, or delete both; rename misnamed fixtures; add data assertions to Commerce query tests or rename to `CommerceQueryServiceSchemaSmokeTests`.

---

## 5. Migration folder size & organization

### 5.1 Layout convention (healthy)

Each module owns:

```
Modules/{Module}/Infrastructure/
  Migrations/
    {timestamp}_{Name}.cs
    {timestamp}_{Name}.Designer.cs
    {Module}DbContextModelSnapshot.cs
```

Each DbContext registers **schema-scoped** history:

```csharp
npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "{schema}");
```

Schemas in use: `one`, `messaging`, `payments`, `crm`, `ops`, `billing`, `lhdn`, `commerce`, `communications`.

This per-schema history is correct for modular monolith multi-context on one Postgres database.

### 5.2 Inventory by module

| Module | Schema | Forward migrations (approx) | Notes |
|---|---|---|---|
| One | `one` | 6 | Initial → DropLegacySchemas → Outbox/Inbox retry → ApiCredentials → WebhookEnabledEvents → OrganizationExternalRef |
| Messaging | `messaging` | 3 | Initial → Outbox/Inbox → MessageDeliveryLogs |
| Payments | `payments` | 6 | Initial → RemoveAccountingOverrides → Outbox/Inbox → WebhookBusinessKey → ConfigIsActive → IntegrationCheckoutSessions |
| CRM | `crm` | 3 | Initial → Outbox/Inbox → ConsentDefaultFalse |
| Ops | `ops` | 2 | Initial → Outbox/Inbox (smallest chain) |
| Billing | `billing` | 5 | Initial → Profiles/Sequences → CreditHolds/Idempotency → Outbox/Inbox → SeparateReceiptAndConsolidation |
| Lhdn | `lhdn` | 4 | Initial → Outbox/Inbox → ApiKeyScopes/KeyHint → LegalAddress + InternalId index |
| Commerce | `commerce` | **14** | Longest chain; dunning engine evolved across several migrations |
| Communications | `communications` | 6 | Initial → Suppression → Broadcasts → TenantEmailConfig → **RemoveBroadcasts (credit columns)** → Outbox/Inbox |

**Totals:**
- ~49 forward migration classes
- ~49 Designer companions
- 9 ModelSnapshots
- ~107 C# files under Migrations folders

**Age window:** timestamps from `20260627*` (Initial*Schema bulk) through `20260807*` (One external ref, Payments integration checkout). Roughly **six weeks** of iterative schema evolution from a coordinated “initial” cut.

### 5.3 Notable migration content / hygiene smells

1. **Bulk Initial\*Schema (2026-06-27)**  
   All nine modules got initial migrations in the same minute window — evidence of `api:migrations:init` / purge-reset workflow used historically.

2. **One `DropLegacySchemas`** (`20260704104342_DropLegacySchemas.cs`)  
   Raw SQL: `DROP SCHEMA IF EXISTS community CASCADE; DROP SCHEMA IF EXISTS vault CASCADE;`. One-way; empty `Down()`. Appropriate for product pivot (ADR 022 community/vault removal) but permanent operational knowledge for any old DB restore.

3. **Communications AddBroadcasts → RemoveBroadcasts**  
   Credit hold columns added then dropped within days. Net effect on greenfield is noise in the chain (create columns then drop). Classic squash candidate for Communications only.

4. **Commerce dunning thrash**  
   `AddDunningEngine` → `RefactorDunningEngine` → `DunningEngineDayOffsetAndProgress` (+ charge attempt enrichment). Correct iterative delivery; largest long-term squash benefit.

5. **Cross-module outbox/inbox migration wave (`20260803*`)**  
   All modules received nearly simultaneous `AddOutboxInboxRetryAndDeadLetter`. Good consistency; also means any squash must keep retry/dead-letter columns.

6. **PendingModelChangesWarning ignored in tests**  
   Integration fixtures call `ConfigureWarnings(w => w.Ignore(PendingModelChangesWarning))`. Hides drift between model and last migration during tests — useful for green CI, dangerous if used to paper over forgotten migrations. Host `Program.cs` logs error on PendingModelChanges during boot migrate but continues for that context only.

### 5.4 How migrations are applied

#### A. Host boot (`Program.cs`)

On every API start, in order:

1. OneDbContext  
2. MessagingDbContext  
3. PaymentsDbContext  
4. CrmDbContext  
5. OpsDbContext  
6. BillingDbContext  
7. LhdnDbContext  
8. CommerceDbContext  
9. CommunicationsDbContext  

`MigrateAsync()` per context. PendingModelChanges → log error, continue. Other exceptions rethrow (boot fails).

**Implication:** Local/dev/prod empty databases self-heal without Taskfile. Taskfile migrate is for operators who want apply without starting the API (or before API is deployable).

#### B. Taskfile tasks (root `Taskfile.yml`)

| Task | Behavior |
|---|---|
| `api:db:migrate` | Sequential `dotnet ef database update` for all 9 contexts; `dir: apps/lazuar-api/src/Lazuar.Api` |
| `api:migrations:purge` | **`rm -rf` all Migrations folders** — destructive, dev-reset only |
| `api:migrations:init` | Adds `Initial{Module}Schema` for all 9 — only valid after purge on empty history |
| `api:migrations:add` | `MODULE` + `NAME` required; builds `{MODULE}DbContext` and path `Modules/{{.MODULE}}/Infrastructure/...` |

#### C. Taskfile hygiene issues

1. **`api:migrations:add` usage string is wrong:**  
   `Usage: task api:migrations:add MODULE=Tenant NAME=AddUsersTable`  
   There is no Tenant module. Correct examples: `MODULE=One`, `MODULE=Billing`, `MODULE=Commerce`. CRM is special-cased by folder name `CRM` vs context `CrmDbContext` — Taskfile uses `{{.MODULE}}DbContext`, so **`MODULE=CRM` produces `CRMDbContext` which is wrong** (actual type is `CrmDbContext`). Operators must use the casing that matches the C# context name prefix (`Crm`, not `CRM`). This is a footgun.

2. **`dev` task has migrate commented out** (`# - task: api:db:migrate`), relying on Program.cs auto-migrate. Fine if documented; currently easy to miss.

3. **`api:migrations:purge` has no confirmation prompt** — accidental run deletes all migration source. Acceptable for expert Taskfile but should be documented as “local only, never on shared branches with applied histories.”

4. **No Taskfile for `migrations remove` / `script` / `bundle`** — advanced ops use raw `dotnet ef`.

5. **No CI job that applies migrations to a fresh DB and asserts success for all 9 contexts** — only Commerce + Billing are migrated inside tests; other modules’ migration chains are only exercised when the API boots against a real database.

### 5.5 Integration tests vs real migrations

| Test | Uses real EF migrations? | Drift risk |
|---|---|---|
| `CommerceQueryServiceTests` | Yes (`MigrateAsync` Commerce) | Low for Commerce schema |
| `CreditDeductionConcurrencyTests` | Yes (Billing) | Low for Billing schema |
| `BillingQueryServiceTests` | **No** — hand-rolled `CREATE TABLE IF NOT EXISTS` for LedgerEntries/Lines | **High** — columns can diverge from EF model silently |
| ModuleTests InMemory | No migrations (InMemory ignores much of relational model) | Medium — unique indexes, xmin, filters not proven |

**Hygiene recommendation:** Either migrate Billing in `BillingQueryServiceTests` (like concurrency tests) or mark it clearly as “SQL contract test against approximate schema.” Prefer real migrate.

---

## 6. Do migrations need squashing long-term?

### 6.1 Verdict

| Horizon | Squash? | Rationale |
|---|---|---|
| **Now (Aug 2026)** | **No mandatory squash** | ~49 migrations, ~6 weeks old, all environments likely still under active development; host auto-migrate is fast enough |
| **When** | After **stable production baseline** + no multi-env history divergence (or with a planned baseline cutover) | |
| **Priority modules** | Commerce first, then Communications (add/remove noise), then Payments/One | |

### 6.2 When squashing becomes worth it

Trigger any of:
1. Commerce chain exceeds ~25–30 migrations or slow `MigrateAsync` on empty DB becomes noticeable in CI/boot.
2. Onboarding pain (“why 14 Commerce migrations for greenfield?”).
3. Designer/snapshot merge conflicts become frequent on parallel feature branches.
4. Desire for a **single baseline migration per schema** matching current ModelSnapshot for new environments only.

### 6.3 Squash strategy (when ready — modular monolith specific)

**Do not** run `api:migrations:purge` against databases that already applied intermediate migrations without a coordinated baseline.

Recommended approach:

1. **Freeze** schema changes for a short window.  
2. For each module independently (order does not matter if empty DB only):  
   - Ensure ModelSnapshot matches current model (`dotnet ef migrations has-pending-model-changes` or add empty migration and delete if empty).  
   - For **new environments only**: replace chain with one `Baseline{Module}Schema` equal to current snapshot.  
3. For **existing production/staging**:  
   - Either keep history forever (safest), **or**  
   - Apply a one-time ops procedure: insert single row into `{schema}."__EFMigrationsHistory"` for the new baseline after verifying schema already matches snapshot (no `Up()` re-run).  
4. Never purge history mid-flight on shared DBs.  
5. Keep raw SQL one-way migrations (`DropLegacySchemas`) documented in ops runbooks even after squash (knowledge still needed for old backups).

### 6.4 Non-squash hygiene (do now / soon)

- Add CI “migration smoke”: spin Postgres, run `api:db:migrate` or Program.cs migrator for **all 9** contexts, assert tables exist.  
- Fix `api:migrations:add` MODULE examples and CRM casing docs.  
- Stop ignoring PendingModelChanges in production boot without fail-closed option (dev may ignore; prod should fail).  
- Avoid further add-then-remove column churn without cleanup squash on that module when convenient.

---

## 7. Flaky / docker-dependent / ignored tests — documentation

### 7.1 Classification matrix

| Class | Tests | Default CI behavior | Local without Docker | Local without Postgres service |
|---|---|---|---|---|
| **Always unit** | Architecture, almost all ModuleTests, Billing domain, Ops LLM | Run | Run | Run |
| **Service Postgres soft-skip** | `BillingQueryServiceTests` | Runs (CI sets `LAZUAR_TEST_PG`) | Runs if `docker-compose up db` or env set | **Ignore** |
| **Testcontainers soft-skip** | `CreditDeductionConcurrencyTests` | Runs if Docker available on runner | **Ignore** if Docker down | N/A (brings own PG) |
| **Testcontainers hard-fail setup** | `CommerceQueryServiceTests` | Runs if Docker available | **Fails fixture** if Docker down | N/A |
| **Ignored fixture** | `LhdnSandboxE2ETests` | Skipped (`[Ignore]`) | Skipped | Skipped |
| **Dead code** | `UblStrategyTests` (all commented) | “Passes” as empty fixture | Same | Same |

### 7.2 Environment variables

| Variable | Used by | Purpose |
|---|---|---|
| `LAZUAR_TEST_PG` | `BillingQueryServiceTests` | Npgsql connection string; CI injects `Host=localhost;Port=5432;Database=lazuar_mvp;Username=postgres;Password=postgres;` |
| (implicit docker-compose defaults) | Same, fallback | `Host=localhost;Port=5432;Database=lazuar_mvp;...` |
| `LHDN_SANDBOX_CLIENT_ID` | Sandbox E2E (ignored) | Live MyInvois preprod |
| `LHDN_SANDBOX_CLIENT_SECRET` | Sandbox E2E | |
| `LHDN_KNOWN_SUBMISSION_UID` | Sandbox E2E status poll | Optional |

### 7.3 Docker / Testcontainers details

**Containers used:**
- `PostgreSqlBuilder` → database `lazuar_test` (Commerce smoke)  
- `PostgreSqlBuilder` → database `lazuar_credit_test` (credit concurrency)  

Both use username/password `postgres`/`postgres`. CS0618 warnings suppressed around builder API.

**CI reality (`.github/workflows/ci.yml`):**
- Provides a **service** Postgres for `LAZUAR_TEST_PG`.
- Does **not** document that Integration tests also start **additional** Testcontainers (second/third Postgres instances).
- Does **not** run `Modules.Ops.Tests`.
- Does not use `task api:test` (duplicates project list with drift).

**Flake vectors:**
1. **Docker daemon unavailable** → Commerce Testcontainers throws in `OneTimeSetUp` (red suite, not yellow).  
2. **Image pull rate limits / slow pull** → intermittent Testcontainers timeouts (possible flaky).  
3. **Hand-rolled DDL vs empty service DB** in BillingQueryServiceTests: first run creates tables; concurrent test jobs sharing one DB could collide on `OrganizationId` cleanup if parallelized (currently sequential steps mitigate).  
4. **InMemory vs Postgres behavioral differences** — not flaky, but false confidence (e.g. xmin concurrency only in CreditDeductionConcurrencyTests).  
5. **Reflection-based Ops tests** — brittle to private method renames (not flaky, but high maintenance).  
6. **Source-scan architecture tests** — false fail if formatting changes string match for filters; false pass if equivalent logic rewritten differently.

### 7.4 Recommended documentation block (for README / contributor docs)

Suggested text to add under `apps/lazuar-api/README.md` (not applied by this analysis):

```markdown
## Backend tests

task api:test   # all 5 projects

### Categories of dependency

1. Pure unit (no Docker): Architecture, ModuleTests (most), Modules.Billing.Tests, Modules.Ops.Tests
2. Needs Postgres connection string LAZUAR_TEST_PG (or docker-compose db):
   BillingQueryServiceTests — skips if unreachable
3. Needs Docker (Testcontainers):
   CommerceQueryServiceTests — fails setup without Docker
   CreditDeductionConcurrencyTests — skips without Docker
4. Opt-in live LHDN sandbox (always [Ignore] until credentials + manual un-ignore):
   LhdnSandboxE2ETests — env LHDN_SANDBOX_CLIENT_ID / SECRET

### CI

GitHub Actions `dotnet` job: service Postgres + LAZUAR_TEST_PG; runners provide Docker for Testcontainers.
Note: Modules.Ops.Tests currently missing from CI steps (Taskfile includes it).
```

### 7.5 Manual E2E (outside NUnit)

| Path | Role |
|---|---|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/scripts/lhdn_sandbox/*.sh` | Live LHDN preprod flows |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/postman/*` | Manual API collection |
| Host boot migrate | Implicit full migration smoke when API starts |

None of these are part of `task api:test`.

---

## 8. Detailed file-by-file notes (existing tests)

### 8.1 Architecture

**`/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/tests/Lazuar.ArchitectureTests/ModuleBoundaryTests.cs`**  
- Force-loads all module assemblies; fails hard if missing.  
- Outbox job naming convention enforced.  
- BuildingBlocks/SharedKernel purity rules (C.9).  

**`/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/tests/Lazuar.ArchitectureTests/TenantIsolationArchitectureTests.cs`**  
- Walks up directories to find source files (works from bin and repo root).  
- Middleware allowlist tests encode product decisions (public commerce, webhooks, Aura provision exempt).  

**`TestData/lhdn-golden-master.json`**  
- Orphan embedded resource; only referenced from csproj.

### 8.2 Integration

**`BillingDbContextTests.cs`**  
- High-value regression; documents real production footgun.  
- InMemory only — does not prove Postgres `xmin` or relational cascade.

**`BillingQueryServiceTests.cs`**  
- Business-meaningful assertion (TOPUP ignored in net revenue).  
- Soft-skip is correct for laptops.  
- **Schema drift risk** from ad-hoc DDL — top migration hygiene issue in the test suite.

**`CommerceQueryServiceTests.cs`**  
- Good migrate path; weak assertions.  
- **Hard Docker dependency** differs from soft-skip sibling — inconsistent policy.

**`CreditDeductionConcurrencyTests.cs`**  
- Best-practice model for money integration: Testcontainers + MigrateAsync + soft-skip + real concurrency.  
- Should be the template for future Postgres tests.

### 8.3 ModuleTests highlights

**Billing**  
- `LedgerBalanceMatrixTests` — table-driven payment shapes; core accounting guard.  
- `DeductTenantCreditIdempotencyTests` — sequential InMemory (complements Postgres concurrency suite).  
- `B2cConsolidationJobTests` — worker behavior without full host.

**Commerce**  
- `SubscriptionRecoveryTests` + `DunningCampaignDomainTests` + `GatewayPaymentFailed*` close the recovery loop at unit level.  
- **No DunningEngineJob tests** remains the largest Commerce gap.

**Payments**  
- `ProcessGatewayWebhookCommandHandlerTests` — primary inbound money pipeline unit suite.  
- Integration checkout suite is substantial (create, secrets, outbound webhooks, auth metadata).

**One**  
- `ApiKeyAuthenticationTests` + credentials + outbound webhooks cover developer platform surfaces.  
- `ProvisionAuraWorkspaceTests` is large and valuable for second-app provisioning.  
- Cookie session auth still untested.

**Lhdn**  
- Practical submit/credit/secret/claim coverage.  
- UBL + sandbox still non-CI.

**TenantIsolation**  
- Handler/IDOR + filter hardening tests encode Phase C security work.  
- Complement architecture source guards with behavioral checks.

### 8.4 Modules.Billing.Tests / Modules.Ops.Tests

- Billing pure domain suite remains highest quality pure money invariants.  
- Ops suite is intentionally narrow; missing from CI is the main hygiene defect.

---

## 9. Recommendations (hygiene-only priority order)

### P0 — Runner consistency (cheap)

1. Add `Modules.Ops.Tests` to `.github/workflows/ci.yml` `dotnet` job (match Taskfile).  
2. Prefer CI invoking `task api:test` **or** single `dotnet test Lazuar.slnx` to prevent future drift.  
3. Document Docker vs `LAZUAR_TEST_PG` vs Sandbox in `apps/lazuar-api/README.md`.

### P0 — Integration policy consistency

4. Make `CommerceQueryServiceTests` soft-skip like `CreditDeductionConcurrencyTests` **or** mark with `[Category("RequiresDocker")]` and document.  
5. Migrate real Billing schema in `BillingQueryServiceTests` (drop hand-rolled DDL) to eliminate drift.

### P1 — Shared fixtures

6. Introduce `Lazuar.TestSupport` for InMemory DbContext factory + Testcontainers fixture + endpoint auth assert.  
7. Standardize NUnit Categories: `Unit`, `Integration`, `RequiresDocker`, `Sandbox`.  
8. Decide home for Billing domain tests (`Modules.Billing.Tests` only) and document in ModuleTests README comment.

### P1 — Dead test assets

9. Restore UBL golden tests **or** delete `UblStrategyTests` body + Architecture golden master together.  
10. Rename `LhdnRateLimitingTests` / `CommerceQueryServiceTests` to match assertions.  
11. Convert `LhdnSandboxE2ETests` from permanent `[Ignore]` to `[Category("Sandbox")]` so filter-based opt-in is possible without source edits.

### P1 — Coverage gaps that are hygiene for confidence (not new features)

12. `DunningEngineJob` pure policy extraction + unit tests (largest remaining revenue path).  
13. One register/login/password domain or handler tests.  
14. Migration smoke job for all 9 contexts on fresh Postgres in CI.  
15. Stripe (and other) gateway adapter signature unit tests.

### P2 — Migration long-term

16. Do **not** squash yet.  
17. When squashing: Commerce → Communications → others; baseline cutover runbook; never purge applied shared DBs.  
18. Fix Taskfile `api:migrations:add` usage string and document CRM → `MODULE=Crm` casing.  
19. Consider failing production boot on PendingModelChanges instead of continue-only log.

### P2 — Architecture expansion

20. Contracts purity + no foreign DbContext references.  
21. Optional NetArchTest rule: every `*OutboxPublisherJob` registered as `IHostedService` (Lhdn already has DI smoke; generalize).

---

## 10. Absolute path index

### Test projects

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/tests/Lazuar.ArchitectureTests/`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/tests/Lazuar.IntegrationTests/`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/tests/Lazuar.ModuleTests/`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/tests/Modules.Billing.Tests/`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/tests/Modules.Ops.Tests/`

### Migration roots

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/Migrations/`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Messaging/Infrastructure/Migrations/`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Infrastructure/Migrations/`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/CRM/Infrastructure/Migrations/`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Ops/Infrastructure/Migrations/`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Billing/Infrastructure/Migrations/`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Lhdn/Infrastructure/Migrations/`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/Migrations/`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Communications/Infrastructure/Migrations/`

### Task / CI / host

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/Taskfile.yml` (`api:test`, `api:db:migrate`, `api:migrations:*`)
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/.github/workflows/ci.yml`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/src/Lazuar.Api/Program.cs` (boot migrate loop)
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Lazuar.slnx`

### Related docs

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/001-gaps/16-testing-coverage.md` (stale snapshot)
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/001-backend/001-backend-solidification-checklist.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/scripts/lhdn_sandbox/`

---

## 11. Bottom line

Lazuar Pay’s modular monolith now has a **real multi-layer test skeleton** that is far denser than the August gap analysis: architecture is fail-closed and assembly-anchored; money paths have domain + handler + (partial) Postgres concurrency; payments webhooks and integration checkout are unit-covered; tenant isolation has both architecture and behavioral tests.

Hygiene debt is concentrated in **process consistency** (CI vs Taskfile Ops omission), **fixture proliferation** (no TestSupport library, mixed Docker skip policies), **dead LHDN golden assets**, **BillingQueryService hand-rolled SQL**, and **migration growth without a baseline plan**. Migrations do **not** need squashing today; Commerce’s 14-step chain is the first candidate when production schemas stabilize.

Highest-leverage next hygiene moves: **(1)** fix CI Ops inclusion, **(2)** unify Docker skip policy + document, **(3)** real-migrate BillingQueryServiceTests, **(4)** shared fixtures, **(5)** DunningEngineJob tests, **(6)** all-schema migration smoke in CI.
