# 03 — Move LLM stack from BuildingBlocks → Ops (FW-3 partial / F11)

**Status:** Analysis only — **no application code changes in this document**  
**Date:** 2026-08-09  
**Track:** FW-3 (BuildingBlocks product-port moves), checklist **F11**  
**Policy source:** [`apps/lazuar-api/docs/009-building-blocks-ownership.md`](../../apps/lazuar-api/docs/009-building-blocks-ownership.md)  
**Prior inventory:** [`plans/004-maintenance/06-building-blocks-shared-kernel.md`](../004-maintenance/06-building-blocks-shared-kernel.md) §2.3.2 / §F / Phase M2.5  
**Future checklist shell:** [`plans/004-maintenance/checklists-future/phase-f11-bb-llm-to-ops.md`](../004-maintenance/checklists-future/phase-f11-bb-llm-to-ops.md)  
**Related:** Phase 15 deferred LLM move (`phase-15-done.md` / `phase-15-building-blocks-thin.md` §15.3)

---

## 0. Executive summary

### Why move

BuildingBlocks currently owns an **Ops-product** LLM stack:

| Fact | Evidence |
|------|----------|
| Sole runtime orchestrator is Ops | `Modules.Ops.Infrastructure.Services.LlmOrchestratorService` is the only production consumer of `IChatClientFactory` / `ILlmTitleGenerator` |
| OpenAI type leak on BB Application | `IChatClientFactory.CreateClient(...)` returns `OpenAI.Chat.ChatClient` → **OpenAI NuGet on `BuildingBlocks.Application`**, which nearly every module Contracts/Application references |
| Agent tooling is an Ops product feature | `AgentToolAttribute` + `IAgentPromptProvider` exist only for the Ops agent / ToolRegistry discovery loop |
| Ownership map already decided | 009 §3: “Full LLM stack → **Ops** Infrastructure (+ Application ports)” and “`IAgentPromptProvider`, `AgentToolAttribute` → **Ops.Contracts** / Ops.Application” |
| Explicit non-goal violated by status quo | 00.6 / 009: do not invent an “LLM module” for purity — but also do **not** keep product orchestration deps in BB when a single module owns them |

### What “done” looks like (for this FW-3 partial)

1. **BB no longer ships** `Application/Llm/*`, `Infrastructure/Llm/*`, or `AgentToolAttribute` on the shared Application surface.  
2. **Ops owns** client factory, provider policies, title generator, and DI registration.  
3. **Cross-module extension points** (`IAgentPromptProvider`, `AgentToolAttribute`) live on **`Modules.Ops.Contracts`** so Billing / One / Payments / Lhdn can implement or annotate without depending on Ops.Application/Infrastructure (architecture rule: outer layers cross modules **only through Contracts**).  
4. **`BuildingBlocks.Application` drops the OpenAI package** (primary fan-out win).  
5. Architecture tests stay green; `Modules.Ops.Tests` stay green; host still boots with the same `Ai:*` config keys (or documented renames).  
6. 009 ownership map updated (LLM row moves from “Move / deferred” to “moved”).

### Recommended split (do not big-bang)

FW-3 recommended order already separates:

| Sub-PR | Scope | Size |
|--------|-------|------|
| **PR-A (F11a)** | Factory + policies + title + DI only (`IChatClientFactory`, `ILlmTitleGenerator`, impls) | Small–medium |
| **PR-B (F11b)** | `AgentToolAttribute` + `IAgentPromptProvider` → Ops.Contracts; Billing + tool-annotated modules retarget | Medium (touch many files, low logic risk) |

Optional later polish (not required for F11 exit): abstract vendor `ChatClient` / `ChatTool` off Ops.Application so only Infrastructure references OpenAI.

---

## 1. File inventory (current state)

Paths relative to `apps/lazuar-api/` unless noted.

### 1.1 BuildingBlocks — Application surface (ports + attribute)

| Path | Type | Role | Approx LOC | Notes |
|------|------|------|------------|-------|
| `BuildingBlocks/Application/Llm/IChatClientFactory.cs` | interface | Vendor client factory port | ~8 | **Returns `OpenAI.Chat.ChatClient`** — causes OpenAI package on Application |
| `BuildingBlocks/Application/Llm/ILlmTitleGenerator.cs` | interface | Conversation title generation | ~9 | Ops-only consumer |
| `BuildingBlocks/Application/Llm/IAgentPromptProvider.cs` | interface | Per-app system-prompt rules for Ops agent | ~7 | Implemented by Billing; consumed by Ops |
| `BuildingBlocks/Application/AgentToolAttribute.cs` | attribute | Tool metadata for discovery | ~20 | Namespace `BuildingBlocks.Application` (not under `Llm/`) |

### 1.2 BuildingBlocks — Infrastructure implementations

| Path | Type | Role | Approx LOC | Notes |
|------|------|------|------------|-------|
| `BuildingBlocks/Infrastructure/Llm/ChatClientFactory.cs` | sealed class | Multi-provider factory (OpenAI / OpenRouter / DeepSeek / MiMo) | ~75 | Reads `Ai:Provider`, `Ai:Model`, `Ai:ProviderKeys:{PROVIDER}`, `Ai:ApiKey`, `OpenRouter:SiteUrl/SiteName` |
| `BuildingBlocks/Infrastructure/Llm/OpenRouterHeaderPolicy.cs` | `PipelinePolicy` | HTTP-Referer + X-OpenRouter-Title | ~39 | OpenRouter-only |
| `BuildingBlocks/Infrastructure/Llm/ProviderQuirksPolicy.cs` | `PipelinePolicy` | Rewrites JSON body for thinking / max_tokens quirks | ~80 | Fast-exit when no rewrite needed (Content-Length safety) |
| `BuildingBlocks/Infrastructure/Llm/LlmTitleGenerator.cs` | sealed class | LLM title + string fallback | ~90 | Depends on `IChatClientFactory` |
| `BuildingBlocks/Infrastructure/Llm/LlmDependencyInjection.cs` | static | `AddThinLlmFactory()` | ~14 | Singleton factory + scoped title gen |

