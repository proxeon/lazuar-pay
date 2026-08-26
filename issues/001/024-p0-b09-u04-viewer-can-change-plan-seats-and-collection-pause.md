---
number: "024"
id: B09-U04
severity: P0
status: resolved
source: plans/009-bugs/09-frontends-ops-portal-admin.md
head: "297ba98"
resolved_branch: fix/024-viewer-cannot-change-plan
---

# 024 — B09-U04 — Viewer can change plan, seats, and collection pause

- **Severity:** P0
- **Status:** resolved
- **Source:** `plans/009-bugs/09-frontends-ops-portal-admin.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/024-viewer-cannot-change-plan`

Plan/seats/collection and subscriber export require OrgMember. Viewer UI no longer shows those actions.

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

#### B09-U04 — Viewer can change plan, seats, and collection pause (P0)

**Where:** `SubscribersPage.tsx` 574–634, 140–204; `SubscriberEndpoints.cs` 157–243 (no OrgMember); `TeamPage.tsx` 62.  
**What:** Team copy: “Viewers can only read.” Member Console: Schedule / Revert / Set seats / Pause collection are enabled. Those four POSTs sit on the OrgRead group. Viewer write is 200.  
**Walk:** Invite a contractor as Viewer. They open a subscriber. They schedule a plan change. Next billing date the customer is on a different product.  
This is the UI exposing an authorization hole. It is also an API hole. This slice owns the painted buttons.

CSV export is the same OrgRead group (`82:104`). Viewer walks out with the subscriber file. Filed with U04 as the PII half of “Viewer can click forbidden actions.”

### P1 — chrome that lies, lockouts, open redirects, cancel that does not cancel

