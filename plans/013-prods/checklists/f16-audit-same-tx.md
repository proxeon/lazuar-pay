# F16 — Audit in the same transaction

**Track:** Fulfillment · **Depends:** F13, D29  
**Analysis:** [07](../07-fulfillment-ledger-docs.md)  
**IDs:** NP-AUD-001  
**Goal:** Insert `audit_events` in the same transaction as `paid` + journal. Not a Notify/Audit process.

---

## F16.1 Write

- [x] Same `BEGIN` as journal + `paid`
- [x] Action e.g. `charge.paid`; entity = checkout; actor = `system:webhook` (not a One user)
- [x] If audit insert fails, the **charge** rolls back

## F16.2 Must not

- [x] Not a Notify service. Not an Audit service. Not One `one.AuditEvents`
- [x] Hub `IAuditRecorder` “must never throw” is the anti-pattern — read `apps/lazuar-api/Modules/One/Infrastructure/Services/AuditRecorder.cs`; **read, do not copy project**
- [x] Do not insert after `SaveChanges` (Hub `RecordRefundCommandHandler`)
- [x] No fire-and-forget / swallow that returns success without a row

## F16.3 Exit

- [x] Failed audit insert leaves checkout not `paid`
- [x] Unblocked for F22 (replay still one audit row per charge)