### 1.3 Ops — already module-owned orchestration (stays in Ops; do not pull into BB)

| Path | Role | Approx LOC |
|------|------|------------|
| `Modules/Ops/Infrastructure/Services/LlmOrchestratorService.cs` | ctor, non-stream + stream orchestration, title resolution, cost log | ~434 |
| `Modules/Ops/Infrastructure/Services/LlmOrchestratorService.Prompts.cs` | system prompt assembly + `IAgentPromptProvider` merge + tool options | ~99 |
| `Modules/Ops/Infrastructure/Services/LlmOrchestratorService.Tools.cs` | proposed action + `ExecuteReadToolAsync` | ~87 |
| `Modules/Ops/Infrastructure/Services/ToolCallAccumulator.cs` | streaming tool-arg byte accumulation | ~15 |
| `Modules/Ops/Application/Services/ILlmOrchestratorService.cs` | Ops application port (sync + stream) | ~13 |
| `Modules/Ops/Application/Services/IToolRegistry.cs` | `AgentToolDefinition` record + registry API | ~22 |
| `Modules/Ops/Application/Services/ToolRegistry.cs` | reflection discovery via `AgentToolAttribute`; builds `OpenAI.Chat.ChatTool` | ~220 |
| `Modules/Ops/Infrastructure/Endpoints/ChatEndpoints.cs` | HTTP chat | (calls orchestrator) |
| `Modules/Ops/Infrastructure/Endpoints/ChatStreamEndpoints.cs` | HTTP stream chat | (calls orchestrator) |
| `Modules/Ops/Infrastructure/DependencyInjection.cs` | registers `ILlmOrchestratorService`, `IToolRegistry` | ~47 |
| `Modules/Ops/Domain/OpsConversation.cs` / `OpsMessage.cs` | chat persistence model | — |
| `Modules/Ops/Contracts/` | **intentionally hollow** today (`README.md` only) | 0 product types |

### 1.4 Cross-module consumers of BB agent/LLM surface

#### A. Runtime DI / interfaces

| Consumer | What it uses | Path |
|----------|--------------|------|
| **Host** | `AddThinLlmFactory()` | `src/Lazuar.Api/Program.cs` (`using BuildingBlocks.Infrastructure.Llm`) |
| **Ops** | `IChatClientFactory`, `ILlmTitleGenerator`, `IAgentPromptProvider` | `LlmOrchestratorService*.cs` |
| **Billing** | implements + registers `IAgentPromptProvider` | `Modules/Billing/Application/Llm/BillingPromptProvider.cs`; `Modules/Billing/Infrastructure/DependencyInjection.cs` |
| **Commerce** | **dead using only** | `Modules/Commerce/Infrastructure/DependencyInjection.cs` has `using BuildingBlocks.Application.Llm` with **no type usage** — cleanup opportunity |
| **Ops tests** | mocks `IChatClientFactory`, `IAgentPromptProvider` | `tests/Modules.Ops.Tests/Services/LlmOrchestratorServiceTests.cs` |

#### B. `[AgentTool(...)]` attribute sites (10 types)

| Module | File | RequiredAppId / severity (summary) |
|--------|------|-------------------------------------|
| Ops | `Application/Commands/RequestFormInputCommand.cs` | CORE / low |
| One | `Application/Commands/InviteUserToWorkspaceCommand.cs` | CORE / medium |
| One | `Application/Commands/RemoveWorkspaceMemberCommand.cs` | CORE / high |
| One | `Application/Commands/ToggleAppEntitlementCommand.cs` | CORE / medium |
| One | `Application/Queries/Agent/GetWorkspaceDetailsAgentQuery.cs` | CORE / low |
| One | `Application/Queries/Agent/ListAppEntitlementsAgentQuery.cs` | CORE / low |
| One | `Application/Queries/Agent/ListWorkspaceMembersAgentQuery.cs` | CORE / low |
| Billing | `Application/Queries/Agent/GetFinancialHealthAgentQuery.cs` | BILLING / low |
| Payments | `Application/Queries/Agent/GetPaymentConfigAgentQuery.cs` | CORE / low |
| Lhdn | `Application/Queries/Agent/ListLhdnSubmissionsAgentQuery.cs` | LHDN / low |

All of these already `using BuildingBlocks.Application` for CQRS (`ICommand` / `IQuery`). Attribute currently resolves from the same assembly.

#### C. Config keys (no code owners outside factory / orchestrator)

| Key | Reader |
|-----|--------|
| `Ai:Provider` (default `OPENAI`) | `ChatClientFactory` |
| `Ai:Model` (default `gpt-4o`) | `ChatClientFactory` |
| `Ai:ProviderKeys:{PROVIDER}` / `Ai:ApiKey` | `ChatClientFactory` |
| `OpenRouter:SiteUrl`, `OpenRouter:SiteName` | `ChatClientFactory` / `OpenRouterHeaderPolicy` |
| `Ai:MaxToolIterations` (default 7) | `LlmOrchestratorService` (already Ops) |

User-secrets docs: `apps/lazuar-api/README.md` (`Ai:ProviderKeys:OPENROUTER`).

### 1.5 Package / project references that matter

| Project | OpenAI package? | Why |
|---------|-----------------|-----|
| `BuildingBlocks.Application` | **Yes** (`PackageReference Include="OpenAI"`) | Only needed for `IChatClientFactory` return type |
| `BuildingBlocks.Infrastructure` | **Yes** | Factory, policies, title generator |
| `Modules.Ops.Infrastructure` | **Yes** | Orchestrator uses `OpenAI.Chat` directly |
| `Modules.Ops.Application` | **No direct PackageReference** | Uses `OpenAI.Chat.ChatTool` in `ToolRegistry` / `AgentToolDefinition` — **compiles via transitive OpenAI from BB.Application** |
| Other module Application projects | No direct OpenAI | Only see OpenAI as transitive dep of BB.Application (weight / change coupling) |

