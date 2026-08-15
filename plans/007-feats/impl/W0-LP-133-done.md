# W0-LP-133 — done

Ops / `webhooks.endpoints:manage` can redrive a logged delivery without SQL. `POST /api/v1/one/workspaces/{id}/webhooks/logs/{deliveryId}/redeliver` clones a new `PENDING` outbox row (same endpoint, event type, payload; attempt 0; due now). The original FAILED / SUCCESS row stays. The existing dispatcher POSTs the clone on the next tick with a **new** `X-Lazuar-Delivery-Id` and a **fresh** `t=,v1=` over the same body.

PENDING → 409. Missing / cross-tenant delivery → 404. Disabled or missing endpoint → 409. Auth is `manageRequired: true` (same helper as rotate). No schema, no second sender, no payload bodies on logs.

Signing, retry/backoff, and fan-out were not rewritten.

## Files changed

### API

- `apps/lazuar-api/Modules/One/Application/IOneRepository.cs` — `GetWebhookDeliveryAsync` + `AddWebhookDelivery`
- `apps/lazuar-api/Modules/One/Infrastructure/Repositories/OneRepository.cs` — `IgnoreQueryFilters`, org+id match
- `apps/lazuar-api/Modules/One/Application/Commands/RedeliverWebhookDeliveryCommand.cs` — **New.** Clone command + handler
- `apps/lazuar-api/Modules/One/Infrastructure/Endpoints/WebhookEndpoints.cs` — `MapPost` redeliver (401 / 404 / 409 / 200)

### Contract

- `packages/api-spec/modules/one/routes.tsp` — `redeliverWorkspaceWebhookDelivery`
- `packages/api-spec/modules/one/models/webhook.tsp` — `@doc` on `WebhookDeliveryLogDto` only
- `packages/api-types-ts/src/index.ts` — `POST /one/workspaces/{id}/webhooks/logs/{deliveryId}/redeliver`
- `packages/api-types-dotnet/Lazuar.ApiContracts.cs` — generated DTO doc

### Ops

- `apps/lazuar-ops/src/modules/workspace/pages/DeliveryLogsPage.tsx` — Redeliver (FAILED) / Resend (SUCCESS); hidden on PENDING; confirm + toast + invalidate

### Docs

- `apps/lazuar-docs/docs/integrations/webhooks.md` — 4xx = permanent FAILED; `## Redeliver`
- `apps/lazuar-docs/docs/guide/architecture-who-does-what.md` — bad signature: Hub does **not** retry; operator redrives

### Tests

- `apps/lazuar-api/tests/Lazuar.ModuleTests/One/RedeliverWebhookDeliveryTests.cs` — **New.** Failed/success clone, pending, missing, wrong workspace, inactive endpoint, clone is claimable
- `apps/lazuar-api/tests/Lazuar.ModuleTests/One/OutboundWebhookClaimTests.cs` — `RecordFailure_Backoff_ThenFailedAtFive`

### Tracker

- `plans/007-feats/00-checklist-tracker.md` — LP-133 Lazuar **P → Y**

## Tests run

- `Lazuar.ModuleTests` filter `RedeliverWebhookDeliveryTests|OutboundWebhookClaimTests|OutboundWebhookTests|WebhookEndpointLifecycleTests|ProvisionAuraWorkspaceTests` — **80 passed**
- `task gen` + `task contracts:honesty` — **OK** (129 OpenAPI, 136 Minimal, 7 impl_only)
- `pnpm --filter lazuar-ops lint` (`tsc --noEmit`) — **OK**

Not committed. Not pushed.

Dispatcher / signature / outbox state machine unchanged. Logs stay shallow (no payload). LHDN registry and event catalog left alone.
