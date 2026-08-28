# P15 — `payment.failed` (parked)

**Track:** Parked  
**Analysis:** [`../03-outbound-webhooks.md`](../03-outbound-webhooks.md) hole 6  
**Unpark when:** A rail **persists** failed (not `{ ignored }`).

**Why parked:** Plane B returns `{ ignored }` for events that are not paid. Emitting Plane C failed from that would wake the sample on noise (Hub catalog-without-writer).

**Related files**

| Path | Role today |
|------|------------|
| `apps/lazuar-pay/src/Lazuar.Pay/Webhooks/WebhookEndpoints.cs` | `{ ignored }` |
| `apps/lazuar-pay/src/Lazuar.Pay/Rails/*/*Webhook.cs` | Parse fail vs ignore |
| W12 catalog | Hatch = `payment.completed` only |

**Current (`6d730d15`):** No failed checkout status writer.

---

## P15.1 Must not

- [ ] Do not add `payment.failed` to the catalog in W12
- [ ] Do not emit from ignored PSP events
