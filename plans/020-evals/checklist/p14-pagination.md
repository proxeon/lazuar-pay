# P14 — List pagination (parked)

**Track:** Parked  
**Analysis:** [`../01-public-http-api.md`](../01-public-http-api.md)  
**Unpark when:** K99a is closed and an org has enough rows that a dump is a lie.

**Why parked:** Lists return the whole org. Fine for dogfood. Not the kernel door.

**Related files**

| Path | Role today |
|------|------------|
| `apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutEndpoints.cs` | List one-off |
| `apps/lazuar-pay/src/Lazuar.Pay/Money/Queries/PaymentQueryEndpoints.cs` | Payments/receipts arrays |
| `apps/lazuar-pay/src/Lazuar.Pay/PaymentLinks/PaymentLinkEndpoints.cs` | List |
| `packages/pay-spec/main.tsp` | Arrays, no cursor |

**Current (`6d730d15`):** No `page` / cursor.

---

## P14.1 Must not (this program)

- [x] Do not invent `/v2` for pagination
- [x] Do not block M14 on list shape
