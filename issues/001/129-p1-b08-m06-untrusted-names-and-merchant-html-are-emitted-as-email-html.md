---
number: "129"
id: B08-M06
severity: P1
status: resolved
resolved_branch: fix/129-email-html-encode
source: plans/009-bugs/08-communications-messaging-crm.md
head: "297ba98"
---

# 129 — B08-M06 — Untrusted names and merchant HTML are emitted as email HTML

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/08-communications-messaging-crm.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/129-email-html-encode`

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B08-M06 — P1 — Untrusted names and merchant HTML are emitted as email HTML

**Where:** `MessageTemplateHydrator.Populate`; `MarkdownParser` pipeline; `UpdateMessageTemplateCommandHandler` 99–108; `DocumentPublishedIntegrationEventHandler` 74–77; `BroadcastFanoutJob` 174–179.

**What:** No HTML encode. Markdig raw HTML on. Update skips tag validation. Document handler interpolates `CustomerName` then Markdown-parses. Broadcasts are raw HTML.

**Why it matters:** Checkout `Name` is attacker-controlled. Stored XSS in the buyer’s (and the merchant’s preview) mailbox. Phishing links via `javascript:` or extra `<a>` tags. This is not “merchants can brand their mail.” This is buyer input in a privileged HTML context.

---

