---
number: "226"
id: B04-P25
severity: P2
status: open
source: plans/009-bugs/04-payments-adapters-webhooks.md
head: "297ba98"
---

# 226 — B04-P25 — Integration checkout GET lazy-expires only while `open`

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/04-payments-adapters-webhooks.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B04-P25 — P2 — Integration checkout GET lazy-expires only while `open`

**Where.** `GetIntegrationCheckoutQueryHandler.cs:31-35`; `TryExpireIfPast` (`IntegrationCheckoutSession.cs:125-134`).

**What.** A `failed` session past TTL stays `failed` (good). An `open` session past 24h becomes `expired` on GET. Webhooks after expire: M2M handler still requires `open` — a late pay on an expired session is dropped. Buyer can pay a 25-hour-old bill; M2M outbound never fires. Related to B04-P02 (terminal states swallow completed).

