<!-- Source subagent: 019fc650-3514-7d71-af79-0651ebeba954 -->
<!-- Full uncondensed subagent analysis — do not summarize -->

# Testing Gap Analysis

**Scope:** `/Users/akmalfirdaus/Code/lazuar/lazuar-hub`  
**Primary focus:** `apps/lazuar-api/tests/` + frontend apps + critical money/auth paths  
**Date context:** 2026-08-03

---

## Executive snapshot

| Layer | Projects | Approx. active tests | Run by `task api:test`? | Overall depth |
|---|---|---:|---|---|
| Architecture | 1 | 3 | Yes | Thin; risk of vacuous pass |
| Integration | 1 | 3 | Yes | Narrow; mixed infra |
| Module (shared) | 1 | ~20 real + many commented/ignored | **No** | Patchy |
| Billing domain unit | 1 | ~20 | **No** | Good for 2 aggregates only |
| Ops unit | 1 | 4 | **No** | Single private method |
| Frontend (all apps) | 0 | 0 | N/A | **None** |
| API host / E2E | 0 | 0 (sandbox scripts only) | N/A | **None** |

**Rough production vs tests:** modular API has on the order of **~450+** production `.cs` files across BuildingBlocks + 9 modules + host; tests are **~15** source files and **~50–60** executable test methods. Coverage is **hotspot-driven** (credits wallet, one LHDN submit path, one Ops reflection test), not systematic.

---

## Test Project Inventory

### 1. `apps/lazuar-api/tests/Lazuar.ArchitectureTests/`
| Item | Detail |
|---|---|
| Project | `Lazuar.ArchitectureTests.csproj` |
| Framework | NUnit + NetArchTest.Rules + FluentAssertions + NSubstitute |
| Source | `ModuleBoundaryTests.cs` |
| Assets | `TestData/lhdn-golden-master.json` (embedded, **unused by any test**) |
| Refs | Only `Lazuar.Api` |
| In solution | Yes (`Lazuar.slnx`) |
| In `task api:test` | Yes |

### 2. `apps/lazuar-api/tests/Lazuar.IntegrationTests/`
| Item | Detail |
|---|---|
| Project | `Lazuar.IntegrationTests.csproj` |
| Framework | NUnit + EF InMemory + Testcontainers.PostgreSql + FluentAssertions + NSubstitute |
| Sources | `BillingDbContextTests.cs`, `BillingQueryServiceTests.cs`, `CommerceQueryServiceTests.cs` |
| Refs | Billing.Infrastructure, Commerce.Infrastructure, BuildingBlocks.Infrastructure |
| In `task api:test` | Yes |

### 3. `apps/lazuar-api/tests/Lazuar.ModuleTests/`
| Item | Detail |
|---|---|
| Project | `Lazuar.ModuleTests.csproj` |
| Areas | Billing event handlers, Communications domain, Lhdn handler + sandbox + UBL strategies |
| In solution | Yes |
| In `task api:test` | **No** (major CI gap) |

### 4. `apps/lazuar-api/tests/Modules.Billing.Tests/`
| Item | Detail |
|---|---|
| Domain unit tests | `CreditHoldTests.cs`, `TenantCreditBalanceTests.cs` |
| In `task api:test` | **No** |

### 5. `apps/lazuar-api/tests/Modules.Ops.Tests/`
| Item | Detail |
|---|---|
| Source | `Services/LlmOrchestratorServiceTests.cs` |
| Style | Reflects private `ExecuteReadToolAsync` |
| In `task api:test` | **No** |

### 6. Frontend / packages / scripts (not .NET test projects)
| Area | Tests? |
|---|---|
| `apps/ops-page` | No `test` script; no vitest/jest/playwright |
| `apps/portal-page` | No test tooling |
| `apps/superadmin-page` | No test tooling |
| `apps/developers-page` | No tests |
| `packages/*` | No unit tests for SDKs / api-types |
| `scripts/lhdn_sandbox/*.sh` | Manual/ops sandbox E2E scripts (not CI unit tests) |
| `docs/postman/` | Manual API collection only |

