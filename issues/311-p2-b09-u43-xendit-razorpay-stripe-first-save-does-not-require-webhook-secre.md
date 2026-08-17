---
number: "311"
id: B09-U43
severity: P2
status: open
source: plans/009-bugs/09-frontends-ops-portal-admin.md
head: "297ba98"
---

# 311 — B09-U43 — Xendit/Razorpay/Stripe first-save does not require webhook secret

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/09-frontends-ops-portal-admin.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

#### B09-U43 — Xendit/Razorpay/Stripe first-save does not require webhook secret (P2)

Vaults accept a key without a callback/whsec. First Billplz *does* require 128-char X-Signature. Inconsistent; Xendit webhooks will 401 until they come back.

