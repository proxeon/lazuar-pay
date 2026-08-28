# U14 — `pay_url` hermetic matrix

**Track:** U · **Depends:** U11, U12, U13  
**Goal:** One place that would have caught a localhost bake-in.

**Why:** `CheckoutUrls.Base` falls back to `http://localhost:5179` in Testing if config is empty. Factory sets a test origin. Tests must assert the factory origin, not laptop, so Production images do not inherit a lie.

**Related files**

| Path | Role today |
|------|------------|
| `apps/lazuar-pay/src/Lazuar.Pay/PublicPay/CheckoutUrls.cs` | Testing fallback localhost |
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Infrastructure/PayApiFactory.cs` | `CheckoutBaseUrl` |
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Checkouts/CheckoutTests.cs` | Add asserts here or a small `PayUrlTests.cs` |
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/PaymentLinks/PaymentLinkTests.cs` | Same |

**Current (`6d730d15`):** No `pay_url` asserts.

---

## U14.1

- [x] Factory `CheckoutBaseUrl` is `http://pay-checkout.test.example` (already)
- [x] Assert no `localhost:5179` in mint JSON when factory origin is set
- [x] Payment-link + checkout both covered
- [x] Key mint (after M14) also returns `pay_url`

## U14.2 Exit

- [x] Unblocked for U15, E13
