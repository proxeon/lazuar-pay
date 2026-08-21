# Q10 — IsolationTests scan Vite `package.json`

**Track:** CI / isolation · **Depends:** M23, K21  
**Analysis:** [10](../10-ci-observability-decommission.md) §3.1  
**Goal:** Merchant and checkout cannot smuggle Hub types.

---

## Q10.1 Keep (existing)

- [x] IsolationTests `src/**/*.cs` bans stay (`MediatR`, `Modules.One`, `BuildingBlocks`)
- [x] csproj bans stay (`lazuar-api`, `Modules.`, `BuildingBlocks`, `MediatR`, `Lazuar.Api`, no `apps/lazuar-api`)

## Q10.2 Widen

- [x] IsolationTests also read `apps/lazuar-pay-merchant/package.json`
- [x] IsolationTests also read `apps/lazuar-pay-checkout/package.json`
- [x] Fail if either contains `@repo/api-types-ts`, `MediatR`, or `apps/lazuar-api`

## Q10.3 Must not

- [x] Do not weaken csproj bans to allow `BuildingBlocks` “for logging”
- [x] Do not scan Hub `lazuar-ops` as a Pay gate

## Q10.4 Exit

- [x] New assertions green in `task pay:test`
- [x] Unblocked for Q11
