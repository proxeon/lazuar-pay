---
number: "127"
id: B08-M04
severity: P1
status: open
source: plans/009-bugs/08-communications-messaging-crm.md
head: "297ba98"
---

# 127 — B08-M04 — Unsubscribe row blocks later BOUNCE/COMPLAINT insert

- **Severity:** P1
- **Status:** open
- **Source:** `plans/009-bugs/08-communications-messaging-crm.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B08-M04 — P1 — Unsubscribe row blocks later BOUNCE/COMPLAINT insert

**Where:** `SuppressionService.SuppressAsync` 50–58; unique `(OrganizationId, Email)`; `IsSuppressedAsync` 34–45.

**What:** First reason wins. Marketing unsub first → transactional lane stays open → Resend bounce cannot upgrade the row.

**Why it matters:** The 008 P0 “unsub kills receipts” was correctly inverted into lanes. The leftover race undoes bounce protection for anyone who unsubscribed first. That is the common List-Unsubscribe-then-mailbox-gone sequence.

**Tests:** `SuppressionLaneTests` locks the lane matrix on a **pre-inserted** reason. Nothing inserts unsub then bounce.

---

