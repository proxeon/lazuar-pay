---
number: "232"
id: B05-L28
severity: P2
status: open
source: plans/009-bugs/05-billing-ledger-refunds-disputes.md
head: "297ba98"
---

# 232 — B05-L28 — `RefundedFee` is always 0

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/05-billing-ledger-refunds-disputes.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B05-L28 — P2 — `RefundedFee` is always 0

Mark-refunded hard-codes 0. Payments adapter success hard-codes 0 (“adapters currently do not return reclaimed fee”). Billing never reverses `EXPENSE_GATEWAY_FEE`. Matrix asserts −3 after a full refund. Fine if labelled. Not fine if we sell “exact gateway fees”.

---

## Evaluation (current tree, 2026-08-18)

### What the bug is
Every refund publisher in this tree still ships `RefundedFee = 0`, so Billing never posts a contra `EXPENSE_GATEWAY_FEE` on a live refund even though the sale booked the fee. Mark-refunded rails (Billplz / offline / cash) hard-code `RefundedFee: 0m` on `GatewayRefundCompletedIntegrationEvent`. The Payments adapter-success path still comments “adapters currently do not return reclaimed fee” and forces `refundedFee = 0m`. After 085, inbound `REFUND_COMPLETED` webhooks also publish Completed with `RefundedFee: 0m`. Billing’s refund writer *can* reverse the fee (`if (@event.RefundedFee > 0)`), so the journal is fee-aware; the publishers never give it a non-zero number. Ops dashboards therefore keep the original MDR as expense after a full refund (matrix net = −fee). That is honest only if we label “fees stay with us”; it is wrong if we sell “exact gateway fees” or if a processor actually returns a reclaimed fee.

### Still present?
**STILL BROKEN**

Mark-refunded still hard-codes zero:

```92:101:apps/lazuar-api/Modules/Commerce/Application/Commands/RecordRefundCommandHandler.cs
            await _eventBus.PublishAsync(new GatewayRefundCompletedIntegrationEvent(
                OrganizationId: request.OrganizationId,
                SubscriptionId: request.SubscriptionId ?? Guid.Empty,
                PaymentRecordId: log.Id,
                GatewayTransactionId: log.ExternalReference,
                RefundedAmount: amount,
                Currency: currency,
                RefundedFee: 0m,
                NetRefundedAmount: amount,
                TaxAmount: request.TaxAmount,
                IsFullRefund: isFullRefund));
```

Adapter success still hard-codes zero:

```51:53:apps/lazuar-api/Modules/Payments/Infrastructure/EventHandlers/GatewayRefundRequestedIntegrationEventHandler.cs
            // Gateway adapters currently do not return reclaimed fee; treat fee as 0 until webhook enrichment exists.
            var refundedFee = 0m;
            var netRefunded = @event.Amount - refundedFee;
```

Inbound refund webhooks (085) also force zero:

```236:248:apps/lazuar-api/Modules/Payments/Application/Commands/ProcessGatewayWebhookCommandHandler.cs
        if (parsedResult.EventType == "REFUND_COMPLETED")
        {
            var refunded = parsedResult.AmountPaid;
            var refundEvent = new GatewayRefundCompletedIntegrationEvent(
                ...
                RefundedFee: 0m,
                NetRefundedAmount: refunded,
```

Billing will reverse a fee if told to (`GatewayRefundCompletedHandler.cs:77-80`) but live events never set one. `LedgerBalanceMatrixTests.PaymentThenFullRefund_NetsRevenueToZeroGrossMinusFees` still seeds `RefundedFee: 0m` and asserts `summary.fees == 3m` / `summary.net == -3m`.