### Runner matrix (important)

| Entry point | What actually runs |
|---|---|
| `task api:test` | Architecture + Integration **only** |
| `apps/lazuar-api/package.json` → `dotnet test` | Entire solution discovery (if invoked from api dir / slnx) — **broader than Taskfile** |
| Root `pnpm test` / turbo `test` | Only packages that define `test` (API yes; frontends no) |

**Inconsistency:** Taskfile under-tests relative to `dotnet test` / turbo.

---

## What Is Covered (by module/area)

### Billing — **best relative coverage, still incomplete**

| Surface | Coverage |
|---|---|
| `TenantCreditBalance` domain | Strong pure unit: TopUp, Deduct (happy + exhaust + insufficient + zero + non-positive + ledger append), Clawback (clamp / partial / no-op) |
| `CreditHold` domain | Strong pure unit: construct, consume, settle/release, double-release, invalid amounts/correlation |
| `BillingDbContext` child append | One InMemory test: TopUp ledger on existing aggregate across change-tracker clear (EF child-entity concurrency class bug regression) |
| Financial summary SQL | One Postgres-backed test: net revenue ignores operational TOPUP expense |
| `GatewayPaymentCompletedHandler` | One ordering test (B2C): sequence → SaveChanges → document gen |
| `ManualSubscriberEnrolledIntegrationEventHandler` | Same ordering pattern |
| **Not covered** | Ledger double-entry math, all other event handlers, credit command handlers, holds orchestration, jobs, documents, sequences, profiles |

### Commerce — **almost none**

| Surface | Coverage |
|---|---|
| Dapper vs EF schema smoke | `CommerceQueryServiceTests` only asserts queries **do not throw** on empty schema (products, coupons, subscribers, transactions, **dunning campaigns**, stats, portal, custom checkouts) |
| Domain (`Subscription`, `DunningCampaign`, `Coupon`, `Product`, `Order`, `CheckoutSession`) | **Zero** unit tests |
| Command handlers (checkout, dunning CRUD, coupons, manual subscriber) | **Zero** |
| `DunningEngineJob`, `BillingEngineJob` | **Zero** |
| Payment lifecycle handlers in Commerce | **Zero** |

### Payments — **zero automated tests**

| Surface | Coverage |
|---|---|
| `ProcessGatewayWebhookCommandHandler` | None (signature verify, idempotency log, event publish, early returns) |
| Gateway adapters (Stripe, Billplz, CHIP, Razorpay) | None |
| Refund / off-session charge handlers | None |
| `PaymentWebhookLog` uniqueness/idempotency | None (docs exist: `docs/006-payment-webhook-idempotency-backfilling.md`) |

### One (auth / workspaces / outbound webhooks) — **zero**

| Surface | Coverage |
|---|---|
| Login / register / cookie JWT | None |
| Password reset / verify email / change password | None |
| Workspace invite / accept / membership | None |
| Tenant webhook endpoints + `OutboundWebhookDispatcherJob` | None |
| Password hashing upgrade-on-login (see `docs/008-...`) | None |

### Lhdn — **partial, fragile**

| Surface | Coverage |
|---|---|
| `SubmitTaxDocumentCommandHandler` “happy path save” | One mocked test (file name `LhdnRateLimitingTests` is **misnamed** — no rate-limit assertions) |
| Sandbox token + document status | Present but **`[Ignore]`** and env-credential gated |
| UBL strategy golden tests | **Entire file commented out** |
| Embedded golden master JSON in ArchitectureTests | **Dead asset** (never read) |
| Validator / XSD / signing / cancel / self-billed | Scripts only (`scripts/lhdn_sandbox/`) |

### Communications — **domain constructors only**

| Surface | Coverage |
|---|---|
| `Broadcast` lifecycle (draft → queue → send → complete/fail) | Good pure unit |
| `SuppressionEntry` email normalize/trim/validation | Good pure unit |
| Broadcast command handlers, fanout job, Resend webhook, templates, credit holds integration | **None** |

