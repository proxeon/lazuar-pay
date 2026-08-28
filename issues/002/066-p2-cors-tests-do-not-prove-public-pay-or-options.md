---
number: "066"
id: PAY-HOST-005
severity: P2
status: resolved
source: plans/019-evals/07-identity-authz-cors.md
head: "9f04ad58"
---

# 066 — CORS tests do not prove `/v1/pay/*` or OPTIONS

- **Severity:** P2
- **Status:** resolved
- **Source:** `plans/019-evals/07-identity-authz-cors.md` B9
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

`CorsTests` only `GET /health` with `Origin`. Default policy applies to all routes **if** `UseCors()` runs, so 5179 would likely work. K14.4 says tests cover public pay GET/POST/OPTIONS. They do not. `CorsTests` also use bare `WebApplicationFactory<Program>()` (Development, real DB + `MigrateAsync` — 021), not `PayApiFactory` Testing. CORS assertions can fail for database reasons.

## Related files

- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Hosting/CorsTests.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Program.cs` **80** `UseCors()`.

## Reproduction

Read CorsTests. No `/v1/pay` path. No OPTIONS.

## Blast radius

False checklist tick. Production CORS (049) untested on the buyer door.

## Suggested fix

Move CorsTests onto `PayApiFactory`. Add GET/POST/OPTIONS `/v1/pay/{token}` with Origin 5179. Keep 3003 deny. Do not tick K14 until those exist.

## Tests

This issue **is** the test work.

## Source reports

- `plans/019-evals/07-identity-authz-cors.md` §B9
- `plans/019-evals/09-tests-inventory.md` CORS
- `plans/019-evals/01-pay-host-seams.md` §B9
