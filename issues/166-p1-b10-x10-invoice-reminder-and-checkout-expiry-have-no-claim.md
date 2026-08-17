---
number: "166"
id: B10-X10
severity: P1
status: open
source: plans/009-bugs/10-tenancy-workers-contracts-tests.md
head: "297ba98"
---

# 166 — B10-X10 — Invoice reminder and checkout expiry have no claim

- **Severity:** P1
- **Status:** open
- **Source:** `plans/009-bugs/10-tenancy-workers-contracts-tests.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B10-X10 — P1 — Invoice reminder and checkout expiry have no claim

`CheckoutSessionExpiryJob` loads every OPEN session with `ExpiresAt < now` (`IgnoreQueryFilters`), expires them, releases coupons, one `SaveChanges`. Two API replicas on the same 5-minute tick both expire the same set. `Expire()` is likely idempotent; `ReleaseReservation()` is **not** — coupon remaining uses can increment twice.

`InvoiceReminderJob` loads every OPEN custom session with `DueAt != null` (unbounded), computes UTC day offset, inserts `InvoiceReminderDispatchLog`. Unique index `(SessionId, DayOffset)` is the only interlock. Two replicas: one `SaveChanges` throws; the fulfillment event may already be in the Commerce outbox of **both** if `PublishAsync` ran before save. The job publishes **then** adds the log **then** saves once at the end. A mid-loop exception loses the log insert but keeps in-memory outbox entries that were never saved — actually `PublishAsync` only stages on the same `CommerceDbContext`, so a throw before `SaveChanges` loses both. Two successful replicas: both stage outbox + log; unique index fails one `SaveChanges`; the winner’s outbox commits. The loser rolls back. That is OK **if** EF InMemory is not production. On Postgres the unique violation is an exception in `SaveChanges` — the **entire** batch of reminders in that process rolls back, including ones that did not collide, because there is one save for the whole job.

No SKIP LOCKED. No per-session transaction. Tests: 3 reminder tests (in-process double `RunOnce` is unique-index safe); expiry is one method inside `CommerceProductCompletenessTests`.

