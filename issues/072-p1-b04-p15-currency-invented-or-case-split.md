---
number: "072"
id: B04-P15
severity: P1
status: open
source: plans/009-bugs/04-payments-adapters-webhooks.md
head: "297ba98"
---

# 072 — B04-P15 — Currency invented or case-split

- **Severity:** P1
- **Status:** open
- **Source:** `plans/009-bugs/04-payments-adapters-webhooks.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B04-P15 — P1 — Currency invented or case-split

| Rail | Generate | Webhook |
|------|----------|---------|
| Stripe | `ToLowerInvariant()` | `session.Currency ?? "myr"` (invent + lower) |
| CHIP | unused on generate | `purchase.currency ?? "MYR"` (invent) |
| Billplz | unused | hardcoded `"MYR"` |
| Razorpay | `ToUpperInvariant()` | fail closed, then `ToUpperInvariant()` |
| Xendit | `(currency ?? "MYR").ToUpperInvariant()` (invent on generate) | fail closed, then upper |

**What.** Stripe/CHIP invent a currency when the processor omitted one. Razorpay/Xendit webhook refuse. Stripe events are lowercase `myr`; everyone else tends to `MYR`. This module publishes the string as-is. Case-sensitive consumers (ledger, tax) are other slices; the cashier is the source of the split.

