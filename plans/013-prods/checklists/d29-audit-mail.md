# D29 — `audit_events` (+ optional `mail_outbox`)

**Track:** Database · **Depends:** D16  
**Analysis:** [03](../03-host-production-seams.md), [09](../09-data-migration.md)  
**Goal:** NP-AUD-001. Audit (and optional mail) in the **same** Pay database. Not Notify. Not `communications`.

---

## D29.1 Audit

- [x] `audit_events`: `org_id`, `action`, `at`, `payload` **small**
- [x] Same database as charges / journal / receipts
- [x] Insert in the money transaction is F16 — this phase is the table

## D29.2 Mail (optional now)

- [x] Optional `mail_outbox` so F16 TX can insert later — **or** defer the table to Bar C
- [x] If created now: empty table, same database, no Hub `communications` schema
- [x] Receipt email is in-process later. **Not** a Notify service

## D29.3 Refuse

- [x] Not Hub `one.AuditEvents` fire-and-forget / own `SaveChanges`
- [x] Not `communications` schema / `DocumentPublishedIntegrationEvent`
- [x] Not nine outboxes

## D29.4 Exit

- [x] `audit_events` exists; `mail_outbox` exists **or** is explicitly deferred
- [x] IsolationTests still ban MediatR
- [x] Unblocked for B99 (tables in use). Track D process+tables complete