Central version: `Directory.Packages.props` → `OpenAI` **2.1.0**.

### 1.6 What is *not* in this inventory (do not confuse)

| Item | Owner today | Move with F11? |
|------|-------------|----------------|
| `ILlmOrchestratorService` + stream orchestration | Ops | Already Ops — **no move** |
| `IToolRegistry` / `ToolRegistry` | Ops.Application | Stay Ops; only attribute namespace changes if AgentTool moves |
| `IEmailService` / Resend / messaging | BB | **F12**, not F11 |
| `MarkdownParser` / Markdig | BB Application | Separate; removing OpenAI from BB.Application is independent of Markdig |
| Credits / FinOps token billing | Billing product (not wired for LLM) | Out of scope; orchestrator only logs token usage |

---

## 2. Dependency graph (as-is)

### 2.1 Compile-time / package graph (LLM-relevant)

```
┌──────────────────────────────────────────────────────────────────────────┐
│ BuildingBlocks.Application                                               │
│  - IChatClientFactory  ──returns──► OpenAI.Chat.ChatClient  (pkg OpenAI) │
│  - ILlmTitleGenerator                                                    │
│  - IAgentPromptProvider                                                  │
│  - AgentToolAttribute                                                    │
│  - CQRS, IEmailService, … (unrelated)                                    │
└───────────────────────────────▲──────────────────────────────────────────┘
                                │ ProjectReference (almost every module)
┌───────────────────────────────┴──────────────────────────────────────────┐
│ BuildingBlocks.Infrastructure.Llm                                        │
│  ChatClientFactory : IChatClientFactory                                  │
│  LlmTitleGenerator : ILlmTitleGenerator                                  │
│  OpenRouterHeaderPolicy, ProviderQuirksPolicy                            │
│  AddThinLlmFactory()                                                     │
│  (also pkg OpenAI)                                                       │
└───────────────────────────────▲──────────────────────────────────────────┘
                                │ transitive via Ops.Infrastructure → BB.Infra
┌───────────────────────────────┴──────────────────────────────────────────┐
│ Host (Lazuar.Api)                                                        │
│  Program.cs: services.AddThinLlmFactory()                                │
│  Composition: AddOpsModule / AddBillingModule / …                        │
└──────────────────────────────────────────────────────────────────────────┘

Modules.Ops.Infrastructure
  LlmOrchestratorService
    ├── IChatClientFactory          (BB.Application.Llm)
    ├── ILlmTitleGenerator          (resolved from scope; BB registration)
    ├── IEnumerable<IAgentPromptProvider>
    ├── IToolRegistry               (Ops.Application)
    ├── IOpsRepository, IOneQueryService, MediatR, IExecutionContextAccessor
    └── OpenAI.Chat (direct pkg)

Modules.Ops.Application
  ToolRegistry ──reads──► AgentToolAttribute (BB.Application)
  AgentToolDefinition ──embeds──► OpenAI.Chat.ChatTool  (transitive OpenAI!)

Modules.Billing.Application
  BillingPromptProvider : IAgentPromptProvider (BB.Application.Llm)

Modules.Billing.Infrastructure
  services.AddSingleton<IAgentPromptProvider, BillingPromptProvider>()

Modules.{One,Billing,Payments,Lhdn,Ops}.Application
  [AgentTool(...)] on commands/queries ──► AgentToolAttribute (BB.Application)
```

### 2.2 Runtime resolution graph

```
Request → ChatEndpoints / ChatStreamEndpoints
       → ILlmOrchestratorService (Ops)
            │
            ├─ IChatClientFactory.CreateClient(thinkingEnabled: true, …)
            │     └─ ChatClientFactory → OpenAIClient / ChatClient + policies
            │
            ├─ IToolRegistry.GetAvailableTools(role, activeApps)
            │     └─ filters by AgentToolAttribute.AllowedRoles + RequiredAppId
            │
            ├─ IEnumerable<IAgentPromptProvider>  (e.g. BillingPromptProvider if BILLING active)
            │
            └─ ILlmTitleGenerator (new conversations only; scoped service from DI)
                  └─ uses IChatClientFactory again for title completion
```

DI registration today is **split**:

| Service | Where registered | Lifetime |
|---------|------------------|----------|
| `IChatClientFactory` → `ChatClientFactory` | Host via `AddThinLlmFactory()` | Singleton |
| `ILlmTitleGenerator` → `LlmTitleGenerator` | Host via `AddThinLlmFactory()` | Scoped |
| `ILlmOrchestratorService` → `LlmOrchestratorService` | `AddOpsModule` | Scoped |
| `IToolRegistry` → `ToolRegistry` | `AddOpsModule` | Singleton |
| `IAgentPromptProvider` → `BillingPromptProvider` | `AddBillingModule` | Singleton |

**Implication for move:** after relocation, host should either:

- call `services.AddOpsLlm()` / fold factory registration into `AddOpsModule`, **or**
- keep a thin host call that lives in Ops.Infrastructure (preferred: **one place** — `AddOpsModule`).

### 2.3 Architecture-test constraints that shape the move

From `tests/Lazuar.ArchitectureTests/ModuleBoundaryTests.cs`:

| Rule | Impact on LLM move |
|------|--------------------|
| **BB ↛ Modules.*** | Cannot leave a BB type that *references* Ops; move is one-way into Ops |
| **Outer layers cross modules only via `*.Contracts`** | `IAgentPromptProvider` and `AgentToolAttribute` **must** land in **`Modules.Ops.Contracts`** if Billing/One/Payments/Lhdn need them — **not** Ops.Application |
| Application ↛ own Infrastructure | Ops ports for factory/title can live in Ops.Application; impls in Ops.Infrastructure |
| Domain ↛ BB Application | Irrelevant (no Domain LLM types) |
| Host ↛ Module Application csproj | Host continues to compose via Ops.Infrastructure only |

