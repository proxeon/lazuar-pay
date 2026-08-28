# W18 — Enqueue delivery in fulfill SaveChanges

**Track:** W · **Depends:** W11, W12, W17  
**Analysis:** [`../03-outbound-webhooks.md`](../03-outbound-webhooks.md) §9.2.6 / refuse fire-and-forget  
**Goal:** Paid + outbox row are one transaction. No HTTP here.

**Why:** If we POST inside `FulfillPaidAsync`, PSP webhook latency includes the merchant URL, and a crash after HTTP before commit double-sends or loses money consistency. Insert pending; worker in W20.

**Related files**

| Path | Role today |
|------|------------|
| `apps/lazuar-pay/src/Lazuar.Pay/Money/Fulfillment.cs` | Charge, journal, document, audit, `SaveChanges` |
| `apps/lazuar-pay/src/Lazuar.Pay/Rails/Test/TestHosted.cs` | Start = fulfill |
| `apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs` | Test start calls fulfill |
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Webhooks/FillTests.cs` | Unique fulfill |
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/PublicPay/PublicPayTests.cs` | Test paid |

**Current (`6d730d15`):** Fulfill does not enqueue HTTP.

---

## W18.1

- [ ] After charge/journal/document/audit are added, if org has active endpoint and catalog allows `payment.completed`, add a `pending` delivery
- [ ] `EventId` stable: charge id (or document id) — **one id per paid checkout**
- [ ] `PayloadJson` is the **exact** string that will be signed/sent
- [ ] No active endpoint → skip (W27)
- [ ] Unique conflict on `(endpoint, event_id)` → do not insert a second row (W19)
- [ ] Test rail fulfill **does** enqueue (do not skip Test)

## W18.2 Must not

- [ ] `HttpClient.PostAsync` inside `Fulfillment`
- [ ] Enqueue on `{ ignored }` Plane B
- [ ] Enqueue on Occupancy expire (P16)

## W18.3 Tests

- [ ] Can wait for W27/W19; at least: Test start-to-paid with endpoint → 1 pending row in same DB

## W18.4 Exit

- [ ] Unblocked for W19, W20
