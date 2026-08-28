# E13 — Sample mints checkout

**Track:** E · **Depends:** E12, M14, U11  
**Goal:** `POST /v1/checkouts` with `lzr_sk_`; use `pay_url`.

**Why:** Merchant SPA mints **payment-links**. Kernel mint is checkout create. Sample must use the unused door so we dogfood M14+U11.

**Related files**

| Path | Role today |
|------|------------|
| `apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutEndpoints.cs` | POST |
| `apps/lazuar-pay/README.md` | JWT curl to copy shape |
| U11 | `pay_url` |
| M14 | Key writer |

**Current (`6d730d15`):** Host mint exists; sample does not.

---

## E13.1

- [ ] POST JSON snake_case: `org_id`, `amount`, `currency` MYR, `provider: test` (laptop) or a real rail
- [ ] Header `Authorization: Bearer $PAY_API_KEY`
- [ ] Read `pay_url` + `public_token` + `id`
- [ ] Do not clone merchant Vite
- [ ] Idempotency-Key optional header documented

## E13.2 Must not

- [ ] Do not treat success_url as paid
- [ ] Do not call Hub `/api/v1`

## E13.3 Exit

- [ ] Unblocked for E14