Existing cross-module Contracts pattern already used heavily (e.g. Communications.Application → many `*.Contracts`). Billing.Application → Ops.Contracts is **allowed**.

### 2.4 Target dependency graph (desired)

```
BuildingBlocks.Application     — no Llm folder, no AgentToolAttribute, no OpenAI package
BuildingBlocks.Infrastructure  — no Llm folder, no OpenAI package (if nothing else needs it)

Modules.Ops.Contracts
  ├── AgentToolAttribute
  └── IAgentPromptProvider
  (prefer: drop unnecessary ProjectReference to BB.Application if still hollow besides these)

Modules.Ops.Application
  ├── IChatClientFactory          (may still return ChatClient short-term)
  ├── ILlmTitleGenerator
  ├── IToolRegistry / ToolRegistry (using Ops.Contracts.AgentToolAttribute)
  └── explicit PackageReference OpenAI (today transitive — make direct)

Modules.Ops.Infrastructure
  ├── ChatClientFactory, policies, LlmTitleGenerator
  ├── LlmOrchestratorService (unchanged responsibility)
  └── AddOpsModule registers factory + title + orchestrator

Modules.Billing.Application
  └── BillingPromptProvider : Modules.Ops.Contracts.IAgentPromptProvider
      + ProjectReference Ops.Contracts

Modules.{One,Payments,Lhdn,Billing,Ops}.Application
  └── [AgentTool] from Modules.Ops.Contracts
      + ProjectReference Ops.Contracts (where not already present)

Host Program.cs
  └── remove AddThinLlmFactory(); rely on AddOpsModule (or one Ops extension)
```

---

## 3. What stays in BuildingBlocks

After F11 (both sub-PRs), **nothing LLM- or agent-tool-specific** remains in BB.

### Stay (unchanged by this work)

All of 009 §2 technical core, including but not limited to:

- Domain: `Entity`, `IBusinessRule`, `IMustHaveTenant`, …
- Application: CQRS (`ICommand`/`IQuery`), `IIntegrationEvent` / bus ports, `IExecutionContextAccessor`, `ISqlConnectionFactory`, security ports, pagination
- Infrastructure: `PlatformDbContext`, outbox/inbox jobs, `InMemoryEventBus`, generic crypto/JWT/password, thin R2 port, technical dead-letter metrics
- **Not** moved in F11: email (`IEmailService`), messaging (`IMessagingService`), `MarkdownParser`, magic links, metrics plugins, worker options bag

### Explicitly leave BB after move (delete from BB, do not keep shims long-term)

| Leave BB | New home |
|----------|----------|
| `Application/Llm/*` (3 files) | Ops.Application ports + Ops.Contracts prompt port |
| `Application/AgentToolAttribute.cs` | Ops.Contracts |
| `Infrastructure/Llm/*` (5 files) | Ops.Infrastructure |
| OpenAI `PackageReference` on BB.Application | Remove |
| OpenAI `PackageReference` on BB.Infrastructure | Remove (verify no other OpenAI usages first — currently only Llm folder) |

**Do not** keep type-forwarders in BB after the move (architecture hygiene). Prefer a single atomic rename of namespaces + project refs in each sub-PR.

**Do not** create a new top-level “LLM module” (decision 00.6 / 009 §8). Ops is the product owner.

---

## 4. Move steps (ordered, implementable)

### Phase 0 — Preflight (read-only / tiny cleanup, optional)

1. Confirm greps still match this analysis (no new consumers of `BuildingBlocks.Application.Llm` / `AddThinLlmFactory`).  
2. Note dead `using BuildingBlocks.Application.Llm` in Commerce DI — remove in whichever PR touches that file or in PR-A.  
3. Snapshot: `dotnet test` on `Modules.Ops.Tests` + `Lazuar.ArchitectureTests` green on base branch.

### Phase A — PR-A: Factory stack only (recommended first PR)

**Goal:** Ops owns client creation + title generation; BB Application no longer needs OpenAI **if** AgentTool/prompt ports are still the only other BB LLM types…  

**Caveat:** After PR-A alone, BB.Application still has `IAgentPromptProvider` (no OpenAI) and still has OpenAI **until** `IChatClientFactory` leaves. PR-A is exactly when OpenAI can leave BB.Application.

#### A.1 Create ports under Ops.Application

Target folder suggestion:

```
Modules/Ops/Application/Llm/
  IChatClientFactory.cs
  ILlmTitleGenerator.cs
```

Namespace: `Modules.Ops.Application.Llm` (or `Modules.Ops.Application.Services` if you prefer fewer folders — either is fine; stay consistent with Billing’s `Application/Llm/BillingPromptProvider.cs` style).

Copy method signatures **byte-for-byte** initially:

```csharp
// IChatClientFactory — keep vendor return type in PR-A to avoid orchestrator rewrite
ChatClient CreateClient(
    string? providerOverride = null,
    string? modelOverride = null,
    bool thinkingEnabled = false,
    string reasoningEffort = "high");

// ILlmTitleGenerator
Task<string> GenerateAsync(string contentContext);
string GenerateFallback(string content);
```

Add **direct** `PackageReference Include="OpenAI"` to `Modules.Ops.Application.csproj` (needed for `ChatClient` return type **and** already for `ChatTool` in ToolRegistry once BB no longer supplies it).

#### A.2 Move implementations under Ops.Infrastructure

Target folder:

```
Modules/Ops/Infrastructure/Llm/
  ChatClientFactory.cs
  LlmTitleGenerator.cs
  OpenRouterHeaderPolicy.cs
  ProviderQuirksPolicy.cs
  // LlmDependencyInjection.cs — merge into Ops DependencyInjection instead of a free-standing BB extension
```

Namespace: `Modules.Ops.Infrastructure.Llm` (or `Modules.Ops.Infrastructure.Services` if co-located with orchestrator — **prefer `Llm/`** to keep provider policies discoverable).

Update usings/namespaces; keep config key names identical.

#### A.3 Wire DI inside `AddOpsModule`

