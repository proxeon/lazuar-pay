# Phase 11 — Additional god-file splits (P1)

**Goal:** Continue house-style splits when capacity allows.  
**Do as separate PRs per file** (not one mega PR).  
**Evidence:** `../02-large-files-chunking.md` §3.4–3.9

---

## 11.1 GatewayPaymentCompletedIntegrationEventHandler (~375 LOC)

- [ ] Inventory: checkout complete vs subscription payment vs logging
- [ ] Split into focused handlers **or** private methods/files with clear names
- [ ] Keep integration event type and subscription registration stable
- [ ] Tests for payment-completed paths green

## 11.2 Commerce `PublicEndpoints.cs` (~371 LOC)

- [ ] Split under `Endpoints/`:
  - [ ] Public product
  - [ ] Public portal
  - [ ] Public checkout + status
  - [ ] Public custom checkout
  - [ ] Public arrears
- [ ] Thin `MapPublicCommerceEndpoints` composer
- [ ] Routes/policies unchanged

## 11.3 ProcessGatewayWebhookCommandHandler (~305 LOC)

- [ ] Separate verify/log/emit stages (partials or helpers)
- [ ] Gateway-specific branches readable
- [ ] Webhook idempotency tests green

## 11.4 LhdnGatewayAdapter (~383 LOC)

- [ ] Split by operation: token, submit, status, TIN, cancel, rate limit
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

- [ ] Single file/PR focused
- [ ] Behavior-preserving
- [ ] Tests for that area green
