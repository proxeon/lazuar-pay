---
number: "007"
id: B05-L01
severity: P0
status: resolved
source: plans/009-bugs/05-billing-ledger-refunds-disputes.md
head: "297ba98"
resolved_branch: fix/007-lhdn-cancel-skip-if-refunded
---

# 007 — B05-L01 — Full B2B refund ≤72h double-reverses cash and tax

- **Severity:** P0
- **Status:** resolved
- **Source:** `plans/009-bugs/05-billing-ledger-refunds-disputes.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/007-lhdn-cancel-skip-if-refunded`

If a `GATEWAY_REFUND` already exists for the same gateway tx, LHDN cancel only stamps `CANCELLED` on the sale. It does not post a second cash/tax contra. Cancel without a refund still posts `LHDN_CANCELLATION`.

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B05-L01 — P0 — Full B2B refund ≤72h double-reverses cash and tax

**Where.** `GatewayRefundCompletedHandler` already contra’d the sale. Lhdn `GatewayRefundCompletedIntegrationEventHandler` on `IsFullRefund` and `hoursSinceValidation <= 72` sends `CancelTaxDocumentCommand`. That command, on MyInvois success (`CancelTaxDocumentCommand.cs:61`), publishes `LhdnDocumentCancelledIntegrationEvent`. Billing `LhdnDocumentCancelledIntegrationEventHandler` finds the **original payment** by INV / `TaxInvoiceId` / `ReferenceId` and posts `LHDN_CANCELLATION` that negates **every** original line. It does not look for an existing `GATEWAY_REFUND` on the same gateway tx.

**Walk** (108 / 8 tax / 3 fee):

| Account | Payment | `GATEWAY_REFUND` | `LHDN_CANCELLATION` | Net |
|---------|---------|------------------|---------------------|-----|
| `ASSET_CASH` | +105 | −108 | −105 | **−108** |
| `EXPENSE_GATEWAY_FEE` | +3 | 0 | −3 | 0 |
| `REVENUE_GROSS` | −100 | 0 | +100 | 0 |
| `CONTRA_REVENUE_REFUNDS` | 0 | +100 | 0 | +100 |
| `LIABILITY_TAX_PAYABLE` | −8 | +8 | +8 | **+8** |

Cash looks like we paid the customer twice. Tax payable flips sign (we appear to have a tax **asset**). Summary net uses `−SUM(REVENUE_GROSS) − SUM(CONTRA) − fees − (−SUM(TAX))` → `0 − 100 − 0 − (−(+8))` = **−108**. Garbage.

≥72h uses Submit CN, not cancel, so this particular double reverse does not fire. The 72h window is the legally preferred IRBM path.

**Tests.** There is **no** `LhdnDocumentCancelledIntegrationEventHandler` test class. There is no matrix test payment → refund → cancel. `LedgerBalanceMatrixTests.PaymentThenFullRefund_*` stops at the refund.

**008.** P0-2. **Still open.**

---

