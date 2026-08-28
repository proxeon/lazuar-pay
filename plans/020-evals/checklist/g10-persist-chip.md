# G10 — CHIP persist-before-PSP (or processor idempotency)

**Track:** G · **Depends:** K00 · **Does not gate K99a**  
**Analysis:** [`../07-money-remaining.md`](../07-money-remaining.md) (014 still live); [`../06-host-production.md`](../06-host-production.md)  
**Goal:** Retry after SaveChanges failure does not mint a second CHIP purchase.

**Why:** `PublicPayEndpoints` comments the 016 hole: HTTP to CHIP, then persist URL. If SaveChanges fails, retry calls CHIP again. Stripe passes `IdempotencyKey = "lazuar-checkout:" + checkout.Id`. CHIP does not. Occupancy may already hold an `open` seat.

**Related files**

| Path | Role today |
|------|------------|
| `apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs` | ~190–233: if `PspRedirectUrl` set, return it; else `CreateHostedUrlAsync` then `SaveChanges` |
| `apps/lazuar-pay/src/Lazuar.Pay/Rails/Chip/ChipHosted.cs` | `POST …/purchases/` — no idempotency header |
| `apps/lazuar-pay/src/Lazuar.Pay/Rails/Stripe/StripeHosted.cs` | **Contrast:** `IdempotencyKey = "lazuar-checkout:" + checkout.Id` |
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Infrastructure/FakePspHandler.cs` | Count POSTs |
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Infrastructure/FulfillmentProbe.cs` | ThrowAfterSave |
| `issues/002/014-…` (if present) | YAML resolved; source still matches |

**Current (`6d730d15`):** Stored URL short-circuit only **after** a successful save. CHIP HTTP has no idempotency key.

---

## G10.1 Hatch (pick one and test it)

- [x] **Either** persist a “starting” row / session id before HTTP (harder with current schema)
- [x] **Or** send a CHIP-supported idempotency header keyed on `checkout.Id` (steal Stripe judgment)
- [x] Retry after FakePsp 201 + SaveChanges throw: second CHIP POST is 0 **or** CHIP treats it as the same purchase
- [x] Occupancy: still one `open` child

## G10.2 Must not

- [x] Do not mark 014 done in YAML without this source change
- [x] Do not import Hub CHIP registrar
- [x] Do not “fix” all rails in one fat PR if you only dogfood CHIP — but do not claim G11–G13 done

## G10.3 Exit

- [x] Unblocked for G11 (same pattern, different HTTP)