### Ops — **narrow reflection unit**

| Surface | Coverage |
|---|---|
| `LlmOrchestratorService.ExecuteReadToolAsync` (private) | Empty JSON, malformed JSON, valid JSON + tenant inject, mediator exception → error string |
| Tool registry, write tools, streaming, auth of agent tools | **None** |

### CRM / Messaging / BuildingBlocks / Host middleware — **none**

No dedicated tests for:
- CRM resolve/create/anonymize client profile  
- Messaging dispatch / tenant seeding  
- Outbox/inbox jobs  
- `ApiKeyAuthenticationMiddleware`, `TenantSecurityMiddleware`  
- `PasswordService`, JWT cookie issuance  

### Architecture boundaries — **present but shallow**

Three NetArchTest rules:
1. Domain isolated from other modules + own Application/Infrastructure  
2. Application must not reference own Infrastructure  
3. Application/Infrastructure may only touch other modules via Contracts (not Domain/App/Infra)

Gaps in architecture suite itself:
- Silent skip if assembly not loaded (`if (domainAssembly == null) continue`)  
- No rules for Contracts purity, SharedKernel/BuildingBlocks misuse, cyclic Contracts, Infrastructure→Application direction, handlers in wrong layer  
- No assembly loading strategy (only `GetAssemblies()` already loaded)  
- No dependency on each module Domain/Application project to force load  

---

## Critical Paths Without Tests

### 1. Dunning (Commerce) — **critical gap**

**Production surfaces with no tests:**
- Domain: `DunningCampaign` (steps, final action, recovery/churn metrics, archive/restore, targeting)  
- Domain: `Subscription` dunning state machine (`AssignDunningCampaign`, `AdvanceDunningStep`, `PauseDunning` / `ResumeDunning` / `ClearDunning`, `MarkAsPastDue`, `Suspend`, `Resume`, arrears-aware `Activate`)  
- App: `DunningCampaignCommandHandlers`, `ManageSubscriberDunningCommandHandlers`  
- Worker: `DunningEngineJob` (campaign matching, pre-dunning windows, step actions, final action, event publishing)  
- Cross-module recovery path in `GatewayPaymentCompletedIntegrationEventHandler` (`RecordRecovery` when recovering from PAST_DUE/SUSPENDED)  
- Reminder dispatch logs / pre-dunning EMAIL steps  

**Risk:** Incorrect day offsets, campaign priority, payment-method inference (`MANUAL` vs `ONLINE_GATEWAY`), double-advance of steps, failure to clear dunning on recovery → **revenue loss / wrongful suspension**.

**Integration touch only:** empty-schema `GetDunningCampaignsAsync` smoke.

---

### 2. Webhooks — **critical gap**

| Path | Test? |
|---|---|
| **Inbound payment webhooks** `ProcessGatewayWebhookCommandHandler` | **No** |
| Signature verification failure | No |
| Unknown event type early return | No |
| Idempotent re-delivery via `PaymentWebhookLog` | No |
| `PAYMENT_COMPLETED` → `GatewayPaymentCompletedIntegrationEvent` | No |
| `DISPUTE_CREATED` → dispute event | No |
| Adapter parsing (Stripe/Billplz metadata reconstruction) | No |
| **Outbound tenant webhooks** (`TenantWebhookEndpoint`, delivery outbox, dispatcher job) | No |
| **Resend email compliance webhook** signature optional-in-dev behavior | No |
| **LHDN webhook subscriptions** | No |

Documented operational risk (`006-payment-webhook-idempotency`) is **not encoded as tests**.

---

### 3. Auth (One + host) — **critical gap**