### Related files
- [`apps/lazuar-api/Modules/Commerce/Application/Commands/RecordRefundCommandHandler.cs`](apps/lazuar-api/Modules/Commerce/Application/Commands/RecordRefundCommandHandler.cs) — mark-refunded Completed publisher (`RefundedFee: 0m`).
- [`apps/lazuar-api/Modules/Payments/Infrastructure/EventHandlers/GatewayRefundRequestedIntegrationEventHandler.cs`](apps/lazuar-api/Modules/Payments/Infrastructure/EventHandlers/GatewayRefundRequestedIntegrationEventHandler.cs) — adapter-true Completed publisher; comment still says no reclaimed fee.
- [`apps/lazuar-api/Modules/Payments/Application/Commands/ProcessGatewayWebhookCommandHandler.cs`](apps/lazuar-api/Modules/Payments/Application/Commands/ProcessGatewayWebhookCommandHandler.cs) — inbound `REFUND_COMPLETED` also zeros the fee.
- [`apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/GatewayRefundCompletedHandler.cs`](apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/GatewayRefundCompletedHandler.cs) — only writer that *could* contra `EXPENSE_GATEWAY_FEE`.
- [`apps/lazuar-api/Modules/Payments/Contracts/Events/GatewayRefundCompletedIntegrationEvent.cs`](apps/lazuar-api/Modules/Payments/Contracts/Events/GatewayRefundCompletedIntegrationEvent.cs) — `RefundedFee` field exists; unused in practice.
- [`apps/lazuar-api/tests/Lazuar.ModuleTests/Billing/EventHandlers/LedgerBalanceMatrixTests.cs`](apps/lazuar-api/tests/Lazuar.ModuleTests/Billing/EventHandlers/LedgerBalanceMatrixTests.cs) — locks “fees remain after full refund”.
- [`apps/lazuar-api/tests/Lazuar.ModuleTests/Payments/GatewayRefundRequestedIntegrationEventHandlerTests.cs`](apps/lazuar-api/tests/Lazuar.ModuleTests/Payments/GatewayRefundRequestedIntegrationEventHandlerTests.cs) — `AdapterTrue_PublishesCompleted` asserts `RefundedFee == 0m`.
- [`apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/RecordRefundCommandHandlerTests.cs`](apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/RecordRefundCommandHandlerTests.cs) — mark-refunded path; does not assert fee.

### Tests
- Existing: `LedgerBalanceMatrixTests.PaymentThenFullRefund_NetsRevenueToZeroGrossMinusFees`; `GatewayRefundRequestedIntegrationEventHandlerTests.AdapterTrue_PublishesCompleted` (`RefundedFee == 0m`); `GatewayRefundCompletedHandlerTests` helper accepts `fee` but every call uses default `0m`; `ProcessGatewayWebhookCommandHandlerTests.Handle_RefundCompleted_Publishes_GatewayRefundCompleted` does not assert `RefundedFee`; `RecordRefundCommandHandlerTests.Handle_MarkRefunded_Billplz_PublishesCompleted_NotRequested` does not read `RefundedFee` from the outbox JSON.
- None of those fail while the bug is present. Two of them *lock* the zero-fee behaviour.
- First regression: publish a full refund of a sale that booked `EXPENSE_GATEWAY_FEE = 3` with `RefundedFee = 3` (or a parsed webhook fee) and assert the refund row has `EXPENSE_GATEWAY_FEE = −3` and summary fees net to 0. A second case should document the labelled policy if we deliberately keep fees.

### Reproduction today
Arrange a `GATEWAY_PAYMENT` of 108 / fee 3 / tax 8 (or let `GatewayPaymentCompletedHandler` book a Stripe hop-1 with `GatewayFee > 0`). Act: mark-refund Billplz via `RecordRefundCommand(MarkRefunded: true)`, or Stripe `IssueRefundAsync` success, or an inbound `REFUND_COMPLETED` webhook. Assert: `GatewayRefundCompleted.RefundedFee == 0`; refund journal has cash −108 (or −105 if fee were reversed — today cash is −108 because fee is 0) and **no** negative fee line; `GET /admin/billing/summary` still shows `total_gateway_fees = 3`.

### Blast radius
Merchants reading “Net Cash in Bank” / net-profit after refunds. Money books, not PII. Every full refund of a fee-bearing sale (Stripe GMV). Offline/Billplz mark-refunded is the same. Frequency: every refund. Severity stays P2 if product copy says fees are not reclaimed; it becomes a lying dashboard if we advertise exact MDR.

### Suggested fix
Smallest honest change: pick one policy and lock it. Either (a) keep `RefundedFee = 0` forever and change README / summary labels to “gateway fees are not reversed on refund”, or (b) plumb a real fee from Stripe Balance Transaction / refund webhook `GatewayFee` into `RefundedFee` and stop hard-coding 0 on the three publishers. Do not invent a Stripe Billing `subscription.updated`. Do not TypeSpec-regen. Do not reverse Hub `SYSTEM_SAAS_FEE` here (that is 240). If (b), only reverse up to the original sale’s fee (handler already `Min(RefundedFee, capped)`).

### Evaluation notes
Sibling of **239** (Billplz sale fee is also always 0, so there is often nothing to reverse). Residual after **085** (inbound refunds now land — and they also zero the fee). Matrix comment in the 009 audit still applies: the test is honest about “fees remain” and silent about selling exact fees. Still P2. Not blocked. No 161–200 fail-closed change touched these publishers.

