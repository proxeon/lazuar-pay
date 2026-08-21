# Q12 — CI Vite build merchant + checkout

**Track:** CI / isolation · **Depends:** M11, K15  
**Analysis:** [10](../10-ci-observability-decommission.md) §3.3  
**Goal:** Broken `:5178` / `:5179` cannot hide behind Hub `dotnet`.

---

## Q12.1 Commands

- [x] CI `pnpm --filter lazuar-pay-merchant build` (or `check-types` + `build`)
- [x] CI `pnpm --filter lazuar-pay-checkout build`
- [x] Filter those two packages only
- [x] Job may share Node/pnpm setup with Q11; it is not Hub `ghcr.yml` ops/portal bake

## Q12.2 Must not

- [x] `pnpm build` / turbo whole workspace as the Pay gate
- [x] `pnpm --filter lazuar-ops` / portal / admin as a Pay gate
- [x] Playwright as the first frontend lock

## Q12.3 Exit

- [x] Both Vite apps typecheck/build on PR
- [x] Unblocked for Q13
