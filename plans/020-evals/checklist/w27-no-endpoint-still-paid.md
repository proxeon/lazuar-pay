# W27 — No endpoint: still paid, zero deliveries

**Track:** W · **Depends:** W18  
**Goal:** Plane C is optional. Cashier must not depend on a second app.

**Why:** First-party `:5179` poll must keep working if no one registered a URL. Missing endpoint is not a fulfill error.

**Related files**

| Path | Role today |
|------|------------|
| `apps/lazuar-pay/src/Lazuar.Pay/Money/Fulfillment.cs` | Paid without HTTP |
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/PublicPay/PublicPayTests.cs` | Test paid |
| `apps/lazuar-pay-checkout/src/` | Poll `?status=verifying` |

**Current (`6d730d15`):** Fulfill never talks to a merchant URL (vacuously true). Keep that if no row.

---

## W27.1 Tests

- [ ] Test start without register → charge + `RCPT-` exist, `webhook_deliveries` count 0
- [ ] First-party `:5179` poll still works (no worker required)

## W27.2 Exit

- [ ] Unblocked for W28
