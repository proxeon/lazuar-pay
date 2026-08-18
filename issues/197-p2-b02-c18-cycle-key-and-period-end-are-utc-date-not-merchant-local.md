---
number: "197"
id: B02-C18
severity: P2
status: resolved
resolved_branch: fix/197-cycle-key-utc
source: plans/009-bugs/02-commerce-subscriptions-billing-engine.md
head: "297ba98"
---

# 197 — B02-C18 — Cycle key and “period end” are UTC Date, not merchant local

- **Severity:** P2
- **Status:** resolved
- **Source:** `plans/009-bugs/02-commerce-subscriptions-billing-engine.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/197-cycle-key-utc`

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B02-C18 — P2 — Cycle key and “period end” are UTC Date, not merchant local

**Evidence.** `NextBillingDate!.Value.Date` for ChargeAttemptLog; `SetCurrentRenewalCheckout` stores `.Date` UTC; claim uses full timestamptz. 2026-09-01 00:00 UTC is 08:00 MYT. **Speculation:** merchants say “bill on the 1st” in MYT. Document UTC or store a merchant-TZ date.

---

