# D16 — `task pay:db:migrate`

**Track:** Database · **Depends:** D12, D14  
**Analysis:** [03](../03-host-production-seams.md), [09](../09-data-migration.md)  
**Goal:** Apply the one migrator **before** Kestrel takes checkout traffic. Not nine-at-boot.

---

## D16.1 Task

- [x] Add `pay:db:migrate` in root `Taskfile.yml` **next to** `pay:test`
- [x] It runs the D10 pick once: SQL runner **or** `dotnet ef database update --context PayDbContext`
- [x] Document the command in `apps/lazuar-pay/README.md` (or the task `desc`)
- [x] **Not** a copy of Hub `api:db:migrate` (nine `--context *DbContext` lines)

## D16.2 When it runs

- [x] Prefer migrate (init / CI / this task) **before** the process accepts checkouts
- [x] Single-replica boot migrate of **one** context is acceptable as a start — still not nine
- [x] Do not call `MigrateAllModuleDatabasesAsync`

## D16.3 Target

- [x] Against `ConnectionStrings:Pay` → `lazuar_pay` on **5435** locally
- [x] Never Hub `lazuar_mvp`, never One `lazuar`

## D16.4 Exit

- [x] `task pay:db:migrate` (or the documented one-liner) applies a clean database
- [x] IsolationTests still ban MediatR
- [x] Unblocked for D17 (D19–D29 tables may start)
