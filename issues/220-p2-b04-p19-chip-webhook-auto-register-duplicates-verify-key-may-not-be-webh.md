---
number: "220"
id: B04-P19
severity: P2
status: open
source: plans/009-bugs/04-payments-adapters-webhooks.md
head: "297ba98"
---

# 220 — B04-P19 — CHIP webhook auto-register duplicates; verify key may not be `Webhook.public_key`

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/04-payments-adapters-webhooks.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B04-P19 — P2 — CHIP webhook auto-register duplicates; verify key may not be `Webhook.public_key`

**Where.** `UpdatePaymentConfigCommandHandler.cs:116-138`. No GET-list. PEM from `GET /public_key/` (company), not from the created webhook object. CHIP docs: webhook deliveries use a dedicated key pair.

**What.** Re-save → N webhook rows at CHIP, N deliveries of the same event (EventId dedupes after the first). Wrong PEM → every delivery `Verified=false` → 500. Unsoaked; residual.

