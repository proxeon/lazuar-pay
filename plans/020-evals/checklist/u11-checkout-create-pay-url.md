# U11 — Checkout create returns `pay_url`

**Track:** U · **Depends:** U10  
**Analysis:** [`../08-headless-vs-spa.md`](../08-headless-vs-spa.md)  
**Goal:** Kernel mint 201 is enough to send a buyer somewhere.

**Why:** Kernel mint is `POST /v1/checkouts`. Merchant SPA does not call it. A second app that does must not reverse-engineer checkout origin.

**Related files**

| Path | Role today |
|------|------------|
| `apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutEndpoints.cs` | 201 `CheckoutSession` |
| `apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutStore.cs` | Persist + public_token |
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Checkouts/CheckoutTests.cs` | Create 201 |
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Infrastructure/PayTest.cs` | `SeedCheckout` |
| `packages/pay-spec/main.tsp` | `CheckoutSession` — no `pay_url` (U15) |

**Current (`6d730d15`):** 201 has `public_token`, not `pay_url`.

---

## U11.1

- [x] `POST /v1/checkouts` 201 JSON includes `pay_url`
- [x] Idempotent 200 replay includes the same `pay_url`
- [x] Field snake_case
- [x] Do not put hosted PSP URL here (that is start)

## U11.2 Tests

- [x] `SeedCheckout` / create test asserts `pay_url` ends with `/c/{public_token}`
- [x] Body does not include WrapKey / secrets

## U11.3 Exit

- [x] Unblocked for U12
