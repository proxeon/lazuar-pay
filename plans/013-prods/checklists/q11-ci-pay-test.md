# Q11 — CI runs Pay tests

**Track:** CI / isolation · **Depends:** existing tests  
**Analysis:** [10](../10-ci-observability-decommission.md) §2.7, §3.1  
**Goal:** A green `main` is not only Hub. Add a focused job; do not sunset Hub yet.

---

## Q11.1 Job

- [ ] GitHub job runs `dotnet test apps/lazuar-pay/Lazuar.Pay.slnx` (or `task pay:test`)
- [ ] Today `.github/workflows/ci.yml` `dotnet` is Hub `Lazuar.slnx` in `apps/lazuar-api` — **keep it**
- [ ] Do **not** replace Hub until [parked-hub-cutover.md](./parked-hub-cutover.md)

## Q11.2 Shape

- [ ] Working directory / slnx is focused Pay, not `apps/lazuar-api`
- [ ] Hermetic: no Zitadel, no One compose, no Hub `lazuar_mvp` required
- [ ] Job may live in `ci.yml` or `pay.yml` — do not fold into Hub `dotnet` as a second slnx

## Q11.3 Must not

- [ ] `needs: [dotnet]` on the Hub job
- [ ] Delete Hub tests to go green
- [ ] `pnpm test` turbo as this job

## Q11.4 Exit

- [ ] Pay job visible on PR
- [ ] Unblocked for Q12
