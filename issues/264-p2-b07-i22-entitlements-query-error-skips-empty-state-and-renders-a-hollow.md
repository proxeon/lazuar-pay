---
number: "264"
id: B07-I22
severity: P2
status: open
source: plans/009-bugs/07-one-identity-invites-keys.md
head: "297ba98"
---

# 264 — B07-I22 — Entitlements query error skips empty-state and renders a hollow shell

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/07-one-identity-invites-keys.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B07-I22 — P2 — Entitlements query error skips empty-state and renders a hollow shell

**Where.** `App.tsx:81–89, 123–157`.

**What.** `useQuery` error → `data` undefined → not `length === 0` → chrome with `[]` entitlements and whatever `ops_active_workspace_id` still says. Not the LP-184 empty-state. Not Access Denied. A failed One query looks like a logged-in product with no workspace switcher.