| Path | Test? |
|---|---|
| `/one/public/register` + workspace creation | No |
| `/one/auth/login` (cookie JWT) | No |
| Invalid credentials / inactive user | No |
| Logout cookie clear | No |
| Forgot / reset password token hash + expiry | No |
| Email verification / resend | No |
| Change password + security stamp rotation | No |
| Password hash compatibility upgrade on login | No |
| Workspace role isolation / invite accept | No |
| `ApiKeyAuthenticationMiddleware` (LHDN developer keys, cache, revocation) | No |
| Super-admin vs client role claims | No |
| Tenant security middleware (API key vs user org binding) | No |

**Risk:** auth regressions are only found manually; multi-tenant isolation is untested end-to-end.

---

### 4. Credits — **domain good; orchestration thin**

| Path | Covered? |
|---|---|
| Wallet `TopUp` / `Deduct` / `Clawback` pure domain | Yes (unit) |
| Hold pure domain | Yes (unit) |
| `ReserveCreditsCommandHandler` (wallet deduct + hold create + concurrency retry) | **No** |
| `ConsumeCreditHoldCommandHandler` | **No** |
| `ReleaseCreditHoldCommandHandler` (top-up remainder + concurrency) | **No** |
| `DeductTenantCreditCommandHandler` (idempotency log + concurrency retry) | **No** |
| `ClawbackCreditsCommandHandler` | **No** |
| `PlatformTopUpEventHandler` package selection + ledger TOPUP | **No** |
| `ChargebackClawbackHandler` package recompute | **No** |
| `ApiCreditPurchasedHandler` / `StarterCreditSeederHandler` | **No** |
| LHDN submit credit sufficiency gate | Only stubbed true in submit test |
| Communications broadcast reserve/consume holds | **No** |
| Concurrent deductions under `xmin` row version | **No** (InMemory cannot prove Postgres xmin) |

**Risk:** money/credit correctness depends on untested app-layer composition; domain purity alone will not catch double-spend under races or missing idempotency keys.

---

### 5. Ledger (Billing) — **critical accounting gap**

| Path | Covered? |
|---|---|
| `LedgerEntry.ValidateBalanced()` | **No direct unit tests** |
| Line construction / FX / tax / fee composition in handlers | Only partially exercised by handler order mocks (no line assertions) |
| `GatewayPaymentCompletedHandler` B2B vs B2C, tax/fee/net balancing | **No** (order-only for B2C) |
| Refund / cancellation reversing entries | **No** |
| Zero-amount checkout ledger | **No** |
| Manual enrollment ledger | Order-only |
| Commission / invoice issued / deferred revenue + `RevenueRecognitionJob` | **No** |
| B2C consolidation job | **No** |
| Sequence numbers + PDF document generation | **No** |
| Financial summary SQL | One case |
| LHDN status updates on ledger rows | **No** |

**Risk:** double-entry bugs (unbalanced entries) or wrong account types can ship; production handlers call `ValidateBalanced()`, but there is no property-based or table-driven suite of payment shapes (tax-only, fee-only, FX ≠ 1, refunds).

---

## Architecture Boundary Tests

### What exists

`ModuleBoundaryTests` checks modular monolith layering for:

`One`, `Messaging`, `CRM`, `Payments`, `Ops`, `Billing`, `Lhdn`, `Commerce`, `Communications`

Three tests (domain isolation, app↛infra, cross-module only via Contracts).

### Gaps / reliability issues

1. **Silent no-op:** missing assemblies are skipped → green CI without evaluating unloaded modules.  
2. **No forced assembly load:** no `typeof(Modules.X.Domain....).Assembly` pins.  
3. **Dead fixture:** `lhdn-golden-master.json` embedded in Architecture project but unused.  
4. **Missing rule families:**
   - Domain may not depend on BuildingBlocks.Infrastructure / EF / MediatR  
   - Contracts must not depend on Infrastructure/Application  
   - Host (`Lazuar.Api`) may reference Infrastructure entrypoints only via DI registration patterns  
   - No cycles between Contracts assemblies  
   - Handlers must live in Application or Infrastructure (not Domain)  
   - No reference to other modules’ EF DbContexts  
