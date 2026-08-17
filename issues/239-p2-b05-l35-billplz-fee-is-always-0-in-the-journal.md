---
number: "239"
id: B05-L35
severity: P2
status: open
source: plans/009-bugs/05-billing-ledger-refunds-disputes.md
head: "297ba98"
---

# 239 — B05-L35 — Billplz fee is always 0 in the journal

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/05-billing-ledger-refunds-disputes.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B05-L35 — P2 — Billplz fee is always 0 in the journal

Adapter formula uses `estimatedFeePercentage` / `fixedFee`. Webhook handler always passes 0, 0, 0 (`ProcessGatewayWebhookCommandHandler:74-76`). Cash = full paid. Payout CSV will not match. Same class of honesty hole as B05-L28.

---

