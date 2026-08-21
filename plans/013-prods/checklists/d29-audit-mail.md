# D29 — `audit_events` (+ optional `mail_outbox`)

**Track:** Database · **Depends:** D16  
**Analysis:** [03](../03-host-production-seams.md), [09](../09-data-migration.md)  
**Goal:** NP-AUD-001. Audit (and optional mail) in the **same** Pay database. Not Notify. Not `communications`.

---

## D29.1 Audit

- [ ] `audit_events`: `org_id`, `action`, `at`, `payload` **small**
- [ ] Same database as charges / journal / receipts
- [ ] Insert in the money transaction is F16 — this phase is the table

## D29.2 Mail (optional now)

- [ ] Optional `mail_outbox` so F16 TX can insert later — **or** defer the table to Bar C
- [ ] If created now: empty table, same database, no Hub `communications` schema
- [ ] Receipt email is in-process later. **Not** a Notify service

## D29.3 Refuse

- [ ] Not Hub `one.AuditEvents` fire-and-forget / own `SaveChanges`
- [ ] Not `communications` schema / `DocumentPublishedIntegrationEvent`
- [ ] Not nine outboxes

## D29.4 Exit

- [ ] `audit_events` exists; `mail_outbox` exists **or** is explicitly deferred
- [ ] IsolationTests still ban MediatR
- [ ] Unblocked for B99 (tables in use). Track D process+tables complete
