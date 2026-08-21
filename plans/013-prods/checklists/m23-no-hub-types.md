# M23 — No Hub types

**Track:** Merchant · **Depends:** M11  
**Analysis:** [04](../04-merchant-frontend.md)  
**Goal:** Merchant `package.json` must not depend on Hub OpenAPI types.

---

## M23.1 Ban

- [x] `package.json` must **not** depend on `@repo/api-types-ts`
- [x] No Hub `openapi-fetch` generated Hub `paths` (`/one/auth/me`, `/admin/commerce/*`, `/lhdn/*`)
- [x] Do not copy ops `src/lib/api-client.ts`

## M23.2 Later (not this phase unless needed)

- [x] Later `@repo/pay-types-ts` **only** when generated from `pay-spec`
- [x] Hand-written whoami types in the SPA are acceptable until then
- [x] Isolation test may wait for Q10 — **grep now**

## M23.3 Must not

- [x] Do not hook `pay-spec` into Hub `task gen` / honesty allowlist (Q13)
- [x] One calls (if any) use One types / `@lazuar/one-client`, not a union Hub `paths`

## M23.4 Exit

- [x] Merchant deps stay Pay/One-shaped; Hub types absent
- [x] Unblocked for M24