In `Modules/Ops/Infrastructure/DependencyInjection.cs`:

```csharp
// conceptual — not applied in this analysis doc
services.AddSingleton<IChatClientFactory, ChatClientFactory>();
services.AddScoped<ILlmTitleGenerator, LlmTitleGenerator>();
// existing:
services.AddSingleton<IToolRegistry, ToolRegistry>();
services.AddScoped<ILlmOrchestratorService, LlmOrchestratorService>();
```

Delete `BuildingBlocks.Infrastructure.Llm.LlmDependencyInjection` (or leave obsolete stub one release — **prefer delete**).

#### A.4 Host

`Program.cs`:

- Remove `using BuildingBlocks.Infrastructure.Llm;`
- Remove `builder.Services.AddThinLlmFactory();`
- Ensure `AddOpsModule` is still called (already is via composition extensions)

Order note: factory registration must happen before any request uses Ops chat; module registration order is fine as long as both are on the same `IServiceCollection` before `Build()`.

#### A.5 Update Ops consumers + tests

| File | Change |
|------|--------|
| `LlmOrchestratorService.cs` | `using Modules.Ops.Application.Llm` (or new namespace) instead of `BuildingBlocks.Application.Llm` |
| `LlmOrchestratorServiceTests.cs` | same; mock types move |
| No Billing change in PR-A | Billing still uses BB `IAgentPromptProvider` |

#### A.6 Delete BB sources + drop OpenAI from BB projects

1. Delete `BuildingBlocks/Application/Llm/IChatClientFactory.cs`, `ILlmTitleGenerator.cs` (**keep** `IAgentPromptProvider.cs` until PR-B).  
2. Delete entire `BuildingBlocks/Infrastructure/Llm/` folder.  
3. Remove OpenAI from `BuildingBlocks.Application.csproj` **only if** no remaining type references OpenAI — after A.1–A.5, Application Llm left is only `IAgentPromptProvider` (no OpenAI). **Yes — remove OpenAI from BB.Application in PR-A.**  
4. Remove OpenAI from `BuildingBlocks.Infrastructure.csproj` after Llm folder gone.  
5. Grep for `BuildingBlocks.Infrastructure.Llm`, `AddThinLlmFactory`, `BuildingBlocks.Application.Llm.IChatClient` — expect zero.

#### A.7 PR-A verification

- `dotnet build` solution  
- `dotnet test` — `Modules.Ops.Tests`, `Lazuar.ArchitectureTests`, smoke host start if practical  
- Manual: Ops chat stream with configured `Ai:ProviderKeys:*` (if secrets present)

### Phase B — PR-B: Agent contracts to Ops.Contracts

**Goal:** BB no longer hosts agent extension points; modules that contribute tools/prompts depend on Ops.Contracts.

#### B.1 Populate Ops.Contracts (today hollow)

Add:

```
Modules/Ops/Contracts/
  AgentToolAttribute.cs          // namespace Modules.Ops.Contracts
  IAgentPromptProvider.cs        // namespace Modules.Ops.Contracts
```

Keep both **dependency-light**:

- `AgentToolAttribute` — BCL only (`System` attribute)  
- `IAgentPromptProvider` — no OpenAI, no MediatR  

**Csproj hygiene:** `Modules.Ops.Contracts` currently references `BuildingBlocks.Application` + ApiContracts “for symmetry.” After adding pure types, prefer **removing BB.Application ProjectReference** if still unused (check no leftover types). That improves the Contracts fan-out story from plan 06. Keep ApiContracts only if still needed (likely not for these two types).

Update `Modules/Ops/Contracts/README.md`: Contracts are no longer “intentionally hollow”; document that agent extension points live here.

#### B.2 Retarget attribute sites (10 files)

Replace:

```csharp
using BuildingBlocks.Application;
// [AgentTool(...)] resolves from BB
```

with:

```csharp
using BuildingBlocks.Application; // still needed for ICommand/IQuery
using Modules.Ops.Contracts;      // AgentToolAttribute
```

Add ProjectReference to `Modules.Ops.Contracts` on:

| Project | Already has Ops.Contracts? |
|---------|----------------------------|
| `Modules.Ops.Application` | Yes (via own Contracts) |
| `Modules.One.Application` | **No — add** |
| `Modules.Billing.Application` | **No — add** |
| `Modules.Payments.Application` | **No — add** |
| `Modules.Lhdn.Application` | **No — add** |

Architecture: Application → other module’s **Contracts only** — satisfied.

#### B.3 Retarget Billing prompt provider

| File | Change |
|------|--------|
| `BillingPromptProvider.cs` | `IAgentPromptProvider` from `Modules.Ops.Contracts` |
| `Billing.Infrastructure/DependencyInjection.cs` | `using Modules.Ops.Contracts` for registration type |

Billing.Infrastructure already references many Contracts; add Ops.Contracts if not transitive via Application (prefer **explicit** ProjectReference on Infrastructure if DI file needs the interface, or rely on Application transitively — explicit is clearer for greppability).

#### B.4 Ops internal usings

| File | Change |
|------|--------|
| `ToolRegistry.cs` | `AgentToolAttribute` from Ops.Contracts |
| `LlmOrchestratorService` (+ tests) | `IAgentPromptProvider` from Ops.Contracts |
| Delete `BuildingBlocks/Application/AgentToolAttribute.cs` | |
| Delete `BuildingBlocks/Application/Llm/IAgentPromptProvider.cs` | |
| Delete empty `BuildingBlocks/Application/Llm/` directory | |

#### B.5 PR-B verification

- All `[AgentTool]` types still discovered at runtime (ToolRegistry scans `AppDomain` — **namespace of attribute type does not matter** as long as attribute type identity is the one ToolRegistry reflects).  
- **Critical:** ToolRegistry uses `GetCustomAttribute<AgentToolAttribute>()` with a **compile-time generic**. After move, it must use the **Ops.Contracts** attribute type. Old BB attribute types on assemblies would not match — but we delete BB attribute, so all sites must recompile against Ops.Contracts in the **same** PR.  
- Billing prompt rules still inject when BILLING app active.  
- Architecture tests green.

