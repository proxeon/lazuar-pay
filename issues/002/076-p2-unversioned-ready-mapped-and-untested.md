---
number: "076"
id: PAY-SPEC-010
severity: P2
status: resolved
source: plans/019-evals/08-contracts-spec-honesty.md
head: "9f04ad58"
---

# 076 — Unversioned `GET /ready` mapped and untested

- **Severity:** P2
- **Status:** resolved
- **Source:** `plans/019-evals/08-contracts-spec-honesty.md` bugs 11
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

`GET /ready` (Postgres `CanConnect`) is mapped beside `/health` and `/v1/health`. TypeSpec has `/v1/health` only. Orchestration that probes `/ready` has no unit lock. `HealthTests` cover `/health` and `/v1/health`; Cors/Health factories boot Development (021).

Do not confuse with `GET /v1/orgs/{id}/ready` (078 dummy member ready).

## Related files

- `apps/lazuar-pay/src/Lazuar.Pay/Hosting/HealthEndpoints.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Hosting/HealthTests.cs`
- `packages/pay-spec/main.tsp` Health interface.

## Reproduction

Grep tests for `"/ready"`. Likely none.

## Blast radius

K8/orchestration. InMemory `/ready` vs Npgsql CanConnect differ.

## Suggested fix

Test `/ready` 200 on Testing factory (define what InMemory returns). Document host-only unversioned `/ready` in pay-spec **or** allowlist it in the honesty scrape (067).

## Tests

- Missing: GET `/ready` 200 in Testing.

## Source reports

- `plans/019-evals/08-contracts-spec-honesty.md` §Bugs 11
- `plans/019-evals/01-pay-host-seams.md` G4
