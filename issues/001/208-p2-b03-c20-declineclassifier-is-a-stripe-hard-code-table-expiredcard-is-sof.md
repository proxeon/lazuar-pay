---
number: "208"
id: B03-C20
severity: P2
status: resolved
resolved_branch: fix/208-expired-card-is-hard
source: plans/009-bugs/03-commerce-dunning-arrears-portal.md
head: "297ba98"
---

# 208 — B03-C20 — `DeclineClassifier` is a Stripe hard-code table; `expired_card` is soft

- **Severity:** P2
- **Status:** resolved
- **Source:** `plans/009-bugs/03-commerce-dunning-arrears-portal.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/208-expired-card-is-hard`

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B03-C20 — P2 — `DeclineClassifier` is a Stripe hard-code table; `expired_card` is soft

Hard: `incorrect_number`, `lost_card`, `pickup_card`, `stolen_card`, revocation pair, `authentication_required`, `highest_risk_level`, `transaction_not_allowed`. Soft: null, NSF, `card_declined`, **`expired_card`**, anything CHIP-shaped. `authentication_required` as HARD means 3DS cards never get AUTO_CHARGE 2–4 (maybe intended). CHIP codes all retry until max 4.

---

