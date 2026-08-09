# Phase 15 — Analysis (BuildingBlocks / SharedKernel thinning — safe subset)

**Date:** 2026-08-09  
**Branch:** `chore/backend-maintenance-004`  
**Goal:** BB stays technical; product concerns move to modules — **gradually**. Full LLM/email move is large; ship ownership map + hygiene only.  
**Evidence:** `checklists/phase-15-building-blocks-thin.md`, `06-building-blocks-shared-kernel.md`, `decisions.md` (00.4, 00.6)

---

## 1. Pre-change inventory (from plan 06 + live grep)

### 1.1 Fatness (summary)

| Assembly | State |
|----------|--------|
| BuildingBlocks.Domain | Lean (Entity, rules, `IMustHaveTenant`) |
| BuildingBlocks.Application | Medium-high: CQRS + product ports (email, LLM, agent, Markdig) |
| BuildingBlocks.Infrastructure | High: spine + Resend + LLM + metrics god SQL + worker options bag |
| SharedKernel | Empty marker only (`SharedKernelMarker`) |

### 1.2 Host parallel type

| Path | Used? | Action |
|------|-------|--------|
| `src/Lazuar.Api/Infrastructure/Data/PlatformDbContext.cs` | **No** (namespace only self; all modules inherit `BuildingBlocks.Infrastructure.PlatformDbContext`) | **Delete** |

Grep: only definition under `Lazuar.Api.Infrastructure.Data`; architecture tests reference BB type.

### 1.3 PlatformMetricsCollector

- Hardcoded `ModuleSchemas` (nine modules).
- Raw SQL per schema outbox/inbox.
- Product SQL: `lhdn."TaxDocuments"` stuck statuses.
- Process counters: dead letters, webhook failed, dunning cancels.

**Direction:** plugin contributors (`IPlatformMetricsContributor`) — not implemented this phase (comment only).

### 1.4 LLM / email (explicit non-moves)

| Stack | Consumers | Why not move in Phase 15 |
|-------|-----------|---------------------------|
| LLM factory, policies, title, DI | Ops orchestrator; Billing `IAgentPromptProvider` | Multi-file, OpenAI package on Application, DI + Ops tests — non-trivial |
| IEmailService / Resend / templates | Messaging dispatch | Composition root + BYOK product rules; decision 00.4 related freeze |
| IMessagingService | Messaging only | Same; multi-channel product frozen |

---

## 2. Scope chosen (safe subset)

| Item | In scope? |
|------|-----------|
| 15.1 Policy write-up + SharedKernel decision | **Yes** — docs 009 + update 002; marker README/comment |
| 15.2 Port placement hygiene | **No** (deferred) |
| 15.3 LLM → Ops | **No** (deferred) |
| 15.4 Email / messaging ownership | **Document only** (move deferred) |
| 15.5 Metrics plugins | **Comment only** |
| 15.6 Worker options split | **No** (deferred) |
| 15.7 Dead host PlatformDbContext | **Yes** — delete if unused |
| Architecture tests re-run | **Yes** (spot-check) |

Product-concern exit: **explicitly deferred with ownership map** (not a silent kitchen-sink).

---

## 3. Target artifacts

```
apps/lazuar-api/docs/009-building-blocks-ownership.md   # stay/move/defer matrix
apps/lazuar-api/docs/002-shared-kernel-vs-building-blocks.md  # refined rules + link 009
apps/lazuar-api/SharedKernel/README.md
apps/lazuar-api/SharedKernel/SharedKernelMarker.cs       # intentional-empty comment
BuildingBlocks/.../PlatformMetricsCollector.cs           # plugin-direction remarks
DELETE src/Lazuar.Api/Infrastructure/Data/PlatformDbContext.cs
plans/004-maintenance/phase-15-analysis.md | phase-15-done.md
checklists/phase-15-building-blocks-thin.md              # honest checkboxes
```

---

## 4. Risks

| Risk | Mitigation |
|------|------------|
| Deleting host PlatformDbContext breaks something | Grep shows zero consumers; modules use BB base |
| Docs drift again | 009 is ownership SSoT; 002 points to it |
| Contributors still dump into BB | Map §9 “how to use”; metrics class remarks |

---

## 5. Explicit non-goals (this PR)

- Moving LLM stack, email adapters, MarkdownParser, MagicLink, Document payloads
- Introducing `IPlatformMetricsContributor` or DI schema registration
- Splitting BuildingBlocks into multiple csproj
- Populating SharedKernel with placeholder VOs
- New modules (00.6)
