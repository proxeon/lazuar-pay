---
number: "297"
id: B08-M27
severity: P2
status: open
source: plans/009-bugs/08-communications-messaging-crm.md
head: "297ba98"
---

# 297 — B08-M27 — Dual CMS and leftover `reminder.due`

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/08-communications-messaging-crm.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B08-M27 — P2 — Dual CMS and leftover `reminder.due`

**Where:** `DunningStepDispatcher` sends step copy, not catalog “Payment Failed”; hydrate still implements `reminder.due` + `template_id` (FulfillmentRequested 51, 161–172); no job publishes it.

**What:** Editing Templates → Payment Failed does not change day-0 dunning. 008 called this Chargebee-shaped debt. Still true. Not a functional defect unless someone sells “one template.” `reminder.due` is dead code path.

---