### Phase C — Docs / map (can ride PR-B or tiny follow-up)

1. Update `apps/lazuar-api/docs/009-building-blocks-ownership.md`:  
   - §3 LLM / Agent rows → mark **moved** (or remove from move table into a “completed moves” note).  
   - §6 deferral table: “Move LLM stack → Ops” → **Done** with PR links.  
   - §7 decision matrix checkmarks.  
2. Optionally note in `docs/001-gaps/12-buildingblocks-host.md` that LLM inventory is historical (or leave gaps doc as archaeology).  
3. Tick F11 checklist (`phase-f11-bb-llm-to-ops.md`).  
4. FUTURE-WORK FW-3 order item 2–3 progress note.

### Phase D — Optional polish (out of F11 exit criteria; separate PRs)

| Polish | Why optional | Effort |
|--------|--------------|--------|
| Abstract `IChatClientFactory` to not return `ChatClient` (e.g. `IChatCompletionClient` port with stream/complete methods) | Removes OpenAI from **Ops.Application**; only Infrastructure holds vendor SDK | Large — touches orchestrator stream path |
| Stop embedding `ChatTool` in `AgentToolDefinition` (store schema BinaryData; build ChatTool in Infrastructure) | Same — Application OpenAI dep | Medium |
| `AiOptions` strongly typed + validate on Ops module start | Startup clarity; 12-buildingblocks-host gap | Small |
| Cost tracking beyond log line | Product/FinOps | Product epic |
| Finish `LlmOrchestratorService` stream/non-stream partials (F14 / phase 11.5) | Maintainability | Medium; independent of ownership move |
| Hardcoded SUPER_ADMIN in `BuildChatOptions` | Product/security debt | Separate |

---

## 5. DI changes (detail)

### 5.1 Today

```
Program.cs
  AddThinLlmFactory()
    → Singleton IChatClientFactory / ChatClientFactory
    → Scoped ILlmTitleGenerator / LlmTitleGenerator

AddOpsModule()
  → Singleton IToolRegistry / ToolRegistry
  → Scoped ILlmOrchestratorService / LlmOrchestratorService
  → Scoped IOpsRepository / …
  → keyed event bus + workers

AddBillingModule()
  → Singleton IAgentPromptProvider / BillingPromptProvider
```

### 5.2 After PR-A

```
Program.cs
  (no AddThinLlmFactory)

AddOpsModule()
  → Singleton IChatClientFactory / ChatClientFactory          // NEW here
  → Scoped ILlmTitleGenerator / LlmTitleGenerator              // NEW here
  → Singleton IToolRegistry / ToolRegistry
  → Scoped ILlmOrchestratorService / LlmOrchestratorService
  → …

AddBillingModule()
  → Singleton IAgentPromptProvider / BillingPromptProvider    // still BB interface until PR-B
```

### 5.3 After PR-B

```
AddOpsModule()
  → same as PR-A (interfaces now in Ops.Application / Ops.Contracts namespaces)

AddBillingModule()
  → Singleton Modules.Ops.Contracts.IAgentPromptProvider / BillingPromptProvider
```

### 5.4 Lifetime rationale (keep as-is)

| Service | Lifetime | Why |
|---------|----------|-----|
| `ChatClientFactory` | Singleton | Stateless wrapper over `IConfiguration`; creates new `ChatClient` per call |
| `LlmTitleGenerator` | Scoped | Uses factory + logger; scoped is fine; orchestrator resolves from created scopes anyway |
| `LlmOrchestratorService` | Scoped | Request/tenant context via `IExecutionContextAccessor` |
| `ToolRegistry` | Singleton | Reflection discovery once; concurrent dictionary |
| `BillingPromptProvider` | Singleton | Pure string rules; no scoped deps |

### 5.5 Registration failure modes to avoid

1. **Double registration** if host keeps `AddThinLlmFactory` and Ops also registers — last wins depending on DI container behavior; remove host call.  
2. **Missing OpenAI package on Ops.Application** after BB drops OpenAI — build break on `ChatTool` / `ChatClient`.  
3. **ToolRegistry attribute type mismatch** if some modules still compile against deleted BB attribute (impossible if BB type deleted and solution rebuilds — partial merges are the risk).  
4. **IAgentPromptProvider empty enumerable** if Billing module not loaded in a future host split — already true today when Billing not registered; orchestrator iterates providers safely.

---

## 6. Tests

### 6.1 Existing coverage

| Suite | Path | What it covers |
|-------|------|----------------|
| Ops unit | `tests/Modules.Ops.Tests/Services/LlmOrchestratorServiceTests.cs` | Reflection on private `ExecuteReadToolAsync`: empty JSON, malformed JSON, tenant inject, mediator exception → error string. Mocks `IChatClientFactory`, `IAgentPromptProvider`. |
| Architecture | `tests/Lazuar.ArchitectureTests/ModuleBoundaryTests.cs` | BB ↛ Modules; Application cross-module via Contracts only; Domain isolation; host csproj Application refs |

**Gap:** No dedicated unit tests for `ChatClientFactory` policies, title generator fallbacks, or ToolRegistry discovery (pre-existing).

### 6.2 Required updates when moving

| Test | Action |
|------|--------|
| `LlmOrchestratorServiceTests` | Change usings to Ops namespaces; no behavior change expected |
| Architecture tests | Should pass without new rules if Contracts pattern followed. Optionally **add** (nice-to-have): BB.Application must not reference package OpenAI (harder with NetArchTest — package refs not always asserted; csproj-level test possible like host Application-ref test) |
| Integration / manual | Stream chat smoke if available in local env |

### 6.3 Suggested new tests (optional but high value for PR-A)

