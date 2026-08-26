---
number: "085"
id: B05-L15
severity: P1
status: resolved
source: plans/009-bugs/05-billing-ledger-refunds-disputes.md
head: "297ba98"
resolved_branch: fix/085-inbound-refund-webhooks
---

# 085 — B05-L15 — Inbound refund webhooks are dropped; Stripe `pending` is terminal

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/05-billing-ledger-refunds-disputes.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/085-inbound-refund-webhooks`

Succeeded Stripe `refund.updated` / `charge.refunded` publish `GatewayRefundCompleted`. Pending is not success (070). Commerce applies dashboard refunds that never went through `REFUND_PENDING`.

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B05-L15 — P1 — Inbound refund webhooks are dropped; Stripe `pending` is terminal

`ProcessGatewayWebhookCommandHandler.cs:83-88` only accepts `PAYMENT_COMPLETED`, `DISPUTE_CREATED`, `PAYMENT_FAILED`. Stripe Dashboard / customer-portal / Radar / `charge.refunded` / `refund.updated` never move Commerce or Billing unless someone hits `RecordRefund`.

`StripeGatewayAdapter.IssueRefundAsync` (`:313`) returns true for `pending`. We publish Completed immediately. If Stripe later fails the pending refund, we have already booked Commerce + Billing as refunded. No unwind.

008 P0-3. Still open. Payments ingress is slice 04’s HTTP; the **money lie** is this slice.

---

