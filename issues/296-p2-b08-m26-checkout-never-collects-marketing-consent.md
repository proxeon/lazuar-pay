---
number: "296"
id: B08-M26
severity: P2
status: open
source: plans/009-bugs/08-communications-messaging-crm.md
head: "297ba98"
---

# 296 — B08-M26 — Checkout never collects marketing consent

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/08-communications-messaging-crm.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B08-M26 — P2 — Checkout never collects marketing consent

**Where:** `InitiateCheckoutCommand` has no consent; Resolve default `ConsentedToMarketing = false`; entity default false (`ConsentDefaultFalse` migration).

**What:** Correct PDPA default. Combined with B08-M18, broadcasts cannot reach hop-1 buyers without a back-door write. Not a “consent forced true” regression (that 007 gap is still closed).

---

