# F14 — Residual polish (FW-7)

**Goal:** Opportunistic navigability and test ergonomics.  
**Depends on:** none  
**PR shape:** one file family per PR preferred

---

## F14.1 God files (when touching)

- [ ] `LhdnGatewayAdapter` partials (token/submit/status/TIN/cancel)
- [ ] `LlmOrchestratorService` remaining partial cleanup
- [ ] Payment gateway shared name/amount helpers (no mega base class)
- [ ] `BillingQueryService` / `B2cConsolidationJob` partials if editing those areas

## F14.2 TestSupport rollout

- [ ] Migrate additional ModuleTests to `Lazuar.TestSupport` (batch of N: ________)
- [ ] Document remaining high-copy suites for later

## F14.3 Shared helpers

- [ ] Optional `AddModuleOutboxInbox<T>` pilot on one module
- [ ] Expand ProblemDetails `code` map as endpoints are touched
- [ ] Pagination: more endpoints use shared `Paging` helper

## F14.4 Exit

- [ ] At least one polish PR landed **or** wave explicitly skips F14
- [ ] No behavior change without tests
