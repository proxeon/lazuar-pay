---
number: "298"
id: B08-M28
severity: P2
status: open
source: plans/009-bugs/08-communications-messaging-crm.md
head: "297ba98"
---

# 298 — B08-M28 — Brand wrapper still injects `<br/>` into HTML

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/08-communications-messaging-crm.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B08-M28 — P2 — Brand wrapper still injects `<br/>` into HTML

**Where:** `EmailTemplateBuilder.cs` 16; tests assert it.

**What:** Markdown already emitted `<p>`. Extra `<br/>` is ugly, not a security issue. Locked in by tests — a future cleanup will fail them on purpose.

---

