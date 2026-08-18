---
number: "179"
id: B10-X23
severity: P1
status: resolved
resolved_branch: fix/179-delivery-log-tenant
source: plans/009-bugs/10-tenancy-workers-contracts-tests.md
head: "297ba98"
---

# 179 — B10-X23 — Child / log tables with `OrganizationId` (or session id) and no tenant filter

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/10-tenancy-workers-contracts-tests.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/179-delivery-log-tenant`

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B10-X23 — P1 — Child / log tables with `OrganizationId` (or session id) and no tenant filter

`ChargeAttemptLog : Entity` — no org column. Count-by-subscription works globally. A guessed `SubscriptionId` + date is not HTTP-reachable today; workers see all orgs by design.

`InvoiceReminderDispatchLog` — `(SessionId, DayOffset)` only. Unique lock is global. Fine.

`MessageDeliveryLog` has `OrganizationId` and is **not** `IMustHaveTenant`. `GET /messaging/delivery-logs` filters by `ctx.TenantId` in LINQ. Any other `DbSet<MessageDeliveryLog>` query with empty ambient sees **all tenants’** recipient addresses. Architecture tests do not require the interface.

`PaymentWebhookLog` is intentionally global (provider EventId idempotency). Forensics are not org-partitioned. 008 noted this; still true.

