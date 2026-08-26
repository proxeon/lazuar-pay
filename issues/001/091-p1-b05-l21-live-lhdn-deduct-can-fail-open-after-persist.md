---
number: "091"
id: B05-L21
severity: P1
status: resolved
source: plans/009-bugs/05-billing-ledger-refunds-disputes.md
head: "297ba98"
resolved_branch: fix/091-lhdn-deduct-fail-closed
---

# 091 — B05-L21 — Live LHDN deduct can fail open after persist

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/05-billing-ledger-refunds-disputes.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/091-lhdn-deduct-fail-closed`

Live LHDN deduct runs before persist. A 402 no longer leaves a free submitted document.

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B05-L21 — P1 — Live LHDN deduct can fail open after persist

See §7. Sufficiency check then persist then deduct. Concurrent empty-wallet → logged, document kept. `LhdnSingleCreditPathTests` do not cover the catch. Meter can under-charge.

---

