# H15 — Start limiter honesty

**Track:** H · **Depends:** K00  
**Analysis:** [`../06-host-production.md`](../06-host-production.md) §13.2.10  
**Goal:** Docs match default 20. Tests stay 200.

**Why:** Production default 20; factory 200 so occupancy tests do not 429. Raising the default to match tests hides production.

**Related files**

| Path | Role today |
|------|------------|
| `apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs` | `GetValue("Pay:StartMaxPerMinute", 20)` |
| `apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayLimiter.cs` | Per-token limiter |
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Infrastructure/PayApiFactory.cs` | Default 200 |
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/PaymentLinks/PaymentLinkTests.cs` | `StartMaxPerMinute = 2` |
| `apps/lazuar-pay/.env.example` | Missing this knob |

**Current (`6d730d15`):** Code 20; docs silent.

---

## H15.1

- [x] `.env.example` comments `Pay__StartMaxPerMinute` default 20
- [x] Do not change factory 200
- [x] Do not change production default to 200
- [x] Optional: `Retry-After` on 429 — if you skip, note it here as leftover

## H15.2 Must not

- [x] Do not disable with 0 in prod examples

## H15.3 Exit

- [x] Track H can complete without Retry-After
