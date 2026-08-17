---
number: "068"
id: B04-P11
severity: P1
status: open
source: plans/009-bugs/04-payments-adapters-webhooks.md
head: "297ba98"
---

# 068 — B04-P11 — Razorpay `SetupFutureUsage` still mints a card registration link

- **Severity:** P1
- **Status:** open
- **Source:** `plans/009-bugs/04-payments-adapters-webhooks.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B04-P11 — P1 — Razorpay `SetupFutureUsage` still mints a card registration link

**Where.** `RazorpayGatewayAdapter.cs:58-82`. `SupportsOffSession("RAZORPAY")` is false. `SupportsEmandate` is false. `method` is `"card"`.

**What.** The adapter does what the port asks. The product says reminder-only. Hop-2 is a card-registration UX whose tokens Commerce refuses to persist (Commerce, out of scope). At this layer: we claim e-mandate nowhere in C#; we still run the registration-link API. `max_amount = amountPaise * 10` authorizes 10× the first charge as the card-mandate ceiling.

