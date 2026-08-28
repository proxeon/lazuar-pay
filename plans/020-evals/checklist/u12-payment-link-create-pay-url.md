# U12 — Payment-link create returns `pay_url`

**Track:** U · **Depends:** U10  
**Analysis:** Merchant copies `VITE_CHECKOUT_ORIGIN` today  
**Goal:** Host is source of the public link.

**Why:** First-party staff copy a URL the SPA synthesizes. If `VITE_CHECKOUT_ORIGIN` is wrong, the host still knows `Pay:CheckoutBaseUrl`. Return it on mint so SPA and second app share one builder.

**Related files**

| Path | Role today |
|------|------------|
| `apps/lazuar-pay/src/Lazuar.Pay/PaymentLinks/PaymentLinkEndpoints.cs` | 201 PaymentLink JSON |
| `apps/lazuar-pay-merchant/src/pages/org/CheckoutsPage.tsx` | Copy uses `VITE_CHECKOUT_ORIGIN` |
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/PaymentLinks/PaymentLinkTests.cs` | Create 201 |
| `packages/pay-spec/main.tsp` | `PaymentLink` model |

**Current (`6d730d15`):** Link has `public_token`. SPA concatenates origin.

---

## U12.1

- [x] `POST /v1/payment-links` 201 includes `pay_url` for the **link** token (`/c/{link.public_token}`)
- [x] List items may include `pay_url` (same builder) — if you skip list, say so here and do GET-only
- [x] Query `slot_key` is **not** baked into `pay_url` (buyer page generates/resumes)

## U12.2 Tests

- [x] Create link asserts `pay_url`

## U12.3 Exit

- [x] Unblocked for U13
