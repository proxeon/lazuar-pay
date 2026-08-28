---
number: "021"
id: PAY-HOST-002
severity: P1
status: resolved
source: plans/019-evals/01-pay-host-seams.md
head: "9f04ad58"
---

# 021 — Development `MigrateAsync` can crash the host; Cors/Health tests boot it

- **Severity:** P1
- **Status:** open
- **Source:** `plans/019-evals/01-pay-host-seams.md` B9
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

`Program.cs` runs `Database.MigrateAsync()` when `IsDevelopment()`. No try/catch. Failures that kill `task pay:dev`: Postgres down; `__EFMigrationsHistory` empty but tables exist; history/code skew; dirty `SlotKey` duplicates before the unique index.

`CorsTests` and some `HealthTests` use `new WebApplicationFactory<Program>()` **without** `UseEnvironment("Testing")`. The factory defaults to **Development**. That path registers Npgsql and runs `MigrateAsync` against `appsettings.Development.json`’s `localhost:5435`.

CI `pay` job runs `dotnet test` with **no Postgres service**. Tests either fail when 5435 is down, or **apply pending migrations to the dogfood DB** when 5435 is up — including `PaymentLinkPayers` as a side effect of a CORS test. Opposite of `PayApiFactory`’s isolated InMemory database.

## Related files

- `apps/lazuar-pay/src/Lazuar.Pay/Program.cs` **74–78**.
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Hosting/CorsTests.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Hosting/HealthTests.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Infrastructure/PayApiFactory.cs` — Testing + InMemory (the intended hermetic path).
- `.github/workflows/ci.yml` pay job — no Postgres service.

## Reproduction

Stop Postgres. Run `CorsTests`. Host factory tries migrate and fails. Or: leave 5435 up, run CORS tests, watch `__EFMigrationsHistory` on `lazuar_pay` change.

## Blast radius

CI flakiness; laptop DB mutated by unit tests; `task pay:dev` crash on drifted schema instead of a clear “run migrate” message.

## Suggested fix

Point Cors/Health tests at `PayApiFactory` (or `UseEnvironment("Testing")` plus InMemory). Never `MigrateAsync` from a test that is not about migrations. For the host: keep auto-migrate as a Development convenience, but catch and log “pay-db schema mismatch” instead of crashing, **or** document that `pay:dev` requires `pay:db:up` and a clean history. Add an explicit Testcontainers migration test if you care about PaymentLinkPayers on a real engine. Do not `EnsureCreated` on the laptop DB.

## Tests

- Cors/Health must not touch 5435. After the fix, `task pay:test` with Postgres down still greens those methods.

## Source reports

- `plans/019-evals/01-pay-host-seams.md` §B9
- `plans/019-evals/09-tests-inventory.md` InMemory vs raw factory
- `plans/019-evals/07-identity-authz-cors.md` §B9 factory note
