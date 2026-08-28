# W11 — `webhook_deliveries` outbox

**Track:** W · **Depends:** W10  
**Analysis:** [`../03-outbound-webhooks.md`](../03-outbound-webhooks.md) §9.2  
**Goal:** At-least-once delivery rows. Not Hub `WebhookDeliveryOutbox`.

**Why:** Fulfillment is one `SaveChanges` with charge + journal + `RCPT-`. The signed POST must survive process crash. Insert a pending row in that TX; HTTP later (W20). Hub type name is Isolation-banned in W28.

**Related files**

| Path | Role today |
|------|------------|
| `apps/lazuar-pay/src/Lazuar.Pay/Money/Fulfillment.cs` | Paid TX — enqueue site is W18 |
| `apps/lazuar-pay/src/Lazuar.Pay/Data/Rows.cs` | `MailOutboxRow` unused — do not reuse |
| Hub `WebhookDeliveryOutbox` | Museum name only |

**Current (`6d730d15`):** No deliveries table.

---

## W11.1 Schema

- [ ] Table `webhook_deliveries`
- [ ] Columns: `Id`, `OrgId`, `EndpointId`, `EventId`, `EventType`, `PayloadJson` (exact signed body), `Status` (`pending`/`succeeded`/`dead`), `AttemptCount`, `NextAttemptAt`, `LeaseUntil` (nullable), `LastHttpStatus` (nullable), `LastError` (short, no secret), `CreatedAt`
- [ ] Unique `(EndpointId, EventId)`
- [ ] Index `(Status, NextAttemptAt)` for the worker
- [ ] Migration + snapshot

## W11.2 Must not

- [ ] Do not store the `whsec_` in this table
- [ ] Do not name types `WebhookDeliveryOutbox` / `OutboundWebhookRequested`

## W11.3 Exit

- [ ] Unblocked for W12
