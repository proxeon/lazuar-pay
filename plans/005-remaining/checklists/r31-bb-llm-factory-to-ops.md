# R31 — LLM factory/policies/title → Ops

**Track:** BB · **Analysis:** `../03-bb-llm-move-to-ops.md` PR-A  
**Goal:** Remove OpenAI package from BuildingBlocks.Application

---

## R31.1 Move files

- [ ] `IChatClientFactory`, policies, title generator, DI (`AddThinLlmFactory`) → Ops
- [ ] Fold registration into `AddOpsModule`
- [ ] Drop OpenAI from BB Application package refs if unused

## R31.2 Fix consumers

- [ ] Ops orchestrator usings/DI
- [ ] Remove dead `using BuildingBlocks.Application.Llm` elsewhere (e.g. Commerce)

## R31.3 Tests

- [ ] Modules.Ops.Tests green
- [ ] Architecture tests green
- [ ] Host builds

## R31.4 Docs

- [ ] Update 009 ownership map

## R31.5 Exit

- [ ] BB has no LLM factory surface
