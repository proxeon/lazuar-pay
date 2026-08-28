# P19 — Optional `@repo/pay-types-ts` (parked)

**Track:** Parked  
**Analysis:** [`../09-spec-docs-sample.md`](../09-spec-docs-sample.md)  
**Unpark when:** pay-spec is stable **after** W29/U15 and first-party SPAs want to drop hand DTOs.

**Why parked:** Hub `@repo/api-types-ts` is Isolation-red. Generating types from tsp before kernel doors exist repeats 019 lag. Sample stays `fetch`.

**Related files**

| Path | Role today |
|------|------------|
| `packages/pay-spec/main.tsp` | Contract |
| `packages/api-types-ts/` | **Hub** museum |
| `apps/lazuar-pay-merchant/src/lib/payApi.ts` | Hand types |
| IsolationTests Vite `package.json` | Ban Hub types |
| `scripts/check-pay-openapi-honesty.mjs` | Path honesty |

**Current (`6d730d15`):** No Pay generated TS client.

---

## P19.1 Must not

- [ ] Do not import Hub `api-types-ts` into merchant/checkout
- [ ] Do not make `examples/pay-node` depend on generated types (006 G5)
- [ ] Do not wait on npm `@lazuar/one-client`
