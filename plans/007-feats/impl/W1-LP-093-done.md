# W1-LP-093 — done

Ops refund UI tells the truth. Transaction Logs detail and Subscriber ledger share `RefundModal`. Amount defaults to remaining. API rails say “Refund via {gateway}” and toast **Refund requested** (status stays `REFUND_PENDING` until the worker). Billplz / offline say **Mark refunded** with SOP copy and send `mark_refunded`. No empty `{}` body. No optimistic `REFUNDED`. Reason is persisted. List badges distinguish pending / partial / full / failed. Method column is `recorded_by_name` + `gateway_name` (`tx.payment_method` is gone). Gateway filter is wired.

Requires LP-091 persist + LP-092 remaining (amount box ships because remaining exists).

## Files changed

- `apps/lazuar-ops/src/modules/commerce/components/RefundModal.tsx` — new shared modal.
- `transactionStatus.ts` — remaining, refundable set, badge classes.
- `TransactionDetailPanel.tsx` — remaining breakdown; no optimistic REFUNDED; pending CTA disabled.
- `SubscribersPage.tsx` — same modal + `subscription_id`; retry / refund rest.
- `TransactionsPage.tsx` — status filters; gateway filter; method column; poll while pending.
- `CommerceQueryService.Transactions` — maps `gateway_name`, `refunded_amount`, `remaining_amount`, `supports_api_refund`; SQL filter on `GatewayName`.
- TypeSpec + `task gen` (`RecordRefundRequestDto.mark_refunded` / `reason`; DTO money flags).
- Command `Reason` → `log.RefundReason`.

## Tests

- `CommerceHonestyDtoTests.TransactionMap_*` — mapper fields.
- `RecordRefund` persists reason.
- `grep tx.payment_method` in ops = 0.

`npx tsc --noEmit -p apps/lazuar-ops/tsconfig.json` — clean.

Not committed. Not pushed.

Tracker `LP-093` **N → Y**. Checklist footnote “No ops refund button” deleted. Manual Stripe/CHIP/Billplz/fail demo not run here.
