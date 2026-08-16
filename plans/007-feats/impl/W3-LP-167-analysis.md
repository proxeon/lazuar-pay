# W3-LP-167 — Audit log

**Status:** Analysis only — **do not implement from this file**  
**Date:** 2026-08-16  
**ID authority:** [00-implement-ids.md](../00-implement-ids.md) Wave 3 `LP-167`. Tracker: *Audit log* — Lazuar **N**.  
**Not this ID:** Stripe Sigma. Webhook delivery logs (`LP-134`). Message delivery logs. Metric reconstructability (LP-161 glossary). PDPA access log.

**Invariant:** Money and identity mutations append an immutable row: who, what, which entity, when. Ops can list the last N for the workspace. Reads are not logged. This is not a SIEM.

---

## 0. Scope lock

In scope:

- `one.AuditEvents` (or `commerce` — prefer **One**, workspace-scoped)  
- Writers on: refund, cancel/keep, record-payment, change-plan/qty, collection pause, invite/remove member, payment-config upsert, API key mint/revoke  
- `GET /one/workspaces/{id}/audit`  
- Ops page

Out of scope:

- GET/list traffic  
- Hash-chain / WORM  
- Export SIEM  
- Actor IP required (optional column ok)

---

## 1. Verdict

No `AuditLog` type in the tree. Support reconstructs “who refunded this” from JWT logs and `TransactionLogs.RecordedByName` (partial). Chargebee/Stripe train merchants to expect a security/activity list.

---

## 2. Current files

| Path | Role |
|------|------|
| `TransactionLogs` | Payment rows, not actor audit |
| `MessageDeliveryLog` | Channel send |
| One members endpoints | No trail |
| Ops | No Audit nav |

---

## 3. Exact gaps

| # | Gap |
|---|-----|
| G1 | No table |
| G2 | Handlers do not append |
| G3 | No UI |

---

## 4. Recommended model

```
AuditEvent(
  Id, OrganizationId, ActorUserId?, ActorEmail?,
  Action,           // refund.created, subscriber.canceled, member.invited, ...
  EntityType, EntityId,
  Metadata jsonb,   // amount, reason — no secrets
  CreatedAt
)
index (OrganizationId, CreatedAt desc)
```

Fire-and-forget in-process after successful `SaveChanges`. Failure to audit must **not** fail the money path (log error). Do not put card numbers or API keys in metadata.

---

## 5. Minimal code changes

| File | Change |
|------|--------|
| Entity + migration + `IAuditRecorder` | One module |
| 8–10 command handlers | `Record(...)` after success |
| TypeSpec + GET | Paginated |
| `AuditLogPage.tsx` | Workspace nav |
| Tests | Refund creates a row; GET IDOR other org empty |

Must not: wrap every MediatR handler; log tokens.

---

## 6. Tests

| Case | Expect |
|------|--------|
| Record-payment | `subscriber.payment_recorded` + amount |
| Foreign org GET | 403 / empty |
| Invite | `member.invited` + email + role |
| Recorder throw | Refund still commits (optional if hard) |

---

## 7. Acceptance

1. Refund + invite appear on Team/Audit within a minute.  
2. Another workspace cannot read those rows.  
3. No secrets in metadata.  
4. Viewer (LP-166) can read audit; Member can too.

Tracker **N → Y** after 1–2.

---

## 8. Order

1. Table + recorder  
2. Money handlers  
3. Identity handlers  
4. Page  

Do **not** implement from this file.
