# Phase C acceptance notes (C.9 close-out)

**Date:** 2026-08-04  
**Branch:** `phase-c/operate-and-trust-it`  
**Checklist:** `plans/001-backend-solidification-checklist.md` Phase C

## Support: “was this payment fulfilled?”

| Layer | Where to look |
|-------|----------------|
| Logs | `Payment webhook processed successfully. EventId=… Provider=… GatewayTransactionId=… TenantId=… EventType=…` from `ProcessGatewayWebhookCommandHandler` |
| Payments | Webhook/event + business-key idempotency tables (dedupe already processed) |
| Billing | `billing.LedgerEntries` where `ReferenceType` ∈ (`GATEWAY_PAYMENT`, `GATEWAY_REFUND`, `SYSTEM_CREDIT_TOPUP`) and `ReferenceId` = gateway transaction id |
| Commerce | `commerce.TransactionLogs` by `ExternalReference` / customer email |

No single support “timeline” UI yet — SQL + structured logs are the supported path.

## Ops UI honesty (C.3)

- **Live:** cancel subscriber, record-payment, refund (gateway-backed), CSV export, portal cancel  
- **Gone:** ban status; portal magic-link / billing-link phantoms  
- **Residual:** offline refund without gateway ref; export row cap

## Horizontal scale (C.5)

Documented in `deploy/prod/README.md`: keep API/worker **replica=1** unless workers are claim-safe. Dunning/billing use `SKIP LOCKED` + per-subscription saves so two replicas do not silently double-cancel once claims are in play.

## Financial summary + refunds

`GetFinancialSummaryAsync` uses signed ledger sums (gross − contra refunds − fees − tax). Module matrix tests + optional Postgres integration test cover payment/refund/top-up polarity for ops dashboards.

## C.9 test map

| Item | Primary tests |
|------|----------------|
| Concurrent credit + idempotency | `CreditDeductionConcurrencyTests`, `DeductTenantCreditIdempotencyTests` |
| Ledger matrix | `LedgerBalanceMatrixTests`, refund/top-up handler tests |
| B2C eligibility | `B2cConsolidationJobTests` |
| Cross-tenant IDOR | `CrossTenantIdorTests`, `TenantIsolationHardeningTests` |
| Coupon lifecycle | `CouponLifecycleTests`, `CommerceProductCompletenessTests` |
| Architecture BB/SK | `ModuleBoundaryTests` BuildingBlocks/SharedKernel rules |
