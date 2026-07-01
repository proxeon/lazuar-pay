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

## Money-handling policies

- **Chargebacks:** Stripe `charge.dispute.created` webhooks flow through `ProcessGatewayWebhookCommandHandler` → `GatewayDisputeCreatedIntegrationEvent` → `ChargebackClawbackHandler`, which recomputes the granted credits from the disputed amount and claws them back via `ClawbackCreditsCommand` (clamps at zero; spent credits are a loss).
- **FX buffer:** Tenants pay in MYR; Resend/WhatsApp charge in USD. The `Credits:Costs` rates must be priced above provider cost with a buffer for FX movement. Review when USD/MYR moves >10%.
- **Refunds:** Accidental top-ups are handled manually (Stripe refund + `ClawbackCreditsCommand` or credit grant). No self-serve refund flow in v1.
- **Credit expiry:** Not yet implemented. Free starter credits currently never expire. Grant-level expiry (FIFO consumption) is a deferred follow-up — see `plan/backup/396-credit-monetization.md` Phase 6.
- **Observability:** Per-tenant credit burn is queryable via `GetCreditBalanceWithHistoryAsync`. Full per-action revenue / per-channel margin dashboards are a deferred follow-up.
