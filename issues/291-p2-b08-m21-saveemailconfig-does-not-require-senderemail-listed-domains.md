---
number: "291"
id: B08-M21
severity: P2
status: open
source: plans/009-bugs/08-communications-messaging-crm.md
head: "297ba98"
---

# 291 — B08-M21 — SaveEmailConfig does not require SenderEmail ∈ listed domains

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/08-communications-messaging-crm.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B08-M21 — P2 — SaveEmailConfig does not require SenderEmail ∈ listed domains

**Where:** `SaveEmailConfigCommand.cs` 73–81.

**What:** 008 recorded this. Still true. Key that can `GET /domains` + `from: gmail.com` saves. Checkout gate then goes green.

---

