# E11 — `examples/pay-node` packaging

**Track:** E · **Depends:** E10, K00  
**Analysis:** [`../09-spec-docs-sample.md`](../09-spec-docs-sample.md); 006 G5 judgment  
**Goal:** Copy-out friendly. Not in default product CI turbo.

**Why:** 006 put Hub sample in workspace but excluded turbo. Repeat for Pay. Plain `fetch` so a stranger can copy the folder out.

**Related files**

| Path | Role today |
|------|------------|
| `pnpm-workspace.yaml` | `examples/*`? |
| `turbo.json` | Filters |
| `package.json` | `example:cashier` Hub script |
| `examples/hub-cashier-next/package.json` | Contrast packaging |
| IsolationTests Vite | Must not import `@repo/api-types-ts` |

**Current (`6d730d15`):** No `examples/pay-node`.

---

## E11.1

- [ ] Path `examples/pay-node`
- [ ] Package name `@examples/pay-node` (or unscoped)
- [ ] Port **3021** (not 3002–3005, not Hub 3020 unless documented)
- [ ] pnpm workspace include `examples/*` if not already; turbo **exclude** from product build
- [ ] No Dockerfile
- [ ] No `@repo/api-types-ts`, no `@lazuar/one-client` as a **required** runtime dep
- [ ] Plain `fetch`

## E11.2 Must not

- [ ] Do not wait on npm publish
- [ ] Do not import Pay C#

## E11.3 Exit

- [ ] Unblocked for E12
