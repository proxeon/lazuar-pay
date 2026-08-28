---
number: "080"
id: PAY-HOST-006
severity: P1
status: resolved
source: plans/019-evals/10-honesty-bugs-gaps.md
head: "9f04ad58"
---

# 080 — CORS and compose still laptop-shaped; no Pay image

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/019-evals/10-honesty-bugs-gaps.md` P1-10 / P1-14 (also `03-checkout-frontend.md` G4, `07` B10)
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

Production-shaped holes that are not 049’s allow-list code:

- Root `docker-compose.yml` still points at `apps/lazuar-api` (Hub). Pay has `apps/lazuar-pay/docker-compose.pay.yml` for Postgres 5435; there is **no** Pay Dockerfile / bake target for 8081 + two Vite apps.
- CORS is hardcoded laptop origins (049).
- Preview origins 4178/4179 were added; still no production origin config.
- Merchant/checkout have no production env story (`VITE_*` 041, 050).

016 P1-14 OPEN. 018 did not close it. Not a live cash bug on localhost dogfood. It is why “we shipped Pay” cannot mean a URL a stranger can pay.

## Related files

- `docker-compose.yml` (repo root) — Hub.
- `apps/lazuar-pay/docker-compose.pay.yml`
- `apps/lazuar-pay/src/Lazuar.Pay/Program.cs` CORS.
- `docker-bake.hcl` — Pay apps?
- `apps/lazuar-pay-merchant` / `apps/lazuar-pay-checkout` — no Dockerfile.

## Reproduction

`docker compose up` at repo root. 8080 is Hub, not Pay. No 8081 container.

## Blast radius

Staging/prod. Operators who assume root compose is the new stack.

## Suggested fix

Do not retarget ops `:3003` or portal `:3004`. Add a Pay image **after** 001–006 money. Config CORS (049) and `VITE_*` (041, 050) in the same slice as the image. Keep Hub compose as museum until cutover (refuse).

## Tests

- Missing: none in hermetic suite. Deploy smoke is runbook.

## Source reports

- `plans/019-evals/10-honesty-bugs-gaps.md` §P1-10 §P1-14
- `plans/019-evals/03-checkout-frontend.md` G4
- `plans/019-evals/00-evaluation.md` refuse Hub cutover
