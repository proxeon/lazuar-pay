# R31 — LLM factory/policies/title → Ops (notes)

**Date:** 2026-08-09  
**Branch:** `chore/remaining-005`  
**Scope:** PR-A from `03-bb-llm-move-to-ops.md` — factory + policies + title + DI only. **No** AgentTool / `IAgentPromptProvider` move (R32).

---

## 1. What moved

| Item | From | To |
|------|------|-----|
| `IChatClientFactory` | `BuildingBlocks.Application.Llm` | `Modules.Ops.Application.Llm` |
| `ILlmTitleGenerator` | `BuildingBlocks.Application.Llm` | `Modules.Ops.Application.Llm` |
| `ChatClientFactory`, `LlmTitleGenerator`, `OpenRouterHeaderPolicy`, `ProviderQuirksPolicy` | `BuildingBlocks.Infrastructure.Llm` | `Modules.Ops.Infrastructure.Llm` |
| DI | Host `AddThinLlmFactory()` | `AddOpsModule` (Singleton factory, Scoped title gen) |

### Explicit non-moves (R32)

- `IAgentPromptProvider` remains in `BuildingBlocks.Application.Llm`
- `AgentToolAttribute` remains in BuildingBlocks.Application
- Billing `BillingPromptProvider` still implements BB `IAgentPromptProvider`

---

## 2. Host / packages

| Change | Detail |
|--------|--------|
| `Program.cs` | Removed `using BuildingBlocks.Infrastructure.Llm` and `AddThinLlmFactory()` |
| `Modules.Ops.Application` | Added direct `PackageReference Include="OpenAI"` (factory return type + existing ToolRegistry `ChatTool`) |
| `BuildingBlocks.Application` | Removed OpenAI package |
| `BuildingBlocks.Infrastructure` | Removed OpenAI package |
| Commerce DI | Removed dead `using BuildingBlocks.Application.Llm` |

---

## 3. Consumers updated

| File | Change |
|------|--------|
| `LlmOrchestratorService.cs` | `using Modules.Ops.Application.Llm` (keeps BB Llm for `IAgentPromptProvider`) |
| `LlmOrchestratorServiceTests.cs` | same |
| Billing DI | unchanged (`IAgentPromptProvider` still BB) |

---

## 4. Verification

- `dotnet build apps/lazuar-api` (host / solution)
- `dotnet test` Modules.Ops.Tests + ArchitectureTests
- Grep: `AddThinLlmFactory` and `BuildingBlocks.Infrastructure.Llm` empty

---

## 5. Exit

- BB has no LLM factory surface (only `IAgentPromptProvider` left under Application/Llm)
- OpenAI no longer on BB.Application / BB.Infrastructure
- Checklist: `plans/005-remaining/checklists/r31-bb-llm-factory-to-ops.md`
