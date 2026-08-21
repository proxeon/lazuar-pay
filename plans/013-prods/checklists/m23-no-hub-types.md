# M23 — No Hub types

**Track:** Merchant · **Depends:** M11  
**Analysis:** [04](../04-merchant-frontend.md)  
**Goal:** Merchant `package.json` must not depend on Hub OpenAPI types.

---

## M23.1 Ban

- [ ] `package.json` must **not** depend on `@repo/api-types-ts`
- [ ] No Hub `openapi-fetch` generated Hub `paths` (`/one/auth/me`, `/admin/commerce/*`, `/lhdn/*`)
- [ ] Do not copy ops `src/lib/api-client.ts`

## M23.2 Later (not this phase unless needed)

- [ ] Later `@repo/pay-types-ts` **only** when generated from `pay-spec`
- [ ] Hand-written whoami types in the SPA are acceptable until then
- [ ] Isolation test may wait for Q10 — **grep now**

## M23.3 Must not

- [ ] Do not hook `pay-spec` into Hub `task gen` / honesty allowlist (Q13)
- [ ] One calls (if any) use One types / `@lazuar/one-client`, not a union Hub `paths`

## M23.4 Exit

- [ ] Merchant deps stay Pay/One-shaped; Hub types absent
- [ ] Unblocked for M24
