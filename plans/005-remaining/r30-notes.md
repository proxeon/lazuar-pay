# R30 — BuildingBlocks port hygiene (notes)

**Date:** 2026-08-09  
**Branch:** `chore/remaining-005`  
**Scope:** Thin only — Application ports / Infrastructure implements. **No** LLM, email, messaging, magic-link, or metrics-plugin product moves (R31+).

---

## 1. Inventory (R30.1)

### 1.1 Interfaces defined under BB.Infrastructure (pre-R30)

| Interface | Pre-R30 location | Module / host consumers | Action |
|-----------|------------------|-------------------------|--------|
| `IJwtService` | `BuildingBlocks/Infrastructure/JwtService.cs` (co-located with impl) | One: `AuthEndpoints`, `PlatformAuthEndpoints`; host DI; module tests | **Moved** → Application |
| `IR2StorageService` | `BuildingBlocks/Infrastructure/R2StorageService.cs` | Billing: document store/download; One: presigned upload; host DI | **Moved** → Application |
| `IPlatformMetricsCollector` | `BuildingBlocks/Infrastructure/Observability/` | Host health/metrics + BB jobs only | **Stay** (host/observability spine; not a module product port) |
| `IMessageProcessingState` | `MessageProcessingResultApplier.cs` | Outbox/Inbox message types only | **Stay** (messaging spine internal) |

### 1.2 Ports already correctly in Application (no move)

| Port | Notes |
|------|--------|
| `IPasswordService`, `ISecretVault`, `ITokenGeneratorService` | Application + Infrastructure impls |
| `ISqlConnectionFactory`, `IExecutionContextAccessor` | Application |
| `IEventBus` / subscriptions / CQRS | Application |
| `IEmailService`, `IMessagingService`, `IMagicLinkTokenService` | Application today; **product re-home deferred** (R33–R34 / 009) |
| LLM ports (`IChatClientFactory`, …) | Application today; **Ops move deferred** (R31–R32) |

### 1.3 Concrete BB services injected where interface would suffice

| Finding | Verdict |
|---------|---------|
| Host registers `IJwtService` → `JwtService`, `IR2StorageService` → `R2StorageService` / `DisabledR2StorageService` | Correct (interface injection) |
| Modules inject `IJwtService` / `IR2StorageService` | Correct after port move |
| `DocumentLinkSigner` static helper used from modules | Not a DI port; generic HMAC + product payload helpers — ownership deferred in 009 (stay BB for now) |
| Messaging spine types (`OutboxMessage`, jobs, `PlatformDbContext`) | Expected Infrastructure concrete usage by module Infrastructure workers |

**Conclusion:** After Phase 15 map, the only **module-facing inverted ports** were `IJwtService` and `IR2StorageService`. Other Application ports were already clean; product stacks intentionally deferred.

---

## 2. Moves (R30.2)

| Change | Detail |
|--------|--------|
| Add | `BuildingBlocks/Application/IJwtService.cs` |
| Add | `BuildingBlocks/Application/IR2StorageService.cs` |
| Edit | `JwtService.cs` / `R2StorageService.cs` — implement Application ports only |
| Usings | Dropped unused `using BuildingBlocks.Infrastructure` where only the moved ports were needed (One auth/storage endpoints, Billing ledger + document handler) |
| DI | Host `Program.cs` unchanged in registrations (already used interface → impl; both namespaces already imported) |

### Explicit non-moves (this PR)

- LLM stack, agent tools  
- Email / messaging / magic link  
- `IPlatformMetricsCollector` pluginization  
- `DocumentLinkSigner` product payload helpers  

---

## 3. Architecture tests

Added `ModuleBoundaryTests.Shared_Technical_Ports_Must_Live_In_BuildingBlocks_Application`:

- Asserts `IJwtService`, `IR2StorageService`, and other shared technical ports resolve from `BuildingBlocks.Application`
- Asserts Infrastructure does not re-define those public interface names

---

## 4. Docs

- Updated `apps/lazuar-api/docs/009-building-blocks-ownership.md` (stay table, deferrals, decision matrix, R2 grey area)

---

## 5. Exit

- No product logic moved into or out of BB beyond port placement  
- Checklist: `plans/005-remaining/checklists/r30-bb-port-hygiene.md`
