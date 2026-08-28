# P10 — Refunds (parked)

**Track:** Parked · **Do not start in Job A**  
**Analysis:** [`../07-money-remaining.md`](../07-money-remaining.md) §4 / §9; [`../11-what-next.md`](../11-what-next.md)  
**Unpark when:** K99b is boring on **one** rail that actually supports refunds, **and** P20 (late PSP pay) is a real loss.

**Why parked:** No refund Map*, no reverse journal, Official Receipt is not a credit note. Staffing refunds as “kernel” delays M14/W21. A second app that needs refunds cannot integrate them because Pay cannot do them — that is a later product, not a 020 hatch.

**Related files**

| Path | Role today |
|------|------------|
| `apps/lazuar-pay/src/Lazuar.Pay/Money/Fulfillment.cs` | Paid only |
| `apps/lazuar-pay/src/Lazuar.Pay/Money/Queries/PaymentQueryEndpoints.cs` | List payments/receipts |
| `apps/lazuar-pay/src/Lazuar.Pay/Data/Rows.cs` | `ChargeRow.Status` |
| IsolationTests | LHDN / tax still refuse |
| W12 catalog | Do not add `refund.created` until a writer exists |

**Current (`6d730d15`):** Grep `Refund` in focused Pay host is empty (or unused).

---

## P10.1 When unparking (not now)

- [ ] Rail-specific refund HTTP (Stripe vs CHIP are different)
- [ ] Unique refund id; journal reverse; document type ≠ `RCPT-` tax lie
- [ ] Plane C `refund.created` only after the writer
- [ ] Tests: partial vs full; replay

## P10.2 Must not (this program)

- [ ] Do not add a stub `POST /v1/refunds` that 501
- [ ] Do not emit refund events from ignored webhooks
