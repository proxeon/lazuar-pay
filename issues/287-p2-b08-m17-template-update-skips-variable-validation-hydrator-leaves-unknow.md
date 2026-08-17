---
number: "287"
id: B08-M17
severity: P2
status: open
source: plans/009-bugs/08-communications-messaging-crm.md
head: "297ba98"
---

# 287 — B08-M17 — Template update skips variable validation; hydrator leaves unknown tags

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/08-communications-messaging-crm.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B08-M17 — P2 — Template update skips variable validation; hydrator leaves unknown tags

**Where:** `UpdateMessageTemplateCommandHandler` 99–108 vs `CreateMessageTemplateCommandHandler` 27, 47–87; `MessageTemplateHydratorTests` 59–63 (locks the leftover).

**What:** Create is strict. Update is a content dump. `{{garbage}}` ships. `{{fulfillment_url}}` and `{{document_link}}` are not in the shared hydrator at all — only in two local replace loops. A dunning step that copies those tags from the wiki’s fulfillment section will send the raw tag.

---

