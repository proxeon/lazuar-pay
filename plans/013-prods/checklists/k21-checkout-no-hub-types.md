# K21 — Checkout `package.json` has no Hub types

**Track:** Buyer page · **Depends:** K15  
**Analysis:** [05](../05-checkout-frontend.md) §2.4, §5.3 refuse  
**Goal:** `:5179` must not import `@repo/api-types-ts`.

---

## K21.1 Lock

- [ ] `apps/lazuar-pay-checkout/package.json` has **no** `@repo/api-types-ts`
- [ ] No `openapi-fetch` client typed from Hub `paths` (`/public/commerce/*`, `/one/auth/me`)
- [ ] Grep now; Isolation / Q10 may scan later
- [ ] Same ban as M23, on the **checkout** package — do not “share” merchant types into 5179

## K21.2 Must not

- [ ] Do not “temporarily” import Hub types to paint a product
- [ ] Do not add `@repo/api-type-ts` (One) either — checkout does not call One
- [ ] `@repo/pay-types-ts` only when generated from `pay-spec` **and** 5179 calls `/v1` for real (optional later)

## K21.3 Exit

- [ ] `package.json` deps stay React (plus router if added) — no Hub OpenAPI package
- [ ] Unblocked for Q10 when that phase exists
