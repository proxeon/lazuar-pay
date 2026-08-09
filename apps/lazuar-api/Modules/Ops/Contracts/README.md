# Ops.Contracts

Cross-module **agent extension points** owned by Ops:

| Type | Purpose |
|------|---------|
| `AgentToolAttribute` | Annotate MediatR commands/queries so Ops `ToolRegistry` discovers them as LLM tools |
| `IAgentPromptProvider` | Inject per-app system-prompt rules into the Ops orchestrator (e.g. Billing) |

Other modules (One, Billing, Payments, Lhdn, …) may ProjectReference this assembly from **Application** (or Infrastructure for DI registration) only. Do **not** reference Ops.Application or Ops.Infrastructure from outer layers of other modules.

Chat orchestration ports (`ILlmOrchestratorService`, `IToolRegistry`, factory/title) stay in Ops.Application — not Contracts — because only Ops Infrastructure consumes them.
