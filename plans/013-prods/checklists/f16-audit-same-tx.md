# F16 — Audit in the same transaction

**Track:** Fulfillment · **Depends:** F13, D29  
**Analysis:** [07](../07-fulfillment-ledger-docs.md)  
**IDs:** NP-AUD-001  
**Goal:** Insert `audit_events` in the same transaction as `paid` + journal. Not a Notify/Audit process.

---

## F16.1 Write

- [ ] Same `BEGIN` as journal + `paid`
- [ ] Action e.g. `charge.paid`; entity = checkout; actor = `system:webhook` (not a One user)
- [ ] If audit insert fails, the **charge** rolls back

## F16.2 Must not

- [ ] Not a Notify service. Not an Audit service. Not One `one.AuditEvents`
- [ ] Hub `IAuditRecorder` “must never throw” is the anti-pattern — read `apps/lazuar-api/Modules/One/Infrastructure/Services/AuditRecorder.cs`; **read, do not copy project**
- [ ] Do not insert after `SaveChanges` (Hub `RecordRefundCommandHandler`)
- [ ] No fire-and-forget / swallow that returns success without a row

## F16.3 Exit

- [ ] Failed audit insert leaves checkout not `paid`
- [ ] Unblocked for F22 (replay still one audit row per charge)