5. **No test for “SharedKernel vs BuildingBlocks” ADR** (`docs/002-...`).  
6. **NetArchTest does not replace module-level design tests** for event contracts / outbox conventions.

---

## Integration/E2E Gaps

### Existing integration style (and limits)

| Test | Style | Limitation |
|---|---|---|
| `BillingDbContextTests` | EF **InMemory** | Does not prove Postgres xmin concurrency, filters, unique indexes |
| `BillingQueryServiceTests` | Live Postgres or `Assert.Ignore` | Manual schema DDL; not EF migrations; not full app host |
| `CommerceQueryServiceTests` | **Testcontainers** + migrate | Asserts “no throw” only; no data seeding / correctness |
| Lhdn sandbox | Live HTTP | Ignored by default |
| Shell sandbox scripts | Manual | Not wired to CI |

### Missing integration classes

There is **no**:
- `WebApplicationFactory<Program>` API host suite  
- Authenticated request pipeline tests  
- Full payment webhook HTTP → Payments → outbox → Billing/Commerce inbox chain  
- Multi-module transactional import protocol tests (`docs/004-...`)  
- Tenant isolation / query-filter tests (`docs/005-...`)  
- Outbox publisher / inbox consumer correctness under failure/retry  
- Concurrent credit deduct under real Postgres  
- Commerce checkout → gateway mock → subscription activate → ledger entry  
- Dunning engine time-travel integration  
- Refund / dispute end-to-end  
- Migrations smoke for **all** module DbContexts (only Commerce migrates in tests)

### Manual-only E2E

`scripts/lhdn_sandbox/run_all.sh` (+ provision, B2B, credit note, B2C, cert, cancel, self-billed) is the closest full E2E; **not automated in `task api:test`**.

Postman collection exists under `docs/postman/` — also manual.

---

## Frontend Test Gaps

| App | Stack | Test runner | Component tests | E2E | Notes |
|---|---|---|---|---|---|
| `ops-page` | Vite/React | **None** | None | None | Large module UI (billing/commerce/comms/etc.) |
| `portal-page` | Next.js | **None** | None | None | Customer portal flows untested |
| `superadmin-page` | Vite/React | **None** | None | None | Platform admin untested |
| `developers-page` | Next.js | **None** | None | None | OpenAPI docs only |

No `@testing-library`, Vitest, Jest, Playwright, or Cypress dependencies found.

**High-value untested UI risks:** checkout UX, dunning campaign builder, credit top-up, auth cookie flows, API key management, multi-tenant workspace switcher, LLM chat tool UX.

---

## Recommendations for Solidifying Backend

### P0 — Make existing tests actually run in CI

1. Expand `task api:test` to:
   - `Lazuar.ModuleTests`
   - `Modules.Billing.Tests`
   - `Modules.Ops.Tests`
2. Prefer single command: `dotnet test Lazuar.slnx` (with filters for optional live sandbox).  
3. Fix architecture suite to **fail if expected assemblies are missing**, not `continue`.  
4. Delete or wire the unused golden master; either restore UBL golden tests or remove dead assets.

### P0 — Money paths (ledger + webhooks + credits)

1. **Table-driven unit tests for `LedgerEntry.ValidateBalanced`** and each handler’s line composition:
   - Gateway payment (fee+tax, fee-only, tax-only, FX)
   - Platform top-up expense shape
   - Refunds / cancellations (reverse lines)
2. **Unit tests for `ProcessGatewayWebhookCommandHandler`** with fakes:
   - bad signature → throw  
   - duplicate EventId → no second publish  
   - DISPUTE vs PAYMENT routing  
3. **Postgres integration for credits:**
   - concurrent `DeductTenantCredit` with same idempotency key → single deduction  
   - concurrent reserve holds cannot overdraw (`xmin`)  
   - release returns remainder to wallet  
4. **Chargeback package parity:** same package selection for top-up and clawback.

### P0 — Dunning state machine

