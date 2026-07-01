# AGENTS.md — Repo Conventions for Agents & Contributors

## Monetization model

Lazuar's SaaS is **free**. The only inbound monetization is the **prepaid utility credit wallet** (`TenantCreditBalance`): tenants buy credits, credits fund high-value actions (email, broadcast, LHDN submission). No subscriptions, no tiers, no Paddle. See `plan/backup/396-credit-monetization.md`.

## Branching

- Never commit to `main`. Branch with `feat/`, `fix/`, `chore/`, or `docs/` prefixes.
- Work on one phase of the plan per branch where practical.

## Build & test commands (via Taskfile)

```bash
task api:build              # restore + build the .NET solution
task api:test               # architecture + integration tests
task api:db:migrate         # apply all module migrations
task api:migrations:add MODULE=Billing NAME=AddX   # add a migration
task dev                    # docker infra + API hot-reload
```

Always run `task api:build` and `task api:test` before requesting review. There is no CI gate on PRs yet (see `.github/workflows/ci.yml` — added but verify it runs).

## API contracts (TypeSpec)

All new endpoints are defined in `packages/api-spec/` (`*.tsp`) and types are generated into `packages/api-types-ts` (TS) and `packages/api-types-dotnet` (C#). Do **not** hand-write API types; define them in TypeSpec and regenerate:

```bash
pnpm --filter @repo/api-spec build
```

## Module boundaries (enforced by `Lazuar.ArchitectureTests`)

The .NET API is a modular monolith. Every module follows `Modules/{Name}/{Application,Contracts,Domain,Infrastructure}`.

- **Domain** is fully isolated: no references to Application, Infrastructure, or any other module.
- **Application** must not reference its own Infrastructure.
- **Application & Infrastructure** may only reference other modules through their `*.Contracts` namespace — never `*.Domain`, `*.Application`, or `*.Infrastructure` of another module.

Cross-module billing work (e.g. Messaging deducting credits) must go through `Modules.Billing.Contracts`.

## Money-path rules

- Credit deduction must be atomic and idempotent. Use the wallet row-version for optimistic concurrency and the idempotency-key log to prevent double-deducts on retry.
- Never deduct without a sufficiency check. `TenantCreditBalance.Deduct` throws on insufficient balance.
- Money-path logic ships with unit tests in `tests/Modules.Billing.Tests/`.