1. **`ChatClientFactory` config selection** — in-memory `IConfiguration`: default OPENAI path vs OPENROUTER endpoint; missing key throws `InvalidOperationException` with provider name.  
2. **`LlmTitleGenerator.GenerateFallback`** — pure string cases (short, long, surrogate edge at cut point) without calling network.  
3. **`ProviderQuirksPolicy`** — if extracted testable: OpenRouter with thinkingEnabled adds `include_reasoning`; OPENAI no-op.  
4. **ToolRegistry** — one dummy type with Ops.Contracts `AgentToolAttribute` discovered when assembly loaded.

Do not block F11 exit on full factory coverage; orchestrator tests + architecture + build are the floor.

### 6.4 Commands (verification checklist)

```bash
# from apps/lazuar-api or repo root per Taskfile conventions
dotnet test tests/Modules.Ops.Tests/Modules.Ops.Tests.csproj
dotnet test tests/Lazuar.ArchitectureTests/Lazuar.ArchitectureTests.csproj
dotnet build Lazuar.slnx   # or solution path used by CI
```

---

## 7. Risks and mitigations

| Risk | Severity | Likelihood | Mitigation |
|------|----------|------------|------------|
| **Big-bang PR touches 15+ projects** | High merge noise | Medium if not split | Use PR-A / PR-B split as above |
| **Attribute type identity break** (ToolRegistry does not see tools) | High (Ops agent loses tools) | Low if single PR for all attribute sites | PR-B must move attribute definition + **all 10** usages together; post-merge smoke: call chat and confirm tools in options |
| **Ops.Application loses transitive OpenAI** after BB.Application drops package | Build break | **High** if forgotten | Explicit OpenAI PackageReference on Ops.Application in PR-A |
| **Vendor leak remains** (`ChatClient` / `ChatTool` on Application) | Medium (design debt) | Certain if PR-A minimal | Accept for F11; document Phase D polish; F11 exit does **not** require full abstraction |
| **Billing → Ops.Contracts** creates “Ops is special” coupling | Medium conceptual | Certain for agent design | Acceptable: agent is Ops product; Contracts is the correct extension point (same pattern as other modules’ Contracts) |
| **Circular Contracts** | High if mis-designed | Low | Ops.Contracts must stay free of Billing/One references; only attributes/interfaces |
| **Host registration forgotten** | Runtime DI failure on first chat | Medium | Fold into `AddOpsModule`; remove host call in same PR; search for `AddThinLlmFactory` |
| **Config key drift** | Runtime key missing | Low | Keep `Ai:*` / `OpenRouter:*` keys identical in move |
| **Streaming Content-Length bugs** reintroduced | High prod impact | Low if copy policies carefully | Move `ProviderQuirksPolicy` with comments intact; do not “clean up” rewrite logic casually |
| **Architecture test failure** if Billing.Application references Ops.Application | Medium | Medium if ports put in wrong layer | Ports for other modules → **Contracts only** |
| **Docs drift** (009 still says deferred) | Low | High if skipped | Phase C in same PR-B or immediate follow-up |
| **Parallel F10 port hygiene** conflicts | Low | Low | F11 does not require F10; optional sequence only |
| **Commerce dead using** confuses greps | Cosmetic | Certain today | Delete in PR-A |

### Historical bug to preserve (do not regress)

`LlmOrchestratorService.cs` header documents OpenAI SDK v2 streaming tool-arg corruption when concatenating `BinaryData` into strings. Move does **not** touch that path if orchestrator stays put — but any “cleanup” that rewrites stream accumulation is **out of scope and dangerous**.

---

## 8. PR size estimates

### PR-A — Factory stack

| Dimension | Estimate |
|-----------|----------|
| Files moved/created | ~7–9 (4–5 impl + 2 ports + DI edits) |
| Files deleted | ~5 BB Infrastructure Llm + 2 Application ports |
| Files lightly edited | Program.cs, Ops DI, LlmOrchestratorService, Ops tests, 2× csproj package refs, Commerce dead using |
| Logic change | **None** (namespace/ownership only) |
| Review surface | **Small–medium** (~300–400 LOC moved, low cognitive load) |
| Risk | Low–medium (DI + package graph) |
| Suggested title | `refactor(ops): move LLM factory from BuildingBlocks to Ops (FW-3 / F11a)` |

### PR-B — Agent Contracts

| Dimension | Estimate |
|-----------|----------|
| New files | 2 in Ops.Contracts (+ README) |
| Attribute site edits | 10 command/query files |
| Csproj ProjectReferences | +4 Application projects → Ops.Contracts |
| Billing prompt + DI | 2 files |
| ToolRegistry / orchestrator / tests usings | 3–4 files |
| Deletes | BB `AgentToolAttribute`, `IAgentPromptProvider` |
| Logic change | **None** |
| Review surface | **Medium width, thin depth** (many files, trivial diffs) |
| Risk | Medium (must be atomic for attribute identity) |
| Suggested title | `refactor(ops): move AgentTool + IAgentPromptProvider to Ops.Contracts (FW-3 / F11b)` |

### Combined single PR (not recommended)

| Dimension | Estimate |
|-----------|----------|
| Touch graph | BB + Ops + Billing + One + Payments + Lhdn + host + tests + docs |
| Review | Harder to bisect if ToolRegistry discovery breaks |
| Only justify if | Very short branch lifetime and single implementer |

### Out-of-scope size (Phase D abstractions)

Rewriting orchestrator behind a vendor-free port is **large** (stream + tools + title) — multi-day, separate epic. Do not bundle with ownership move.

---

## 9. Decision recap / non-goals

### In scope for F11

- Relocate BB LLM factory stack to Ops.  
- Relocate agent extension attribute + prompt provider interface to Ops.Contracts.  
- Drop OpenAI from BuildingBlocks.Application (and Infrastructure once empty of Llm).  
- Keep config keys and runtime behavior stable.  
- Update ownership map.

### Out of scope

- New LLM product module.  
- Email / messaging / Markdig moves (F12).  
- Metrics plugins (F13).  
- Fixing SUPER_ADMIN hardcoding, FinOps credits for tokens, rate limits.  
- Full OpenAI abstraction off Ops.Application.  
- Further LlmOrchestratorService partial splits (F14).  
- Changing chat HTTP contracts / TypeSpec.

