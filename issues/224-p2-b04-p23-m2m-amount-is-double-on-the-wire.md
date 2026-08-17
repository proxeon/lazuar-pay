---
number: "224"
id: B04-P23
severity: P2
status: open
source: plans/009-bugs/04-payments-adapters-webhooks.md
head: "297ba98"
---

# 224 — B04-P23 — M2M amount is `double` on the wire

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/04-payments-adapters-webhooks.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B04-P23 — P2 — M2M amount is `double` on the wire

**Where.** `IntegrationEndpoints.cs:45` `(decimal)body.Amount`; response `(double)result.Amount` (`154`). NSwag DTO `CreateIntegrationCheckoutRequestDto.Amount` is `double`.

**What.** Binary floating point on money at the HTTP edge. Internal command is `decimal`. Typical MYR 2-dp values survive; 3-dp / repeating fractions do not.

