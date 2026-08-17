---
number: "204"
id: B03-C16
severity: P2
status: open
source: plans/009-bugs/03-commerce-dunning-arrears-portal.md
head: "297ba98"
---

# 204 — B03-C16 — TRIALING is invisible to pre-dunning

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/03-commerce-dunning-arrears-portal.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B03-C16 — P2 — TRIALING is invisible to pre-dunning

Claim requires ACTIVE. Trials due in 3 days get no “trial ending” comms from this engine. Cancel works (008 P0-4 closed). Update-payment UI is hidden for TRIALING; POST would 400 anyway.

---

