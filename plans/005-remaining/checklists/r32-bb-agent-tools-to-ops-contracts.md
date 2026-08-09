# R32 — AgentTool + IAgentPromptProvider → Ops.Contracts

**Track:** BB · **Analysis:** `../03-bb-llm-move-to-ops.md` PR-B  
**Depends on:** R31 recommended first  
**Arch rule:** Cross-module types via Contracts only  
**Notes:** `../r32-notes.md`

---

## R32.1 Move

- [x] `AgentToolAttribute`, `IAgentPromptProvider` → `Modules.Ops.Contracts`
- [x] Retarget all `[AgentTool]` sites (One/Billing/Payments/Lhdn/Ops — count: **10**)
- [x] Billing prompt provider implements Ops.Contracts interface

## R32.2 Cleanup BB

- [x] Remove agent types from BuildingBlocks.Application
- [x] Package refs updated (Ops.Contracts dependency-light; module Application/Infra ProjectRefs added)

## R32.3 Tests

- [x] Tool discovery still works (Ops tests — `ToolRegistryTests` + orchestrator suite)
- [x] Architecture: Contracts-only references

## R32.4 Exit

- [x] No AgentTool in BB
