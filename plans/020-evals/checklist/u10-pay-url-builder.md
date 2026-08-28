# U10 — Public pay URL builder

**Track:** U · **Depends:** K00  
**Analysis:** [`../08-headless-vs-spa.md`](../08-headless-vs-spa.md); [`../01-public-http-api.md`](../01-public-http-api.md)  
**Goal:** One function builds the buyer URL. No SPA origin guess in the host.

**Why:** Checkout SPA is `/c/{token}`. Merchant copies `VITE_CHECKOUT_ORIGIN + '/c/' + token`. A second app has neither Vite env. `CheckoutUrls.Base` already knows `Pay:CheckoutBaseUrl` but mint JSON does not return a URL.

**Related files**

| Path | Role today |
|------|------------|
| `apps/lazuar-pay/src/Lazuar.Pay/PublicPay/CheckoutUrls.cs` | `Base` + success/cancel; Testing fallback `localhost:5179` |
| `apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutEndpoints.cs` | Create/Get JSON — no `pay_url` |
| `apps/lazuar-pay/src/Lazuar.Pay/PaymentLinks/PaymentLinkEndpoints.cs` | Same |
| `apps/lazuar-pay-merchant/src/pages/org/CheckoutsPage.tsx` | Builds copy URL from `VITE_CHECKOUT_ORIGIN` |
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Infrastructure/PayApiFactory.cs` | `CheckoutBaseUrl=http://pay-checkout.test.example` |
| `apps/lazuar-pay/.env.example` | `Pay__CheckoutBaseUrl` |

**Current (`6d730d15`):** Hosted PSP `redirect_url` is start, not mint. No `pay_url` field.

---

## U10.1

- [ ] Input: `Pay:CheckoutBaseUrl` (trim, no trailing slash) + `public_token`
- [ ] Output: `{base}/c/{token}` matching checkout Vite route
- [ ] Empty/missing `CheckoutBaseUrl` → mint still succeeds but `pay_url` null **or** 503 on mint — **pick one in this phase and test it**. Prefer: 201 with `pay_url` omitted only in tests that unset the setting; factory already sets a test origin
- [ ] Do not bake `localhost:5179` in Production code paths

## U10.2 Tests

- [ ] Unit: `http://pay-checkout.test.example` + `tok_abc` → `http://pay-checkout.test.example/c/tok_abc`
- [ ] Trailing slash on base stripped

## U10.3 Exit

- [ ] Unblocked for U11