### Design choice locked by architecture tests

**Cross-module agent types live in `Modules.Ops.Contracts`.** Putting `IAgentPromptProvider` in Ops.Application would force Billing.Application → Ops.Application, which fails `Outer_Layers_Should_Only_Reference_Other_Modules_Through_Contracts`.

---

## 10. Implementation checklist (for the implementer)

Copy into PR description or tick F11 checklist when executing.

### PR-A

- [ ] Add `IChatClientFactory`, `ILlmTitleGenerator` under Ops.Application  
- [ ] Move factory + policies + title generator under Ops.Infrastructure  
- [ ] Register in `AddOpsModule`; remove `AddThinLlmFactory` from host  
- [ ] Update Ops orchestrator + Ops tests usings  
- [ ] Delete BB Application ports for factory/title + entire BB Infrastructure Llm  
- [ ] Remove OpenAI package from BB.Application and BB.Infrastructure  
- [ ] Add explicit OpenAI package to Ops.Application  
- [ ] Remove Commerce dead `using BuildingBlocks.Application.Llm`  
- [ ] Grep clean for old namespaces / `AddThinLlmFactory`  
- [ ] `Modules.Ops.Tests` + ArchitectureTests green  

### PR-B

- [ ] Add `AgentToolAttribute` + `IAgentPromptProvider` to Ops.Contracts  
- [ ] Update Contracts README  
- [ ] Retarget 10 `[AgentTool]` sites + ProjectReferences  
- [ ] Retarget BillingPromptProvider + Billing DI  
- [ ] Update ToolRegistry + orchestrator + tests  
- [ ] Delete BB attribute + BB `IAgentPromptProvider`  
- [ ] Prefer drop unused BB.Application ref from Ops.Contracts if safe  
- [ ] ArchitectureTests green; manual tool discovery smoke  
- [ ] Update `docs/009-building-blocks-ownership.md`  
- [ ] Tick `phase-f11-bb-llm-to-ops.md`  

---

## 11. Traceability matrix (type → home)

| Type | Today | After F11 | PR |
|------|-------|-----------|-----|
| `IChatClientFactory` | BB.Application.Llm | Ops.Application.Llm | A |
| `ILlmTitleGenerator` | BB.Application.Llm | Ops.Application.Llm | A |
| `ChatClientFactory` | BB.Infrastructure.Llm | Ops.Infrastructure.Llm | A |
| `LlmTitleGenerator` | BB.Infrastructure.Llm | Ops.Infrastructure.Llm | A |
| `OpenRouterHeaderPolicy` | BB.Infrastructure.Llm | Ops.Infrastructure.Llm | A |
| `ProviderQuirksPolicy` | BB.Infrastructure.Llm | Ops.Infrastructure.Llm | A |
| `AddThinLlmFactory` | BB.Infrastructure.Llm | **deleted**; logic in `AddOpsModule` | A |
| `IAgentPromptProvider` | BB.Application.Llm | Ops.Contracts | B |
| `AgentToolAttribute` | BB.Application | Ops.Contracts | B |
| `BillingPromptProvider` | Billing.Application (implements BB) | implements Ops.Contracts | B |
| `LlmOrchestratorService` | Ops.Infrastructure | Ops.Infrastructure (stay) | — |
| `ILlmOrchestratorService` | Ops.Application | Ops.Application (stay) | — |
| `ToolRegistry` / `IToolRegistry` | Ops.Application | Ops.Application (stay; using updates) | B usings |

---

## 12. Evidence sources (this analysis)

| Source | Used for |
|--------|----------|
| `apps/lazuar-api/docs/009-building-blocks-ownership.md` | Ownership policy, deferred status |
| `apps/lazuar-api/BuildingBlocks/Application/Llm/*` | Port inventory |
| `apps/lazuar-api/BuildingBlocks/Application/AgentToolAttribute.cs` | Attribute inventory |
| `apps/lazuar-api/BuildingBlocks/Infrastructure/Llm/*` | Impl inventory |
| `apps/lazuar-api/Modules/Ops/Infrastructure/Services/LlmOrchestratorService*.cs` | Sole orchestrator consumer |
| `apps/lazuar-api/Modules/Ops/Application/Services/ToolRegistry.cs` | Attribute discovery |
| `apps/lazuar-api/Modules/Billing/Application/Llm/BillingPromptProvider.cs` | Prompt extension |
| `apps/lazuar-api/src/Lazuar.Api/Program.cs` | Host DI |
| `apps/lazuar-api/tests/Modules.Ops.Tests/Services/LlmOrchestratorServiceTests.cs` | Test surface |
| `apps/lazuar-api/tests/Lazuar.ArchitectureTests/ModuleBoundaryTests.cs` | Contracts-only cross-module rule |
| `plans/004-maintenance/FUTURE-WORK.md` FW-3 | Program order |
| `plans/004-maintenance/checklists-future/phase-f11-bb-llm-to-ops.md` | Checklist shell |
| Repo greps for `IChatClientFactory`, `AgentTool`, `AddThinLlmFactory`, OpenAI package refs | Consumer completeness |

---

## 13. Bottom line

Moving the LLM stack is **not** a rewrite of the Ops agent. It is an **ownership and package-boundary** change:

1. **PR-A** relocates the thin multi-provider factory + title helper next to the only orchestrator, and **strips OpenAI off BuildingBlocks.Application** (largest fan-out win).  
2. **PR-B** makes Ops.Contracts the real extension surface for tools and prompt rules, matching modular-monolith Contracts rules already enforced by architecture tests.  

What stays in BuildingBlocks is the technical spine only. What stays in Ops is everything that already is Ops — plus the provider stack BB should never have owned long-term.

**Estimated total effort:** 0.5–1.5 engineer-days if split as above and tested carefully; more if Phase D vendor abstraction is bundled (do not bundle).
