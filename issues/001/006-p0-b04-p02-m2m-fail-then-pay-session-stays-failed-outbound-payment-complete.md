---
number: "006"
id: B04-P02
severity: P0
status: resolved
source: plans/009-bugs/04-payments-adapters-webhooks.md
head: "297ba98"
resolved_branch: fix/006-m2m-fail-then-pay
---

# 006 — B04-P02 — M2M fail-then-pay: session stays `failed`, outbound `payment.completed` never sent

- **Severity:** P0
- **Status:** resolved
- **Source:** `plans/009-bugs/04-payments-adapters-webhooks.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/006-m2m-fail-then-pay`

`payment.completed` now recovers a `failed` (or expired) M2M session. Already-`completed` stays idempotent. Fail-then-pay publishes `payment.failed` then `payment.completed`.

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B04-P02 — P0 — M2M fail-then-pay: session stays `failed`, outbound `payment.completed` never sent

**Where.** `IntegrationCheckoutGatewayEventsHandler.cs:59-66` (completed only if `Status == open`); `89-108` (fail marks `failed` while open); `IntegrationCheckoutSession.MarkFailed` (`104-108`) has no “already completed” guard because the handler checks status first.

**What.** After `a1afc09`, `ProcessGatewayWebhookCommandHandler` publishes **both** `GatewayPaymentFailed` and `GatewayPaymentCompleted` for the same CHIP purchase / Billplz bill. The M2M handler consumes failed first (or any unpaid Billplz `due` callback): `MarkFailed`, outbound `payment.failed`. Completed arrives: status is not `open`, debug log “skipping duplicate payment.completed”, **return**. Integrator is told the checkout failed. Buyer paid.

**Why the EventId fix made this visible.** Before `a1afc09`, completed was dropped at the log and M2M never saw it. The log layer is now correct. The session state machine still treats fail as terminal.

**Pay-then-fail is safe here** (completed first → fail skipped). Fail-then-pay is not.

**Test that lies by omission.** `IntegrationCheckoutOutboundWebhookTests` has `Failed_AlreadyFailed_NoSecondPublish` and `Completed_AlreadyCompleted_NoSecondPublish` and **no** `Failed_ThenCompleted_MarksCompleted`. The new handler test `Handle_FailThenPay_SameObject_PublishesFailedAndCompleted` stops at the outbox and never instantiates `IntegrationCheckoutGatewayEventsHandler`.