1. Pure domain tests for `Subscription` status transitions + dunning indices.  
2. `DunningCampaign` matching helpers (product IDs, payment methods, priority).  
3. Extract pure `DunningEngine` policy from `DunningEngineJob` for unit testing; keep job as thin host.  
4. Integration: seed PAST_DUE sub + campaign → advance clock → assert step + event + final suspend/cancel.

### P1 — Auth & tenancy

1. Domain tests: `GlobalUser` verify/reset/password stamp.  
2. Handler tests: register uniqueness, reset token expiry, invite accept.  
3. Middleware tests: API key hash lookup, revoked key cache eviction, JWT-from-cookie.  
4. Host tests: unauthorized cross-tenant access returns 401/403.

### P1 — Commerce core

1. `Coupon` reserve/confirm/release domain events and max-use races.  
2. `CheckoutSession` / zero-amount path.  
3. Payment completed handler: first activation vs recovery from arrears vs one_time products.

### P1 — Lhdn

1. Uncomment / regenerate UBL golden masters (or assert structural XPath invariants).  
2. Rename/expand rate limiting / credit gate tests (insufficient credits, test mode, idempotency).  
3. Keep sandbox tests optional (`[Category("Sandbox")]` + filter), not permanently `[Ignore]` with no alternative.

### P2 — Integration harness

1. Shared `LazuarApiFactory` + Testcontainers Postgres running **all** module migrations.  
2. Outbox “process until empty” helper for multi-module flows.  
3. Contract tests: TypeSpec OpenAPI response shapes for critical routes.  
4. Architecture expansion: layer + dependency allowlists committed as tests.

### P2 — Process / hygiene

1. Align Taskfile, turbo, and `dotnet test`.  
2. Track coverage with Coverlet thresholds on Billing/Payments/Commerce first (start ~40% on domain, grow).  
3. Tag flaky external tests; never block PR on LHDN sandbox without secrets.  
4. Prefer NUnit + FluentAssertions consistency (already established).

### Suggested near-term file plan

```
tests/
  Modules.Billing.Tests/
    LedgerEntryBalanceTests.cs          # NEW
    DeductTenantCreditHandlerTests.cs   # NEW (InMemory + concurrency later Postgres)
    CreditHoldCommandHandlerTests.cs    # NEW
    PlatformTopUpEventHandlerTests.cs   # NEW
  Modules.Payments.Tests/               # NEW project
    ProcessGatewayWebhookHandlerTests.cs
    PaymentWebhookLogIdempotencyTests.cs
  Modules.Commerce.Tests/               # NEW project
    SubscriptionLifecycleTests.cs
    DunningCampaignTests.cs
    CouponDomainTests.cs
    DunningEnginePolicyTests.cs
  Modules.One.Tests/                    # NEW project
    GlobalUserAuthDomainTests.cs
  Lazuar.IntegrationTests/
    CreditsConcurrencyPostgresTests.cs  # NEW
    PaymentWebhookEndToEndTests.cs      # NEW (factory + mock gateway)
    TenantIsolationTests.cs             # NEW
```

---

## File-by-File Notes on Existing Tests

### Architecture

#### `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/tests/Lazuar.ArchitectureTests/ModuleBoundaryTests.cs`
- **Purpose:** Modular boundary enforcement via NetArchTest.  
- **Strengths:** Correct modular monolith intent; covers all current modules including Communications.  
- **Weaknesses:** Vacuous pass if assembly not loaded; no load pins; no BuildingBlocks rules; empty line in namespace list is cosmetic only.  
- **Asset issue:** Project embeds golden master not used here.

#### `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/tests/Lazuar.ArchitectureTests/TestData/lhdn-golden-master.json`
- Pre-hashed UBL JSON + expected base64/hex digests.  
- **Orphan:** no C# test loads this resource (grep only finds csproj embed).

#### `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/tests/Lazuar.ArchitectureTests/Lazuar.ArchitectureTests.csproj`
- Solid package set; only references API host (transitive module load is fragile).

