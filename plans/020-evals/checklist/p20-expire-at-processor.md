# P20 — Late PSP pay after TTL (parked)

**Track:** Parked · **Job B money, not kernel**  
**Analysis:** [`../07-money-remaining.md`](../07-money-remaining.md) production leftover  
**Unpark when:** Dogfood rail is CHIP (or similar) **and** 30-minute reservation is on.

**Why parked:** Abandoned `open` becomes `expired` lazily. Buyer can still complete CHIP. Plane B sees expired, does not pay, cash sits at the processor. Needs refund (P10) or expire-at-processor API. Not M2M.

**Related files**

| Path | Role today |
|------|------------|
| `apps/lazuar-pay/src/Lazuar.Pay/PaymentLinks/PaymentLinkOccupancy.cs` | TTL |
| `apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs` | ExpireFailedReservation |
| `apps/lazuar-pay/src/Lazuar.Pay/Webhooks/WebhookEndpoints.cs` | Fulfill skips expired? (verify when unparking) |
| `apps/lazuar-pay/src/Lazuar.Pay/Rails/Chip/ChipHosted.cs` | Purchase lives at CHIP |
| P10 | Refunds |

**Current (`6d730d15`):** TTL 30 min; no reverse at processor.

---

## P20.1 Must not

- [x] Do not staff as Job A
- [x] Do not take live CHIP volume until this is named on that rail
- [x] Do not fulfill expired over-capacity to “fix” cash (occupancy rule)
