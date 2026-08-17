---
number: "092"
id: B05-L22
severity: P1
status: resolved
source: plans/009-bugs/05-billing-ledger-refunds-disputes.md
head: "297ba98"
resolved_branch: fix/092-pdf-retry-after-processed
---

# 092 — B05-L22 — PDF after `SaveChanges` is not retried

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/05-billing-ledger-refunds-disputes.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/092-pdf-retry-after-processed`

A retried payment handler still generates the receipt/invoice PDF after `HasEntryBeenProcessed`.

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B05-L22 — P1 — PDF after `SaveChanges` is not retried

`GatewayPaymentCompletedHandler` and `ManualSubscriberEnrolledIntegrationEventHandler` save first (correct: number must exist), then PDF. Retry hits `HasEntryBeenProcessed` and returns. Receipt/invoice number exists; R2 object may not. Email `DocumentPublished` never fires. Operator sees `PENDING` / broken download. `HandleAsync_WhenB2C_SavesChangesBeforeGeneratingDocument` **locks this order** and does not lock a retry-generates-PDF property.

---

