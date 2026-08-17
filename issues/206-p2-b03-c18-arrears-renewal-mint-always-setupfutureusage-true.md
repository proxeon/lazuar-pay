---
number: "206"
id: B03-C18
severity: P2
status: open
source: plans/009-bugs/03-commerce-dunning-arrears-portal.md
head: "297ba98"
---

# 206 — B03-C18 — Arrears / renewal mint always `SetupFutureUsage: true`

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/03-commerce-dunning-arrears-portal.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B03-C18 — P2 — Arrears / renewal mint always `SetupFutureUsage: true`

`PublicArrearsEndpoints` 190, `RenewalCheckoutIssuer` 63. On Billplz/Xendit this is ignored. On Razorpay it is a card-registration link for a reminder-only product (008 payments report). PAST_DUE Billplz still hits this path.

---

