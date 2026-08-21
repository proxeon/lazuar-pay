# K17 — No OIDC on `:5179` (NP-CHK-007)

**Track:** Buyer page · **Depends:** K15  
**Analysis:** [05](../05-checkout-frontend.md) §8.3, §9.1  
**Goal:** Buyer pays without a One account. Fail the slice if login appears.  
**011:** NP-CHK-007

---

## K17.1 Package

- [ ] `apps/lazuar-pay-checkout/package.json` has **no** `oidc-client-ts` / `oidc-client` / `react-oidc-context`
- [ ] No One `@lazuar/one-client` as a checkout dep

## K17.2 UI / source

- [ ] No Sign in button on `/c/{token}`
- [ ] Grep checkout `src` + `package.json` + `.env*`: no `zitadel`, no `/callback` OIDC route, no password form
- [ ] No `VITE_ZITADEL_*` / `VITE_OIDC_CLIENT_ID` on this app
- [ ] Fail this slice if any of the above is present — not a follow-up ticket

## K17.3 Runtime (if you e2e)

- [ ] Fresh profile: no redirect to `:5175` / `:8085` / Zitadel login
- [ ] No `GET /v1/whoami` from checkout
- [ ] Merchant Playwright storage must **not** be reused as the buyer

## K17.4 Exit

- [ ] Grep clean
- [ ] Unblocked for K22
