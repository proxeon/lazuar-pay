# W1-LP-091 — done

Full refund is a real money loop. `POST /admin/commerce/transactions/{id}/refund` now commits the Commerce outbox and sets `REFUND_PENDING` before Payments runs `IssueRefundAsync`. No default `STRIPE`. Missing gateway is `400 GATEWAY_REQUIRED`. Billplz / offline require `mark_refunded` and publish `GatewayRefundCompleted` without calling an adapter. Adapter false / missing config flips the log to `REFUND_FAILED` (retryable). HTTP 400 is RFC 7807 `ProblemDetails` (`title` = code, `detail` = message).

Landed with LP-092 remaining and LP-093 ops modal in the same series.

## Files changed

### Domain / persistence

- `CommerceTransactionLog` — `GatewayName`, `RefundedAmount`, `RefundReason`; `MarkRefundPending` / `MarkRefundFailed` / `ApplyRefund`.
- Commerce EF + `20260817180000_AddTransactionRefundFields` — nullable gateway, refunded amount default 0.
- New logs stamp `GatewayName` (product/session on gateway paid; `OFFLINE` on record-payment / mark-offline / manual enroll).

### Command / HTTP

- `RecordRefundCommand` — `MarkRefunded`, `Reason`; returns `refund_requested` | `refunded`.
- `RecordRefundCommandHandler` — resolve gateway, pending guard, persist after every `PublishAsync` on `OutboxEventBus<CommerceDbContext>`.
- `TransactionEndpoints` — `mark_refunded` / `reason`; ProblemDetails on 400.

### Payments / Commerce consumers

- `PaymentGatewayCapabilities.SupportsApiRefund` / `RequiresMarkRefunded`.
- `GatewayRefundFailedIntegrationEventHandler` + DI subscribe.
- Completed handler applies only from `REFUND_PENDING`.
- Stripe refund idempotency key `lazuar-refund:{pi}:{minor}`.
- Billplz `IssueRefundAsync` stays `false` (comment: not Payment Order).

### Tests

- `RecordRefundCommandHandlerTests` — persist, no Stripe default, CHIP stamp, reject pending/refunded/Billplz/offline, mark-refunded completed event.
- `GatewayRefundRequestedIntegrationEventHandlerTests` — missing config, amount ≤ 0, adapter true/false, soft-disable still refunds.
- Commerce completed/failed handler tests.
- Billplz refund lock; CHIP refund POST; capabilities.

## Tests run

- `Lazuar.ModuleTests` filter `RecordRefundCommandHandlerTests|GatewayRefundCompletedIntegrationEventHandlerTests|GatewayRefundRequestedIntegrationEventHandlerTests|PaymentGatewayCapabilitiesTests|GatewayRefundCompletedHandlerTests|CommerceHonestyDtoTests|LedgerBalanceMatrixTests|RecordRefund_ForeignOrg|IssueRefundAsync|FormatRefundIdempotencyKey` — **75 passed**.
- After TypeSpec gen, `RecordRefundCommandHandlerTests|CommerceHonestyDtoTests|GatewayRefundCompletedHandlerTests` — **26 passed**.

Not committed. Not pushed.

Tracker `LP-091` **P → Y**. `LP-PAY-009` / `LP-PAY-022` unchanged.
