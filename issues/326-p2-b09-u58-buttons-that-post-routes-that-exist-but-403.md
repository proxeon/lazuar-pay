---
number: "326"
id: B09-U58
severity: P2
status: open
source: plans/009-bugs/09-frontends-ops-portal-admin.md
head: "297ba98"
---

# 326 — B09-U58 — Buttons that POST routes that exist but 403

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/09-frontends-ops-portal-admin.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

#### B09-U58 — Buttons that POST routes that exist but 403 (P2 inventory)

Not missing routes. Catalog of painted writes that are not Viewer-legal: refund, cancel, keep, record-payment, anonymize, invite, remove, save vault, save email, save legal, Check TIN, create quote, mark paid, create coupon, deploy dunning, create template (WhatsApp required), create API key, create webhook, rotate secret, redeliver, SaaS pay, credit top-up, create product. Failure = toast. This is U14’s inventory.

No live button in the three apps POSTs a path that 404s at the API, except the unrouted chat island (`/ops/execute-action`, `/ops/chat/conversations/...`) which is not mounted.

---

