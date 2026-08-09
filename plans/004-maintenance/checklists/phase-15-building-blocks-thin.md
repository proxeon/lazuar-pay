# Phase 15 — BuildingBlocks / SharedKernel thinning

**Goal:** BB stays technical; product concerns move to modules.  
**Evidence:** `../06-building-blocks-shared-kernel.md`  
**Do gradually** (multiple PRs). Do not block Horizon 1–2.

---

## 15.1 Policy write-up

- [ ] Write short ADR or update `002-shared-kernel-vs-building-blocks.md` with stay/move list
- [ ] Decide SharedKernel: **populate** with real shared domain types **or** keep as marker and document why

## 15.2 Port placement hygiene

- [ ] Move infrastructure-defined ports that belong in Application abstractions (e.g. storage port) if currently inverted
- [ ] Ensure modules depend on abstractions, not concrete BB services where feasible

## 15.3 LLM stack → Ops

- [ ] Inventory BB LLM types (`IChatClientFactory`, title generator, OpenRouter policies)
- [ ] Move Ops-owned orchestration dependencies toward Ops module
- [ ] Keep only true shared chat client factory if multiple modules need it
- [ ] Ops tests green

## 15.4 Email / messaging ports ownership

- [ ] Decide Resend/console email: BB vs Messaging vs Communications
- [ ] Move product email template HTML builders out of BB Application if still there
- [ ] Keep `IEmailService` abstraction placement consistent with decision

## 15.5 Metrics god SQL

- [ ] Inventory `PlatformMetricsCollector` schema knowledge (dunning, webhooks, lhdn, …)
- [ ] Introduce module metric contributors **or** accept temporary god collector with ticket
- [ ] Prefer plugin interface over more SQL in BB

## 15.6 Worker options

- [ ] Reduce module-wide `BackgroundWorkerOptions` coupling if all intervals live only in BB
- [ ] Prefer per-module options where intervals differ

## 15.7 Dead host parallel types

- [ ] Remove host `PlatformDbContext` duplicate if still unused (confirm with grep)
- [ ] Single PlatformDbContext ownership in BB

## 15.8 Exit criteria

- [ ] Written ownership map for LLM / email / metrics
- [ ] At least one product concern moved out of BB **or** explicitly deferred with issue
- [ ] Architecture tests still enforce BB ↔ module direction
