---
number: "227"
id: B04-P26
severity: P2
status: open
source: plans/009-bugs/04-payments-adapters-webhooks.md
head: "297ba98"
---

# 227 — B04-P26 — Placeholder PII on generate

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/04-payments-adapters-webhooks.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B04-P26 — P2 — Placeholder PII on generate

**Where.** `GatewayCommon.PlaceholderEmail = "customer@example.com"`; CHIP/Billplz/Xendit `ResolveEmail`. Razorpay phone `+60100000000`.

**What.** Blank buyer email becomes a real processor customer record on the tenant account. Not a verify skip; it is a data-quality / support bug.

---

