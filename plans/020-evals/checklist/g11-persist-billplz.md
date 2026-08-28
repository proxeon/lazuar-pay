# G11 — Billplz persist-before-PSP

**Track:** G · **Depends:** G10 (pattern) · **Does not gate K99a**  
**Analysis:** [`../07-money-remaining.md`](../07-money-remaining.md) 014  
**Goal:** Same hole as G10 on Billplz `POST …/bills`.

**Why:** BillplzHosted POSTs a bill, reads `url`/`id`, returns `HostedSession`. PublicPay then SaveChanges. No Stripe-style idempotency key. Localhost callbacks already 400 (separate PublicBaseUrl rule).

**Related files**

| Path | Role today |
|------|------------|
| `apps/lazuar-pay/src/Lazuar.Pay/Rails/Billplz/BillplzHosted.cs` | `POST {host}bills` after `TryPublicBase` |
| `apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs` | Same persist-after-HTTP as CHIP |
| `apps/lazuar-pay/tests/` Billplz tests | Create + webhook |
| `apps/lazuar-pay/.env.example` | `Pay__PublicBaseUrl` https |

**Current (`6d730d15`):** HTTP then persist. PublicBaseUrl localhost rejected at **start**, not this hole.

---

## G11.1

- [x] Idempotency header/body Billplz accepts **or** persist-before-HTTP
- [x] FakePsp: first 201, SaveChanges fail, retry does not create a second bill
- [x] Do not weaken localhost callback 400

## G11.2 Must not

- [x] Do not copy Hub Billplz module
- [x] Do not skip because “G10 CHIP is the dogfood rail” without saying so in K99b

## G11.3 Exit

- [x] Unblocked for G12
