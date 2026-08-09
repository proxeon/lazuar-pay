# R32 — AgentTool + IAgentPromptProvider → Ops.Contracts (notes)

**Date:** 2026-08-09  
**Branch:** `chore/remaining-005`  
**Scope:** PR-B from `03-bb-llm-move-to-ops.md` — cross-module agent extension points only. Factory/title already moved in R31.

---

## 1. What moved

| Item | From | To |
|------|------|-----|
| `AgentToolAttribute` | `BuildingBlocks.Application` | `Modules.Ops.Contracts` |
| `IAgentPromptProvider` | `BuildingBlocks.Application.Llm` | `Modules.Ops.Contracts` |

### Explicit non-moves

- `IToolRegistry` / `ToolRegistry` / `AgentToolDefinition` stay in `Modules.Ops.Application.Services`
- `IChatClientFactory` / title generator remain Ops.Application/Infrastructure (R31)

---

## 2. Ops.Contracts hygiene

| Change | Detail |
|--------|--------|
| New types | `AgentToolAttribute.cs`, `IAgentPromptProvider.cs` (BCL-only, no OpenAI/MediatR) |
| `Modules.Ops.Contracts.csproj` | Removed `BuildingBlocks.Application` + `Lazuar.ApiContracts` ProjectReferences (unused after hollow → agent-only) |
| `README.md` | Documents agent extension points (no longer “intentionally hollow”) |

---

## 3. ProjectReferences added

| Project | Ref |
|---------|-----|
| `Modules.One.Application` | `Modules.Ops.Contracts` |
| `Modules.Billing.Application` | `Modules.Ops.Contracts` |
| `Modules.Payments.Application` | `Modules.Ops.Contracts` |
| `Modules.Lhdn.Application` | `Modules.Ops.Contracts` |
| `Modules.Billing.Infrastructure` | `Modules.Ops.Contracts` (DI registration type) |
| `Modules.Ops.Application` | already had Contracts |

Architecture: Application → other module **Contracts only** — satisfied for `[AgentTool]` sites.

---

## 4. Consumers retargeted

### `[AgentTool]` sites (10)

| Module | File |
|--------|------|
| Ops | `RequestFormInputCommand.cs` |
| One | `InviteUserToWorkspaceCommand`, `RemoveWorkspaceMemberCommand`, `ToggleAppEntitlementCommand` |
| One | `GetWorkspaceDetailsAgentQuery`, `ListAppEntitlementsAgentQuery`, `ListWorkspaceMembersAgentQuery` |
| Billing | `GetFinancialHealthAgentQuery` |
| Payments | `GetPaymentConfigAgentQuery` |
| Lhdn | `ListLhdnSubmissionsAgentQuery` |

Each: `using Modules.Ops.Contracts;` (keep `BuildingBlocks.Application` for `ICommand`/`IQuery`).

### Prompt provider

| File | Change |
|------|--------|
| `BillingPromptProvider.cs` | implements `Modules.Ops.Contracts.IAgentPromptProvider` |
| `Billing.Infrastructure/DependencyInjection.cs` | `using Modules.Ops.Contracts` |
| `LlmOrchestratorService.cs` | `IEnumerable<IAgentPromptProvider>` from Ops.Contracts |
| `LlmOrchestratorServiceTests.cs` | same |
| `ToolRegistry.cs` | reflects `Modules.Ops.Contracts.AgentToolAttribute` |

### Deleted from BB

- `BuildingBlocks/Application/AgentToolAttribute.cs`
- `BuildingBlocks/Application/Llm/IAgentPromptProvider.cs` (+ empty `Llm/` dir)

---

## 5. Verification

- `dotnet build` Lazuar.slnx — 0 errors
- `dotnet test` Modules.Ops.Tests — 5 passed (incl. `ToolRegistryTests` discovery)
- `dotnet test` Lazuar.ArchitectureTests — 14 passed
- Grep: no `BuildingBlocks.Application.Llm`; no `AgentToolAttribute` under BuildingBlocks

---

## 6. Exit

- BB has **no** AgentTool / agent-prompt surface
- Cross-module agent types live only in `Modules.Ops.Contracts`
- Checklist: `plans/005-remaining/checklists/r32-bb-agent-tools-to-ops-contracts.md`
