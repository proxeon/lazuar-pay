# R31 — LLM factory/policies/title → Ops

**Track:** BB · **Analysis:** `../03-bb-llm-move-to-ops.md` PR-A  
**Goal:** Remove OpenAI package from BuildingBlocks.Application

---

## R31.1 Move files

- [x] `IChatClientFactory`, policies, title generator, DI (`AddThinLlmFactory`) → Ops
- [x] Fold registration into `AddOpsModule`
- [x] Drop OpenAI from BB Application package refs if unused

## R31.2 Fix consumers

- [x] Ops orchestrator usings/DI
- [x] Remove dead `using BuildingBlocks.Application.Llm` elsewhere (e.g. Commerce)

## R31.3 Tests

- [x] Modules.Ops.Tests green
- [x] Architecture tests green
- [x] Host builds

## R31.4 Docs

- [x] Update 009 ownership map

## R31.5 Exit

- [x] BB has no LLM factory surface
