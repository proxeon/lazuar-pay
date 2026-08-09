# Phase 11 — Additional god-file splits (P1)

**Goal:** Continue house-style splits when capacity allows.  
**Do as separate PRs per file** (not one mega PR).  
**Evidence:** `../02-large-files-chunking.md` §3.4–3.9

---

## 11.1 GatewayPaymentCompletedIntegrationEventHandler (~375 LOC)

- [x] Inventory: checkout complete vs subscription payment vs logging  
  → `../phase-11-analysis.md` §1.2
- [x] Split into focused handlers **or** private methods/files with clear names  
  → partials: router / OpenCheckout / Subscription / Helpers
- [x] Keep integration event type and subscription registration stable  
  → type name + ctor + `AddTransient` + `eventBus.Subscribe` unchanged
- [x] Tests for payment-completed paths green  
  → CommerceProductCompleteness + TenantIsolationHardening (handler ctor) green in smoke

## 11.2 Commerce `PublicEndpoints.cs` (~371 LOC)

- [x] Split under `Endpoints/`:
  - [x] Public product → `PublicProductEndpoints.cs`
  - [x] Public portal → `PublicPortalEndpoints.cs`
  - [x] Public checkout + status → `PublicCheckoutEndpoints.cs`
  - [x] Public custom checkout → `PublicCustomCheckoutEndpoints.cs`
  - [x] Public arrears → `PublicArrearsEndpoints.cs`
- [x] Thin `MapPublicCommerceEndpoints` composer → `PublicEndpoints.cs` (~20 LOC)
- [x] Routes/policies unchanged

## 11.3 ProcessGatewayWebhookCommandHandler (~305 LOC)

- [x] Separate verify/log/emit stages (partials or helpers)  
  → orchestration + Metadata / Logging / Idempotency partials
- [x] Gateway-specific branches readable  
  → dispute / failed / completed still sequential in `HandleCoreAsync`
- [x] Webhook idempotency tests green  
  → `ProcessGatewayWebhookCommandHandlerTests` in smoke suite

## 11.4 LhdnGatewayAdapter (~383 LOC)

- [ ] Split by operation: token, submit, status, TIN, cancel, rate limit  
  → **deferred** this commit (webhook partials chosen as the optional third split)
- [ ] Keep port interface stable
- [ ] Module/LHDN gateway tests green (or smoke)

## 11.5 LlmOrchestratorService (continue partials)

- [ ] Review existing partials; finish non-stream vs stream separation if still tangled
- [ ] Tool execution vs title generation boundaries clear
- [ ] Ops tests green

## 11.6 Defer (P2 — only when touching area)

- [ ] Payment gateway adapters (Chip/Stripe/Billplz/Razorpay) — extract shared `ExtractName`/amount helpers first (see Phase 13)
- [ ] BillingQueryService partials
- [ ] B2cConsolidationJob phases
- [ ] Billing/Lhdn/Ops remaining endpoint monoliths (~210–246 LOC)

## 11.7 Exit criteria for each PR in this phase

- [x] Single commit focused on phase-11 required splits (public + payment-completed + webhook)
- [x] Behavior-preserving
- [x] Tests for that area green → smoke **34/34** (webhook + commerce product + gateway payment filters)
