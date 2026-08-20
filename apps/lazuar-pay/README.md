# Lazuar Pay (focused host)

New money process. Not the modular monolith in `apps/lazuar-api/`.

Plan: [`plans/011-new-lazuar-pay`](../../plans/011-new-lazuar-pay/README.md). Tracker: [`11-checklist.md`](../../plans/011-new-lazuar-pay/11-checklist.md).

- One solution, one host, one test project.
- Listen on **8081** so the old API can keep **8080**.
- Merchants come from **lazuar-one** (not yet wired). Do not copy `Modules/One`.
- Do not add MediatR, per-module DbContexts, or a project reference into `apps/lazuar-api`.

```bash
task pay:test
task pay:dev          # http://localhost:8081/health  and  /v1/health
# or
pnpm --filter lazuar-pay dev
```

TypeSpec: [`packages/pay-spec`](../../packages/pay-spec/) (`task pay:spec`). Not `packages/api-spec`.

Compose still points at `apps/lazuar-api`. Swap later when S1 dogfood is real.
