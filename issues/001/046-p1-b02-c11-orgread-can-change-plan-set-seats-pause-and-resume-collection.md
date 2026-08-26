---
number: "046"
id: B02-C11
severity: P1
status: resolved
source: plans/009-bugs/02-commerce-subscriptions-billing-engine.md
head: "297ba98"
resolved_branch: fix/046-orgread-subscriber-writes
---

# 046 — B02-C11 — OrgRead can change plan, set seats, pause and resume collection

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/02-commerce-subscriptions-billing-engine.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/046-orgread-subscriber-writes`

Those four subscriber writes already require `OrgMember` (same as cancel). Authorization test pins the policies.

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B02-C11 — P1 — OrgRead can change plan, set seats, pause and resume collection

**Evidence.** `Endpoints.cs` 23: admin group `RequireAuthorization("OrgRead")`. `SubscriberEndpoints.cs` 157–243: change-plan, quantity, collection pause, collection resume have **no** extra policy. Cancel (98–116), keep (118–132), record-payment (134–155) are OrgMember. Anonymize is OrgAdmin.

**Repro.** Token with OrgRead only. `POST /admin/commerce/subscribers/{id}/collection/pause`. 200 paused. Same for change-plan and quantity.

**Blast radius.** Viewer / read-scoped API keys. Not anonymous (group is not AllowAnonymous). Still a write via a read policy.

**Tests.** `CommerceEndpointsAuthorizationTests.MapCommerceEndpoints_GetSubscribers_Requires_OrgRead` only. No test for the four write routes.

**Fix direction.** `.RequireAuthorization("OrgMember")` on those four, matching cancel.

---

