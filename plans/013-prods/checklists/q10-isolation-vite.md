# Q10 — IsolationTests scan Vite `package.json`

**Track:** CI / isolation · **Depends:** M23, K21  
**Analysis:** [10](../10-ci-observability-decommission.md) §3.1  
**Goal:** Merchant and checkout cannot smuggle Hub types.

---

## Q10.1 Keep (existing)

- [ ] IsolationTests `src/**/*.cs` bans stay (`MediatR`, `Modules.One`, `BuildingBlocks`)
- [ ] csproj bans stay (`lazuar-api`, `Modules.`, `BuildingBlocks`, `MediatR`, `Lazuar.Api`, no `apps/lazuar-api`)

## Q10.2 Widen

- [ ] IsolationTests also read `apps/lazuar-pay-merchant/package.json`
- [ ] IsolationTests also read `apps/lazuar-pay-checkout/package.json`
- [ ] Fail if either contains `@repo/api-types-ts`, `MediatR`, or `apps/lazuar-api`

## Q10.3 Must not

- [ ] Do not weaken csproj bans to allow `BuildingBlocks` “for logging”
- [ ] Do not scan Hub `lazuar-ops` as a Pay gate

## Q10.4 Exit

- [ ] New assertions green in `task pay:test`
- [ ] Unblocked for Q11