---

### Integration

#### `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/tests/Lazuar.IntegrationTests/BillingDbContextTests.cs`
- **Regression test** for EF behavior when child `CreditLedger` has pre-assigned Guid and parent was loaded in a new request.  
- Uses InMemory + NSubstitute execution context/mediator/job trigger.  
- **Good:** documents a real production footgun.  
- **Missing:** Postgres equivalent; concurrent SaveChanges; query filters when wrong tenant.

#### `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/tests/Lazuar.IntegrationTests/BillingQueryServiceTests.cs`
- Seeds SALE + TOPUP ledger rows; asserts gross/fees/tax/net and that software expense is ignored.  
- Soft-skips if Postgres down (`Assert.Ignore`).  
- Hand-rolled DDL (not migrations) — can drift from real schema.  
- Deletes by `OrganizationId` (lines may orphan if FK not cascade — depends on DDL; no FK declared in ad-hoc DDL).  
- **Only** financial summary path tested; rest of `BillingQueryService` untested.

#### `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/tests/Lazuar.IntegrationTests/CommerceQueryServiceTests.cs`
- Strong setup: Testcontainers + `MigrateAsync` + real Dapper connection factory.  
- Weak assertions: only `DoesNotThrowAsync` on empty org.  
- Does **not** validate row mapping, filters, pagination, dunning content, or portal token behavior.  
- Name suggests broad coverage; reality is **schema smoke**.

#### `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/tests/Lazuar.IntegrationTests/Lazuar.IntegrationTests.csproj`
- Has Testcontainers + InMemory; good foundation for expansion.

---

### Module tests — Billing handlers

#### `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/tests/Lazuar.ModuleTests/Billing/EventHandlers/GatewayPaymentCompletedHandlerTests.cs`
- Verifies **ordering** only for B2C receipt path (sequence before save before document).  
- Does **not** assert ledger lines, `ValidateBalanced`, idempotent early return, B2B skip of receipt, tax/fee math, metadata parsing.  
- Uses substitute repository — no EF.

#### `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/tests/Lazuar.ModuleTests/Billing/EventHandlers/ManualSubscriberEnrolledHandlerTests.cs`
- Same ordering pattern for manual enrollment.  
- Same missing accounting assertions.

---

### Module tests — Communications

#### `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/tests/Lazuar.ModuleTests/Communications/BroadcastTests.cs`
- Solid pure domain lifecycle.  
- Gaps: no credits fields (if still on aggregate vs removed migrations), no interaction with hold commands, no fanout job.

#### `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/tests/Lazuar.ModuleTests/Communications/SuppressionEntryTests.cs`
- Good validation/normalization coverage for constructor.  
- No persistence uniqueness (email+org) tests.

---

### Module tests — Lhdn

#### `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/tests/Lazuar.ModuleTests/Lhdn/LhdnRateLimitingTests.cs`
- **Misnamed:** tests successful document save with mocks, not rate limiting.  
- Credits always sufficient; validator unused beyond DI; strategy returns trivial XML.  
- Does not cover insufficient credits, missing tenant config, idempotency keys, validation failure.

#### `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/tests/Lazuar.ModuleTests/Lhdn/LhdnSandboxE2ETests.cs`
- Live preprod MyInvois; entire fixture `[Ignore]`.  
- Throws in Setup if env vars missing (even if not ignored in future).  
- Useful for local credential validation only.

#### `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/tests/Lazuar.ModuleTests/Lhdn/Strategies/UblStrategyTests.cs`
- **All real tests commented out** (standard invoice, credit note, consolidated).  
- File is effectively an empty fixture — false sense of coverage if counted by path.  
- Historical golden XML snapshots still valuable if re-enabled with normalization helpers.

#### `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/tests/Lazuar.ModuleTests/Lazuar.ModuleTests.csproj`
- References Lhdn full stack + Billing app/infra + Communications Domain + Commerce/Payments Contracts.  
- No Commerce Domain/Application — cannot host commerce unit tests here without expanding refs (or new project).

