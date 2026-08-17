---
number: "203"
id: B03-C15
severity: P2
status: open
source: plans/009-bugs/03-commerce-dunning-arrears-portal.md
head: "297ba98"
---

# 203 — B03-C15 — Pre-dunning claim window is hardcoded 14 days

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/03-commerce-dunning-arrears-portal.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B03-C15 — P2 — Pre-dunning claim window is hardcoded 14 days

A −21 / −30 step cannot fire on time (`Claim.cs` 112, `PreDunning.cs` 36–38). First visibility is day −14, when the step catch-up-fires. Campaign builder does not warn.

**Fix.** Claim `NOW() + INTERVAL 'N days'` from `max(|negative offsets|)` among active campaigns, or store a per-org window.

---

