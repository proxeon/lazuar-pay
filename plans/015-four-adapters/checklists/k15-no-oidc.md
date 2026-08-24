# K15 — Still no Zitadel on :5179

**Track:** Checkout UI · **Depends:** A00  
**Analysis:** NP-CHK-007, NP-XX-013  
**IDs:** NP-CHK-007  
**Goal:** Buyers have no One account.

---

## K15.1

- [ ] Keep `locks.test.ts` forbidding `oidc-client-ts`, `react-oidc-context`, `@repo/api-types-ts`
- [ ] No Bearer on public GET/start
- [ ] Fail the program if login `:5175` appears on checkout

## K15.2 Exit

- [ ] Locks still green
