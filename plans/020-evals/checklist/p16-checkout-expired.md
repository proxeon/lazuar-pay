# P16 — Occupancy TTL event (parked)

**Track:** Parked  
**Analysis:** [`../07-money-remaining.md`](../07-money-remaining.md) lazy TTL; [`../03-outbound-webhooks.md`](../03-outbound-webhooks.md) hole 9  
**Unpark when:** A second app holds seats and races 30-minute expire.

**Why parked:** Expire is lazy on GET/start. No worker. Hatch accepts no `checkout.expired`. K99a still true via poll or completed.

**Related files**

| Path | Role today |
|------|------------|
| `apps/lazuar-pay/src/Lazuar.Pay/PaymentLinks/PaymentLinkOccupancy.cs` | TTL default 30 min |
| `apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs` | Expire on start fail |
| P20 | Late PSP pay after expire — different hole |

**Current (`6d730d15`):** Expire in-process; no outbound.

---

## P16.1 Must not

- [ ] Do not block W18 on this event
- [ ] Do not add `IHostedService` expire as a fake Plane C
