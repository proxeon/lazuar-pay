---
number: "067"
id: PAY-SPEC-001
severity: P1
status: resolved
source: plans/019-evals/08-contracts-spec-honesty.md
head: "9f04ad58"
---

# 067 — `dist/openapi.yaml` is stale vs `main.tsp`

- **Severity:** P1
- **Status:** open
- **Source:** `plans/019-evals/08-contracts-spec-honesty.md` bugs 1
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

Live host has 22 doors. TypeSpec describes 13. On-disk OpenAPI describes **11** of an older 13. Description still says checkout is a fixture. Gateways interface and `StartPayRequest` exist in tsp and not in yaml. Anyone reading dist (or generating types from it without recompile) documents a host that does not exist. `task pay:spec` compiles; freshness is not enforced. Dist is gitignored **and** a stale leftover can remain — worst of both.

Do not shrink the host to the spec. SPA clients are hand-written against the host (more honest).

## Related files

- `packages/pay-spec/main.tsp`
- `packages/pay-spec/dist/openapi.yaml`
- `packages/pay-spec/README.md`
- `apps/lazuar-pay/src/Lazuar.Pay/Program.cs` **82–92** — Map* inventory.

## Reproduction

Diff tsp operations vs dist paths vs `MapGet|Post|Put` under `apps/lazuar-pay/src`. Three different sets.

## Blast radius

Generated clients, README, “the spec is the host.” Kernel story (018-evals) cannot start from this yaml.

## Suggested fix

Grow tsp to live JSON (068–076), then `task pay:spec`. Pick: gitignore dist **or** commit it and dirty-check. New Pay scrape of `Map*` vs OpenAPI in job `pay`. Do not hook Hub `task gen` / `honesty-allowlist.yaml`.

## Tests

- Missing: scrape `MapGet|Post|Put` vs OpenAPI; fail CI on drift. Allowlist unversioned `/health` `/ready` if you keep them host-only.

## Source reports

- `plans/019-evals/08-contracts-spec-honesty.md` §Bugs 1, how to solve
- `plans/019-evals/00-evaluation.md` 22 vs 13 vs 11