---

### Billing unit project

#### `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/tests/Modules.Billing.Tests/TenantCreditBalanceTests.cs`
- Highest-quality pure domain suite in the repo for money-like invariants.  
- Missing: TopUp non-positive throw, reference empty, multi-tenant id immutability, concurrent simulation (needs integration).

#### `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/tests/Modules.Billing.Tests/CreditHoldTests.cs`
- Excellent edge cases for hold lifecycle.  
- Missing: interaction with wallet (by design domain-separated — must be covered in command handlers).

#### `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/tests/Modules.Billing.Tests/Modules.Billing.Tests.csproj`
- References Infrastructure (heavier than needed for pure Domain tests).  
- **Not** in `task api:test`.

---

### Ops unit project

#### `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/tests/Modules.Ops.Tests/Services/LlmOrchestratorServiceTests.cs`
- Uses reflection on private method — brittle to renames.  
- Valuable for tenant injection safety (security-sensitive).  
- Does not test full agent loop, tool allowlists, write tools, streaming, prompt providers.  
- Dummy query type is local to test — good isolation.

---

### Non-project test-like artifacts

| Path | Role |
|---|---|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/scripts/lhdn_sandbox/*.sh` | Manual LHDN sandbox E2E |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/docs/postman/*` | Manual API regression aid |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/docs/006-payment-webhook-idempotency-backfilling.md` | Ops playbook, not tests |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/Taskfile.yml` `api:test` | **Incomplete** runner |

---

## Coverage heat map (qualitative)

| Module / area | Domain unit | App/handler unit | Integration | E2E |
|---|---|---|---|---|
| Billing credits wallet/hold | ●●●●○ | ○○○○○ | ●○○○○ | ○○○○○ |
| Billing ledger/accounting | ○○○○○ | ●○○○○ | ●○○○○ | ○○○○○ |
| Billing jobs/docs | ○○○○○ | ○○○○○ | ○○○○○ | ○○○○○ |
| Commerce dunning | ○○○○○ | ○○○○○ | ●○○○○ (smoke) | ○○○○○ |
| Commerce checkout/subs | ○○○○○ | ○○○○○ | ●○○○○ (smoke) | ○○○○○ |
| Payments webhooks/gateways | ○○○○○ | ○○○○○ | ○○○○○ | ○○○○○ |
| One auth/workspaces | ○○○○○ | ○○○○○ | ○○○○○ | ○○○○○ |
| Lhdn submit/UBL | ○○○○○ | ●○○○○ | ○○○○○ | ●○○○○ (manual/ignored) |
| Communications | ●●○○○ | ○○○○○ | ○○○○○ | ○○○○○ |
| Ops LLM | ○○○○○ | ●●○○○ | ○○○○○ | ○○○○○ |
| CRM / Messaging | ○○○○○ | ○○○○○ | ○○○○○ | ○○○○○ |
| Architecture | n/a | n/a | ●●○○○ | n/a |
| Frontend apps | ○○○○○ | ○○○○○ | ○○○○○ | ○○○○○ |

---

## Bottom line

Lazuar Hub has a **modular test skeleton** and a few **high-value regression tests** (credit domain, EF child-entity save, financial summary SQL, B2C ledger handler ordering, Ops tenant injection, NetArchTest boundaries). Relative to the product surface—**dunning engine, payment webhooks, auth, credit orchestration, double-entry ledger, multi-module outbox, and all frontends**—automated protection is **thin to absent**.

The single highest-leverage fixes are:

1. **Run all test projects in CI** (Taskfile currently omits three of five).  
2. **Harden money paths** (webhook idempotency + ledger balance + concurrent credits on Postgres).  
3. **Add dunning/subscription pure domain + engine policy tests.**  
4. **Add auth/API-key pipeline tests.**  
5. **Stop counting dead/commented/ignored files as coverage.**

That sequence turns the existing architecture into a safety net rather than a documentation of intent.
