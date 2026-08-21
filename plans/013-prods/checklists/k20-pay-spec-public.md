# K20 — TypeSpec public pay (`PayPublic`)

**Track:** Buyer page · **Depends:** K10, K12  
**Analysis:** [05](../05-checkout-frontend.md) §3.2; 012 [04](../../012-one-to-pay/04-pay-spec-contract.md)  
**Goal:** `packages/pay-spec` grows public GET + start. Not Hub `/public/commerce`.

---

## K20.1 Add

- [ ] Interface e.g. `PayPublic`: `GET /v1/pay/{token}` and `POST /v1/pay/{token}/start`
- [ ] Buyer DTO + `{ redirect_url }` (and start body name/email if K18 landed)
- [ ] Namespace `LazuarPay`; server still `http://localhost:8081`
- [ ] snake_case matches host; document public (no Bearer) vs merchant checkouts

## K20.2 Must not

- [ ] No Hub `/public/commerce` import or clone
- [ ] No `packages/api-spec` import
- [ ] Do **not** mark merchant `GET /v1/checkouts/{id}` as unauthenticated in the spec
- [ ] No `task gen` / Hub honesty allowlist / NSwag into `@repo/api-types-ts`

## K20.3 Compile

- [ ] `task pay:spec` succeeds
- [ ] Dist stays gitignored

## K20.4 Exit

- [ ] Spec and host public paths match
- [ ] May same-tip with K10/K12 if small
- [ ] Unblocked for later `@repo/pay-types-ts` on checkout (not this phase)
