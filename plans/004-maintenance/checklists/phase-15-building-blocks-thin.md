# Phase 15 — BuildingBlocks / SharedKernel thinning

**Goal:** BB stays technical; product concerns move to modules.  
**Evidence:** `../06-building-blocks-shared-kernel.md`  
**Do gradually** (multiple PRs). Do not block Horizon 1–2.  
**Status:** Safe subset done 2026-08-09 (`phase-15-done.md`). Full LLM/email/metrics plugin moves deferred.

---

## 15.1 Policy write-up

- [x] Write short ADR or update `002-shared-kernel-vs-building-blocks.md` with stay/move list — **`docs/009-building-blocks-ownership.md`** + refined **`docs/002`**
- [x] Decide SharedKernel: **populate** with real shared domain types **or** keep as marker and document why — **keep marker**; README + `SharedKernelMarker` xmldoc

## 15.2 Port placement hygiene

- [ ] Move infrastructure-defined ports that belong in Application abstractions (e.g. storage port) if currently inverted — **deferred**
- [ ] Ensure modules depend on abstractions, not concrete BB services where feasible — **deferred** (status quo)

## 15.3 LLM stack → Ops

- [ ] Inventory BB LLM types (`IChatClientFactory`, title generator, OpenRouter policies) — **inventory in 009 / plan 06 only; no code move**
- [ ] Move Ops-owned orchestration dependencies toward Ops module — **deferred** (non-trivial)
- [ ] Keep only true shared chat client factory if multiple modules need it — **N/A until move**
- [ ] Ops tests green — **N/A (no LLM move)**

## 15.4 Email / messaging ports ownership

- [x] Decide Resend/console email: BB vs Messaging vs Communications — **Messaging ownership in 009**; move deferred
- [ ] Move product email template HTML builders out of BB Application if still there — **deferred**
- [x] Keep `IEmailService` abstraction placement consistent with decision — **documented: thin port may stay Application until Messaging owns impl**

## 15.5 Metrics god SQL

- [x] Inventory `PlatformMetricsCollector` schema knowledge (dunning, webhooks, lhdn, …) — **009 + class remarks**
- [x] Introduce module metric contributors **or** accept temporary god collector with ticket — **accept temporary + plugin direction comment** (no interface yet)
- [ ] Prefer plugin interface over more SQL in BB — **direction only; implement later**

## 15.6 Worker options

- [ ] Reduce module-wide `BackgroundWorkerOptions` coupling if all intervals live only in BB — **deferred**
- [ ] Prefer per-module options where intervals differ — **deferred**

## 15.7 Dead host parallel types

- [x] Remove host `PlatformDbContext` duplicate if still unused (confirm with grep) — **deleted** `src/Lazuar.Api/Infrastructure/Data/PlatformDbContext.cs`
- [x] Single PlatformDbContext ownership in BB — **yes** (`BuildingBlocks.Infrastructure.PlatformDbContext`)

## 15.8 Exit criteria

- [x] Written ownership map for LLM / email / metrics — **docs/009**
- [x] At least one product concern moved out of BB **or** explicitly deferred with issue — **explicitly deferred** (009 §6, phase-15-done)
- [x] Architecture tests still enforce BB ↔ module direction — **unchanged enforcement; no BB→Modules edges added**
