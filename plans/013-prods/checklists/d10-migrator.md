# D10 — One migrator

**Track:** Database · **Depends:** B00  
**Analysis:** [03](../03-host-production-seams.md), [09](../09-data-migration.md)  
**Goal:** One migrator, one history table. SQL **or** one `PayDbContext`. Not nine.  
**No money tables yet.**

---

## D10.1 Choose (XOR)

- [ ] SQL files in **one** folder under `apps/lazuar-pay` (e.g. `src/Lazuar.Pay/sql/`) **or**
- [ ] **One** `PayDbContext` in the host project + **one** migrations folder
- [ ] Write the pick into [`decisions.md`](./decisions.md) **Migrator** row if B00 left `_SQL files **or** one EF context_`
- [ ] Do not introduce DbUp **and** EF. Pick one tool

## D10.2 Refuse

- [ ] No nine `*DbContext` (`OneDbContext`, `CommerceDbContext`, …)
- [ ] No `MigrateAllModuleDatabasesAsync`
- [ ] No MediatR, `IRequest`, `AddMediatR`
- [ ] No second “Infrastructure” csproj because EF likes it
- [ ] IsolationTests still ban `MediatR` / `Modules.` / `BuildingBlocks` / `lazuar-api`

## D10.3 History

- [ ] One history table (EF `__EFMigrationsHistory` **or** one SQL runner table)
- [ ] Folder lives in `apps/lazuar-pay`, not `apps/lazuar-api/Modules/*/Infrastructure/Migrations`

## D10.4 Exit

- [ ] `decisions.md` Migrator is a real pick (SQL folder **or** `PayDbContext`), not a placeholder
- [ ] IsolationTests still green
- [ ] Unblocked for D11 (D14 may start)
