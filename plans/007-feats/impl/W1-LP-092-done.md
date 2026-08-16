# W1-LP-092 — done

Partial refund is a remaining-amount machine, not a second full flip. Omitted amount is **remaining**. `0 < amount < remaining` completes as `PARTIALLY_REFUNDED` and can be refunded again until remaining hits 0. `amount > remaining` is `400 AMOUNT_EXCEEDS_REMAINING`. Billing posts one `GATEWAY_REFUND` per attempt (`PaymentRecordId:event.Id`). LHDN cancel/CN runs only when `IsFullRefund` (remaining → 0). Billplz mark-refunded of a slice leaves `PARTIALLY_REFUNDED`.

Built on the LP-091 persist / capability / failed-consumer foundation.

## Files changed

- `RecordRefundCommandHandler` — cap against `Amount − RefundedAmount`; allow `PARTIALLY_REFUNDED` and `REFUND_FAILED` as sources; omit = remaining.
- `CommerceTransactionLog.ApplyRefund` — accumulate; `PARTIALLY_REFUNDED` vs `REFUNDED`.
- Commerce completed — apply only from `REFUND_PENDING` (redelivery / mark path is a no-op).
- `GatewayRefundRequested` / `GatewayRefundCompleted` — `IsFullRefund`; Payments copies the flag.
- Billing `GatewayRefundCompletedHandler` — attempt-scoped reference id.
- LHDN refund handler — skip unless `IsFullRefund`.
- TypeSpec `refunded_amount` / `remaining_amount` on `TransactionLogDto`.

## Tests

- Command: partial pending + amount 40; omit after 40 uses 60; over remaining throws; from `PARTIALLY_REFUNDED` allowed; from `REFUNDED` rejected; Billplz mark of RM 20 stays partial.
- Commerce completed: slice → partial; second slice → refunded; not-pending redelivery does not double-add.
- Billing: same event id still one row; two event ids → two `GATEWAY_REFUND`; 50% tax unchanged.
- LHDN: `IsFullRefund=false` does not cancel; full + &lt;72h still cancels.

## Tests run

Same refund filter as LP-091 — **75 passed** (includes 092 command / completed / billing / LHDN cases).

Not committed. Not pushed.

Tracker `LP-092` **P → Y**. Inbound dashboard refunds remain `LP-PAY-022`. Credit-note product remains LP-104.
