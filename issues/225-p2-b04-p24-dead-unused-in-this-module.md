---
number: "225"
id: B04-P24
severity: P2
status: open
source: plans/009-bugs/04-payments-adapters-webhooks.md
head: "297ba98"
---

# 225 — B04-P24 — Dead / unused in this module

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/04-payments-adapters-webhooks.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B04-P24 — P2 — Dead / unused in this module

- `ChipCollectGatewayAdapter._configuration` (injected, never read).
- `BillplzPublicBase.ProductionHosts` (filled, discarded).
- `SupportsDuitNowQr` / `SupportsHostedWallet` / `SupportsEmandate` (no Payments readers).
- `xendit_payment_methods` (no Payments/Commerce setter).
- Razorpay `ChargeOffSessionAsync` email/phone branch.
- Estimated fee parameters on `ParseWebhookAsync` (handler always 0).
- Payments README §3 “stateless checkouts”, §6 two adapters, overview “FPX, Curlec”.

