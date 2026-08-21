# D16 — `task pay:db:migrate`

**Track:** Database · **Depends:** D12, D14  
**Analysis:** [03](../03-host-production-seams.md), [09](../09-data-migration.md)  
**Goal:** Apply the one migrator **before** Kestrel takes checkout traffic. Not nine-at-boot.

---

## D16.1 Task

- [ ] Add `pay:db:migrate` in root `Taskfile.yml` **next to** `pay:test`
- [ ] It runs the D10 pick once: SQL runner **or** `dotnet ef database update --context PayDbContext`
- [ ] Document the command in `apps/lazuar-pay/README.md` (or the task `desc`)
- [ ] **Not** a copy of Hub `api:db:migrate` (nine `--context *DbContext` lines)

## D16.2 When it runs

- [ ] Prefer migrate (init / CI / this task) **before** the process accepts checkouts
- [ ] Single-replica boot migrate of **one** context is acceptable as a start — still not nine
- [ ] Do not call `MigrateAllModuleDatabasesAsync`

## D16.3 Target

- [ ] Against `ConnectionStrings:Pay` → `lazuar_pay` on **5435** locally
- [ ] Never Hub `lazuar_mvp`, never One `lazuar`

## D16.4 Exit

- [ ] `task pay:db:migrate` (or the documented one-liner) applies a clean database
- [ ] IsolationTests still ban MediatR
- [ ] Unblocked for D17 (D19–D29 tables may start)
