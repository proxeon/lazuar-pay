---
number: "086"
id: B05-L16
severity: P1
status: resolved
source: plans/009-bugs/05-billing-ledger-refunds-disputes.md
head: "297ba98"
resolved_branch: fix/086-lost-chargeback-journal
---

# 086 — B05-L16 — Lost GMV chargeback never journals unless ops refunds

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/05-billing-ledger-refunds-disputes.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/086-lost-chargeback-journal`

`DISPUTE_CLOSED` is allow-listed. A lost GMV dispute books `GATEWAY_DISPUTE` unless a refund already reversed the sale.

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B05-L16 — P1 — Lost GMV chargeback never journals unless ops refunds

After `e18edbe` this is the remaining chargeback hole, not a double reverse. OPEN forever. No won/lost. No `GATEWAY_DISPUTE`. Stripe loss that auto-refunds at the processor is B05-L15. Access stays `ACTIVE` (`HasOpenDispute` is a bit, not a gate). Books stay sold.

---

