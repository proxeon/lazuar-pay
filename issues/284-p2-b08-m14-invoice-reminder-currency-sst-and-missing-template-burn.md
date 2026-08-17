---
number: "284"
id: B08-M14
severity: P2
status: open
source: plans/009-bugs/08-communications-messaging-crm.md
head: "297ba98"
---

# 284 — B08-M14 — Invoice reminder currency/SST and missing-template burn

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/08-communications-messaging-crm.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B08-M14 — P2 — Invoice reminder currency/SST and missing-template burn

**Where:** `InvoiceReminderJob.cs` 108–118; hydrate 147–154; job writes the dispatch log in the same loop after publish (133).

**What:** `currency = "MYR"` always. No SST field on ad-hoc lines (matches hop-1 custom charge — consistent, still a lie if they ever add SST to quotes). Missing template: Communications returns; Commerce already recorded the offset. Exact-day only, UTC, no catch-up (pre-existing 008).

No hydrator test for `EventType == "invoice.reminder"`.

---

