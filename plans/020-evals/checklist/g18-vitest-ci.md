# G18 — Merchant + checkout vitest in CI

**Track:** G · **Depends:** K00  
**Analysis:** [`../06-host-production.md`](../06-host-production.md) §13.2.9  
**Goal:** 002 UI tests actually run on the PR.

**Why:** Merchant/checkout vitest exists. Root `ci.yml` is still Hub (`task gen`, `apps/lazuar-api`, Hub honesty). `Taskfile.yml` `pay:test` is NUnit only unless someone added UI.

**Related files**

| Path | Role today |
|------|------------|
| `.github/workflows/ci.yml` | Hub contracts + `apps/lazuar-api` |
| `Taskfile.yml` | `pay:test`, `pay:spec`, `pay:merchant` |
| `apps/lazuar-pay-merchant/package.json` | `test` script |
| `apps/lazuar-pay-checkout/package.json` | `test` script |
| `apps/lazuar-pay-merchant/src/auth/bearerToken.ts` | M23 |

**Current (`6d730d15`):** No `lazuar-pay` job in `ci.yml` (verify when implementing; add one).

---

## G18.1

- [x] `ci.yml` job `pay` (or sibling) runs `pnpm --filter lazuar-pay-merchant test` and checkout test
- [x] Checkout `vite build` with explicit `VITE_PAY_API_URL` dummy https if Dockerfile requires it
- [x] Do not skip on “unit is grep”

## G18.2 Exit

- [x] Unblocked for G19
