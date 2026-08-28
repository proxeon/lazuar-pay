# U13 — GET checkout includes `pay_url`

**Track:** U · **Depends:** U11  
**Goal:** Poll/read after mint still has the buyer URL.

**Why:** Idempotent replay and `GET /v1/checkouts/{id}` must match create. 062 already requires Bearer before lookup (cross-org 403 not 404).

**Related files**

| Path | Role today |
|------|------------|
| `apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutEndpoints.cs` | `GET /v1/checkouts/{id}` |
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Checkouts/CheckoutTests.cs` | Get + 401 before lookup |
| `apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs` | Public GET already has the token in the path |

**Current (`6d730d15`):** Get matches create minus `pay_url`.

---

## U13.1

- [ ] `GET /v1/checkouts/{id}` member JSON includes `pay_url`
- [ ] Cross-org still 403 before lookup (062)
- [ ] Public `GET /v1/pay/{token}` does **not** need `pay_url` (they already have the token)

## U13.2 Tests

- [ ] Get-after-create matches create `pay_url`

## U13.3 Exit

- [ ] Unblocked for U14
