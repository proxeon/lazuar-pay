# G13 — Razorpay persist-before-PSP

**Track:** G · **Depends:** G10 pattern · **Does not gate K99a**  
**Analysis:** [`../07-money-remaining.md`](../07-money-remaining.md) 014  
**Goal:** Same hole on Razorpay payment-link/order create. Do not regress Stripe.

**Why:** RazorpayHosted POSTs then returns session. StripeHosted is the **only** rail with `IdempotencyKey` today. A “fix all rails” PR that touches Stripe must keep `lazuar-checkout:{id}`.

**Related files**

| Path | Role today |
|------|------------|
| `apps/lazuar-pay/src/Lazuar.Pay/Rails/Razorpay/RazorpayHosted.cs` | Create hosted |
| `apps/lazuar-pay/src/Lazuar.Pay/Rails/Stripe/StripeHosted.cs` | Must keep idempotency key |
| `apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs` | Shared persist |
| Razorpay tests | Capture/paid leftovers stay 07 unless you touch parse |

**Current (`6d730d15`):** Razorpay HTTP then persist. Stripe idempotent.

---

## G13.1

- [x] Razorpay idempotency **or** persist-before-HTTP
- [x] Test FakePsp retry
- [x] Stripe test: same checkout id still sends the same Stripe idempotency key

## G13.2 Must not

- [x] Do not remove Stripe `RequestOptions.IdempotencyKey`

## G13.3 Exit

- [x] 014 source comment on PublicPay can be deleted only when **all** non-Test rails are covered **or** the comment lists remaining rails
