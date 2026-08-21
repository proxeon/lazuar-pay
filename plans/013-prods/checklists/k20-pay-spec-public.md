# K20 — TypeSpec public pay (`PayPublic`)

**Track:** Buyer page · **Depends:** K10, K12  
**Analysis:** [05](../05-checkout-frontend.md) §3.2; 012 [04](../../012-one-to-pay/04-pay-spec-contract.md)  
**Goal:** `packages/pay-spec` grows public GET + start. Not Hub `/public/commerce`.

---

## K20.1 Add

- [x] Interface e.g. `PayPublic`: `GET /v1/pay/{token}` and `POST /v1/pay/{token}/start`
- [x] Buyer DTO + `{ redirect_url }` (and start body name/email if K18 landed)
- [x] Namespace `LazuarPay`; server still `http://localhost:8081`
- [x] snake_case matches host; document public (no Bearer) vs merchant checkouts

## K20.2 Must not

- [x] No Hub `/public/commerce` import or clone
- [x] No `packages/api-spec` import
- [x] Do **not** mark merchant `GET /v1/checkouts/{id}` as unauthenticated in the spec
- [x] No `task gen` / Hub honesty allowlist / NSwag into `@repo/api-types-ts`

## K20.3 Compile

- [x] `task pay:spec` succeeds
- [x] Dist stays gitignored

## K20.4 Exit

- [x] Spec and host public paths match
- [x] May same-tip with K10/K12 if small
- [x] Unblocked for later `@repo/pay-types-ts` on checkout (not this phase)
