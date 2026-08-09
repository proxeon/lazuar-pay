# R32 — AgentTool + IAgentPromptProvider → Ops.Contracts

**Track:** BB · **Analysis:** `../03-bb-llm-move-to-ops.md` PR-B  
**Depends on:** R31 recommended first  
**Arch rule:** Cross-module types via Contracts only

---

## R32.1 Move

- [ ] `AgentToolAttribute`, `IAgentPromptProvider` → `Modules.Ops.Contracts`
- [ ] Retarget all `[AgentTool]` sites (One/Billing/Payments/Lhdn/Ops — count: ________)
- [ ] Billing prompt provider implements Ops.Contracts interface

## R32.2 Cleanup BB

- [ ] Remove agent types from BuildingBlocks.Application
- [ ] Package refs updated

## R32.3 Tests

- [ ] Tool discovery still works (Ops tests)
- [ ] Architecture: Contracts-only references

## R32.4 Exit

- [ ] No AgentTool in BB
