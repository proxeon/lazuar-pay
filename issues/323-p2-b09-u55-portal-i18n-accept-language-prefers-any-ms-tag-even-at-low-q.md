---
number: "323"
id: B09-U55
severity: P2
status: open
source: plans/009-bugs/09-frontends-ops-portal-admin.md
head: "297ba98"
---

# 323 — B09-U55 — Portal i18n Accept-Language prefers any `ms` tag even at low q

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/09-frontends-ops-portal-admin.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

#### B09-U55 — Portal i18n Accept-Language prefers any `ms` tag even at low q (P2)

`i18n.test.mjs` 72–78 asserts this. A `en-US,en;q=0.9,ms-MY;q=0.8` browser gets BM. Product decision encoded as a test; easy to call a bug later.

