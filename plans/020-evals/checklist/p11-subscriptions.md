# P11 — Subscriptions (parked / refuse this program)

**Track:** Parked  
**Analysis:** [`../07-money-remaining.md`](../07-money-remaining.md) §5; 019 G11 dead branch  
**Unpark:** Not in 020. Needs a new product paper.

**Why parked:** `subscriptions` table and checkout `Interval` exist; mint always `one_off`. Emitting `subscription.activated` because the table exists is Hub catalog-without-writer.

**Related files**

| Path | Role today |
|------|------------|
| `apps/lazuar-pay/src/Lazuar.Pay/Data/Rows.cs` | `SubscriptionRow` |
| `apps/lazuar-pay/src/Lazuar.Pay/Data/PayDbContext.cs` | `Subscriptions` set |
| `apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutEndpoints.cs` | `Interval` default `one_off` |
| Catalog prices `interval` | Label; not a billing engine |

**Current (`6d730d15`):** Schema leftover. No dunning. No auto-debit.

---

## P11.1 Must not

- [ ] Do not add Plane C subscription types
- [ ] Do not put buyers in One as members for “access”
- [ ] Do not staff Hub Billing module
