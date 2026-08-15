# W0-LP-133 — Signed outbound deliveries + retry + redrive

**Date:** 2026-08-16  
**Ticket:** `LP-133` — Signed deliveries + retry + redrive (Wave 0)  
**Tracker:** [00-checklist-tracker.md](../00-checklist-tracker.md) row `LP-133` = **P**  
**Pair:** `LP-132` is “no silent drop / fan-out”. This file is **only** `LP-133`. Do not reopen LHDN zombie CRUD, event catalog honesty, or payload richness.  
**Does not ship product code.** Implementation is a later pass.

Wave 0 intent ([00-evaluation.md](../00-evaluation.md), [00-implement-ids.md](../00-implement-ids.md)):

> Outbound: no silent drop, **redrive from delivery logs**.

Support must replay hop 2 after a 4xx (wrong `whsec_`, mapping 422) or after the 5-attempt budget dies — without SQL.

---

## 1. Verdict

| Slice | Status | Evidence |
|-------|--------|----------|
| Signed delivery (`t=,v1=` HMAC) | **Shipped** | `OutboundWebhookSignature` + dispatcher headers + sample verify + unit vectors |
| Automatic retry + backoff + lease | **Shipped** | `WebhookDeliveryOutbox` + `OutboundWebhookDispatcherJob` + claim tests |
| Delivery logs (read) | **Shipped** (`LP-134` = Y) | `GET …/webhooks/logs` + `DeliveryLogsPage` |
| Redrive API | **Missing** | No POST. TypeSpec has no redeliver route. `IOneRepository` cannot load outbox rows. |
| Ops button | **Missing** | Page copy: *“Redeliver / resend is not available yet (API residual).”* Refresh only. |

**LP-133 stays Partial until a human or `webhooks.endpoints:manage` key can re-queue a logged delivery and the dispatcher actually POSTs it again, freshly signed.**

Signing and retry are not the work. Do not rewrite them. The hole is the human escape hatch.

---

## 2. Not this ticket

Leave these residuals alone (they are other IDs / later waves):

| Residual | Why not LP-133 |
|----------|----------------|
| Silent-drop / product-URL match | Fixed. `LP-132`. Tests in `OutboundWebhookTests`. |
| LHDN `POST /lhdn/webhooks` zombie registry | Honesty / `LP-DEV-010`. Dispatch already fans out to One. |
| Delivery log HTTP status / response body / payload | `DX-034`. Logs stay shallow. Redrive does not need bodies. |
| `test.ping` / CLI listen | `DX-036` / Wave 4. |
| Auto-disable endpoint, jitter, multi-day tail | Explicit residuals in `14-developer-dx-api-webhooks.md`. 5 × exponential minutes is enough once redrive exists. |
| Full SSRF (HTTPS to link-local / RFC1918) | `WebhookUrlValidator` already blocks userinfo + non-loopback HTTP. |
| Official `@lazuar/webhooks` package | `DX-032`. Sample + C# helper already match. |
| Schema columns / new tables | Not required. |

---

## 3. Ground truth (read 2026-08-16)

### 3.1 Signing — `OutboundWebhookSignature`

**File:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/Workers/OutboundWebhookSignature.cs`

| Rule | Implementation |
|------|----------------|
| Header | `X-Lazuar-Signature: t={unix},v1={hmac_hex}` |
| Signed material | `{unix}.{rawBody}` (UTF-8) |
| MAC | HMAC-SHA256, **lowercase** hex |
| Key | Full secret string including `whsec_` prefix — do not strip |
| Verify | `TryVerify`: parse `t`/`v1`, optional 300s skew, `CryptographicOperations.FixedTimeEquals` |
| Extra headers (dispatcher, not this type) | `X-Lazuar-Event`, `X-Lazuar-Delivery-Id` = outbox row id, `X-Lazuar-Webhook-Id` = endpoint id |

Receiver twin: `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/examples/hub-cashier-next/lib/webhook-verify.ts` (comments name the C# SSoT). Stable vector in `OutboundWebhookTests.Signature_P09_SaasEnvelope_Vector_Is_Stable`.

**Redrive implication:** every dispatcher attempt already recomputes `t` as `DateTimeOffset.UtcNow`. A re-queued row is re-signed with a fresh timestamp. Receivers inside the 300s window accept it. Do not persist an old signature.

This is Standard Webhooks–**style**, not the official library header names. Do not “fix” header names in this ticket.

### 3.2 Outbox + retry — `WebhookDeliveryOutbox`

**File:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Domain/WebhookDeliveryOutbox.cs`  
**Table:** `one.WebhookDeliveryOutboxes` (no extra columns needed)

| Field | Role |
|-------|------|
| `Id` | UUIDv7. Becomes `X-Lazuar-Delivery-Id`. |
| `OrganizationId` / `EndpointId` | Tenant + target |
| `EventType` / `Payload` | Type + **exact JSON bytes** the dispatcher POSTs (envelope already baked at enqueue) |
| `AttemptCount` | Incremented on success and on failure |
| `NextAttemptAt` | Due time; also the **claim lease** |
| `Status` | `PENDING` → `SUCCESS` or `FAILED` only. UI aliases `RETRYING`/`DELIVERED` are unused. |
| `LastError` | Last HTTP/transport string |
| `CreatedAt` | Enqueue time |

State machine (today):

```text
ctor            → PENDING, AttemptCount=0, NextAttemptAt=now
ClaimLease      → NextAttemptAt = leaseUntil  (only if still PENDING)
RecordSuccess   → SUCCESS, AttemptCount++
RecordFailure   → AttemptCount++
                  if AttemptCount >= 5 → FAILED
                  else NextAttemptAt = now + 2^AttemptCount minutes
                  Status stays PENDING until the 5th fail
RecordPermanentFailure → AttemptCount++, FAILED  (any 4xx)
```

Backoff after increment:

| Fail # | `AttemptCount` after | Next wait | Status |
|--------|----------------------|-----------|--------|
| 1 | 1 | 2 min | PENDING |
| 2 | 2 | 4 min | PENDING |
| 3 | 3 | 8 min | PENDING |
| 4 | 4 | 16 min | PENDING |
| 5 | 5 | — | FAILED |

~30 minutes then dead. No jitter. **There is no `ScheduleRedrive` / clone helper.** After `FAILED`, nothing in domain will move the row back to `PENDING`. If someone naively set `Status=PENDING` without zeroing `AttemptCount`, the next `RecordFailure` would immediately `FAILED` again (`>= 5`).

`IOneRepository` has **zero** outbox methods. Enqueue writes `OneDbContext` from `OutboundWebhookEventHandlers`. Logs read via `IOneQueryService.GetWorkspaceWebhookLogsAsync` (last 50, **no payload**). A command cannot clone a delivery today without new repository surface.

### 3.3 Dispatcher + claim — `OutboundWebhookDispatcherJob`

**File:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/Workers/OutboundWebhookDispatcherJob.cs`

- Poll: `Workers:OutboundWebhookInterval` default **10s**
- Claim: `FOR UPDATE SKIP LOCKED`, `Status='PENDING' AND NextAttemptAt <= NOW()`, batch **50**, lease `Workers:ClaimLeaseDuration` default **2 min**
- HTTP: named client `"DeveloperWebhooks"`, **15s** timeout
- Inactive / missing endpoint → `RecordFailure("Endpoint not found or inactive.")` (counts toward the 5, not permanent)
- `IsPermanentHttpFailure` = **any 4xx** (`>= 400 && < 500`), not only 401/422
- 5xx / transport exception → `RecordFailure` (retry)
- Secret: `ResolveSigningSecret` decrypts; lazy-encrypts leftover plaintext `whsec_` rows

Workers must `IgnoreQueryFilters()` — `IMustHaveTenant` filter is fail-closed on empty ambient `TenantId` (`PlatformDbContext`).

**Redrive implication:** the dispatcher is already the send path. Redrive only needs to insert (or re-arm) a `PENDING` row with `NextAttemptAt <= now`. Do not add a second HTTP sender.

### 3.4 Enqueue (payload identity)

**File:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/EventHandlers/OutboundWebhookEventHandlers.cs`

Fan-out writes one outbox row per matching active endpoint. Payload is:

```json
{ "id": "<uuid v7>", "event_type": "…", "created_at": "…", "data": { … } }
```

That envelope `id` is the **event id**. Outbox `Id` is the **delivery id**. They are different. Cloning a row copies the same payload (same event id) and mints a new delivery id. That is what Stripe-style resend needs.

### 3.5 Logs API + ops UI

**API:** `GET /api/v1/one/workspaces/{id}/webhooks/logs`  
**Impl:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/Endpoints/WebhookEndpoints.cs`  
**Query:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/Services/OneQueryService.cs`  
**Contract:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/api-spec/modules/one/models/webhook.tsp` (`WebhookDeliveryLogDto`)  
**UI:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/modules/workspace/pages/DeliveryLogsPage.tsx`

DTO today: `id`, `event_type`, `status`, `attempt_count`, `last_error?`, `created_at`. No `endpoint_id`.

Auth (`CanAccessWorkspaceWebhooksAsync`):

| Actor | Read logs (`manageRequired: false`) | Mutate (create / rotate / disable) |
|-------|-------------------------------------|------------------------------------|
| System admin | yes | yes |
| Human with workspace membership | yes | ADMIN / SUPER_ADMIN only |
| `API_CLIENT` + `webhooks.endpoints:manage` and `TenantId == path id` | yes | yes |
| Cross-tenant key | no (Unauthorized) | no |

Existing mutate routes return `Unauthorized()` for all denials (not 403). Match that.

UI: table + expand for `last_error`. Header **Refresh** invalidates `["developer-webhook-logs", workspaceId]`. Expand footer explicitly says redeliver is an API residual. No `useMutation`. Button patterns to copy live on `DeveloperSettingsPage` (`useMutation` + `toast` + `window.confirm` for rotate).

Ops has **no** frontend test runner (`lint` = `tsc --noEmit` only). Do not invent a Vitest suite for the button.

### 3.6 Tests that already cover LP-133’s shipped slices

| File | What it proves |
|------|----------------|
| `apps/lazuar-api/tests/Lazuar.ModuleTests/One/OutboundWebhookTests.cs` | Signature vector, format, verify accept/reject, fan-out without URL match |
| `apps/lazuar-api/tests/Lazuar.ModuleTests/One/OutboundWebhookClaimTests.cs` | Permanent 4xx, `IsPermanentHttpFailure`, lease, in-memory claim |
| `apps/lazuar-api/tests/Lazuar.ModuleTests/One/WebhookEndpointLifecycleTests.cs` | Create/rotate/disable, decrypt + lazy-encrypt for HMAC |
| `apps/lazuar-api/tests/Lazuar.ModuleTests/One/ProvisionAuraWorkspaceTests.cs` | `CanAccessWorkspaceWebhooksAsync` (scope, IDOR, org admin) |

**Missing today:** `RecordFailure` backoff schedule (2/4/8/16 then FAILED). Permanent-fail is tested; the exponential path is not. Add one domain test while touching this file. No dispatcher HTTP mock exists — do not add one for redrive.

### 3.7 Receiver idempotency (why clone, not reset)

Sample cashier (`examples/hub-cashier-next/app/webhooks/hub/payments/route.ts`):

1. Dedupe **first** on `X-Lazuar-Delivery-Id` (`hasSeenDelivery`)
2. Then business keys (`order.status === "paid"`, `gateway_transaction_id`)

Reset-in-place of a `SUCCESS` row would reuse the same delivery id → sample (and anyone who copied it) ACKs `already: true` and never re-runs. Failed deliveries that never called `markDeliverySeen` would still work on reset — but SUCCESS resend would be a lie.

Clone = new delivery id, same envelope `id`. Delivery-id dedupe does not swallow the replay. Business-key dedupe still prevents double unlock. That is the Stripe shape.

### 3.8 Docs lie (retry honesty)

| Doc | Claim | Code |
|-----|-------|------|
| `apps/lazuar-docs/docs/integrations/webhooks.md` | 401: “Hub **may still retry**” | All 4xx → `RecordPermanentFailure` |
| `apps/lazuar-docs/docs/guide/architecture-who-does-what.md` | Bad signature: “**May retry** delivery” | Same |

After a 401, the only recovery is **redrive** (once they fix `whsec_`). The docs currently imply automatic retry. One-paragraph fix belongs in this ticket because it is the redrive story.

---

## 4. What is missing

### 4.1 Redrive API

There is no:

```http
POST /api/v1/one/workspaces/{id}/webhooks/logs/{deliveryId}/redeliver
```

No command, no TypeSpec operation, no generated `paths` entry (`packages/api-types-ts` `post?: never` on `…/webhooks/logs`). Ops `openapi-fetch` client cannot type a call that does not exist.

### 4.2 Domain / repository

- No way to load `WebhookDeliveryOutbox` by `(organizationId, deliveryId)` through `IOneRepository`
- No way to insert a second outbox row through the repository
- Query snapshot **drops `Payload`**, so logs DTO cannot be the source of a replay

### 4.3 Ops button

`DeliveryLogsPage` has no per-row action. The residual sentence is the product hole.

### 4.4 Contract honesty

Adding only a Minimal API route without TypeSpec (or the reverse) fails `task contracts:honesty`. Both must land in the same change.

---

## 5. Recommended design (minimal)

**One behavior:** clone a new `WebhookDeliveryOutbox` from an existing FAILED or SUCCESS row. Leave the original row untouched. Dispatcher sends the clone on the next 10s tick.

```text
Ops / machine  POST …/logs/{deliveryId}/redeliver
        → load source (IgnoreQueryFilters, org + id)
        → 404 if missing / other workspace
        → 409 if Status == PENDING  (already queued or in backoff)
        → 409 if endpoint missing or !IsActive
        → insert new PENDING (same EndpointId, EventType, Payload; AttemptCount=0; NextAttemptAt=now)
        → 200 WebhookDeliveryLogDto of the NEW row

OutboundWebhookDispatcherJob  (unchanged)
        → claim clone → POST + fresh X-Lazuar-Signature
        → X-Lazuar-Delivery-Id = clone.Id
        → body bytes identical (envelope id unchanged)
```

### 5.1 Why clone (not reset-in-place)

| | Reset same row | Clone new row |
|--|----------------|---------------|
| Files | Slightly fewer | +1 insert |
| `X-Lazuar-Delivery-Id` | Same → sample skips SUCCESS | New → receiver sees a new attempt |
| Audit | FAILED row disappears | FAILED/SUCCESS stays; new PENDING appears in last-50 |
| Attempt budget | Must zero `AttemptCount` or next fail is instantly FAILED | Fresh 5 |
| Dispatcher | Unchanged | Unchanged |
| Migration | None | None |

Clone is still minimal: no schema, no second sender, no new DTO model (reuse `WebhookDeliveryLogDto`).

### 5.2 Policy (keep it small)

| Case | Response |
|------|----------|
| FAILED or SUCCESS, endpoint active | 200 + new PENDING log DTO |
| PENDING (backoff or leased) | 409 — do not double-claim; UI hides the button |
| Unknown id or other workspace | 404 (same “not found” wording as rotate) |
| Endpoint disabled / missing | 409 — operator re-enables on Outbound Webhooks, then retries |
| Auth fail | 401 via existing `CanAccessWorkspaceWebhooksAsync(..., manageRequired: true)` |
| Double-click | Two clones possible. Accept. Integrators are already at-least-once. Disable the button while the mutation is in flight. |

Do **not** add rate limits, `RedriveCount` columns, or a confirmation token.

### 5.3 Auth

Same as rotate-secret: `manageRequired: true`.

- Humans: workspace ADMIN / SUPER_ADMIN  
- Machines: `webhooks.endpoints:manage` and path id == key tenant  
- Read-only members can see logs but cannot redrive  

### 5.4 Contract

TypeSpec (append only):

```tsp
@useAuth(BearerAuth)
@post
@route("/workspaces/{id}/webhooks/logs/{deliveryId}/redeliver")
@doc("Enqueue a new signed delivery of the same stored payload. New X-Lazuar-Delivery-Id; envelope id unchanged. FAILED and SUCCESS only.")
redeliverWorkspaceWebhookDelivery(
  @path id: string,
  @path deliveryId: string,
): WebhookDeliveryLogDto | LazuarApi.Core.ProblemDetailsResponse;
```

No new model. Reuse `WebhookDeliveryLogDto`.

Then `task gen` so `@repo/api-types-ts` grows:

`POST /one/workspaces/{id}/webhooks/logs/{deliveryId}/redeliver`

Minimal API maps the same path on `WebhookEndpoints` (after the GET logs route). Host prefix is `/api/v1`.

### 5.5 Command shape (mirror rotate)

Put this next to the other webhook commands in  
`/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Application/Commands/SaveWebhookCommand.cs`  
**or** a sibling `RedeliverWebhookDeliveryCommand.cs` (prefer a new file — `SaveWebhookCommand.cs` is already the create/update/rotate/disable pile).

```text
RedeliverWebhookDeliveryCommand(OrganizationId, DeliveryId)
  : ICommand<RedeliverWebhookDeliveryResult>
  Id = Guid.CreateVersion7()   // command id, not delivery id

RedeliverWebhookDeliveryResult(
  Guid Id, string EventType, string Status, int AttemptCount,
  string? LastError, DateTime CreatedAt)
```

Handler:

1. `GetWebhookDeliveryAsync(org, deliveryId)` — `IgnoreQueryFilters`, match **both** ids  
2. Throw `InvalidOperationException("Webhook delivery not found.")` if null (endpoint maps to 404, same as rotate)  
3. If `Status == "PENDING"` → `InvalidOperationException` containing a stable phrase the endpoint maps to 409 (e.g. `"Delivery is already pending."`)  
4. `GetWebhookEndpointByIdAsync(source.EndpointId)` — 409 if null, other org, or `!IsActive`  
5. `AddWebhookDelivery(new WebhookDeliveryOutbox(org, endpointId, eventType, source.Payload))`  
6. `SaveChangesAsync`  
7. Return the **new** row snapshot  

MediatR already scans `Modules.One.Application` (`MediatRRegistrationExtensions`). No DI line.

### 5.6 Repository (two methods)

`IOneRepository` + `OneRepository`:

```csharp
Task<WebhookDeliveryOutbox?> GetWebhookDeliveryAsync(
    Guid organizationId, Guid deliveryId, CancellationToken ct = default);

void AddWebhookDelivery(WebhookDeliveryOutbox delivery);
```

`Get` must `IgnoreQueryFilters()` and filter `OrganizationId == organizationId && Id == deliveryId` (IDOR fail-closed).  
`Add` is `_context.WebhookDeliveryOutboxes.Add`.  
Do not put `OneDbContext` in Application (architecture tests).

### 5.7 Endpoint

`WebhookEndpoints.MapPost("/workspaces/{id:guid}/webhooks/logs/{deliveryId:guid}/redeliver")`:

- `CanAccessWorkspaceWebhooksAsync(..., manageRequired: true)` → `Unauthorized()`  
- `mediator.Send(new RedeliverWebhookDeliveryCommand(id, deliveryId))`  
- Map `not found` → 404  
- Map pending / inactive-endpoint → 409 (string or ProblemDetails — match nearby handlers; rotate uses 400 for `InvalidOperationException`. Prefer **409** for conflict so the UI can toast “already queued / endpoint disabled” without treating it as a bad request. If you want zero new status mapping, 400 is acceptable and smaller — pick **409** for PENDING/inactive, 404 for missing.)  
- 200 `WebhookDeliveryLogDto` of the clone  

### 5.8 Ops button

`DeliveryLogsPage.tsx` only:

- `useMutation` → `client.POST("/one/workspaces/{id}/webhooks/logs/{deliveryId}/redeliver", { params: { path: { id: activeWorkspaceId, deliveryId: log.id } } })`  
- After `task gen`, the path is typed. Until gen, TS will fail — gen is part of the same change.  
- Button on **FAILED** (always) and **SUCCESS** (Resend). Hide on PENDING.  
- `stopPropagation` so the row expand click does not fire.  
- Confirm: “Send this event again to the same endpoint? Receivers must be idempotent.”  
- `toast` success/error (copy `DeveloperSettingsPage`)  
- `invalidateQueries({ queryKey: ["developer-webhook-logs", activeWorkspaceId] })`  
- Delete the residual sentence; replace with one line: “Redeliver queues a new signed attempt. Automatic retry still runs for 5xx.”  
- No new page, no filters, no endpoint column.

`pnpm --filter lazuar-ops lint` (`tsc --noEmit`) is the UI check.

### 5.9 Docs (same ticket, three lines)

1. VitePress webhooks.md HTTP table: **401/4xx = permanent FAILED; fix secret/mapping then Redeliver from Delivery Logs.**  
2. Same file: new subsection `## Redeliver` with the POST path.  
3. `architecture-who-does-what.md` row “Bad webhook signature”: Hub does **not** retry; operator redrives.

Do not expand the developers-hub catalog in this ticket.

---

## 6. File list (implementation pass)

Touch only these. No migration.

| Path | Change |
|------|--------|
| `apps/lazuar-api/Modules/One/Application/IOneRepository.cs` | `GetWebhookDeliveryAsync` + `AddWebhookDelivery` |
| `apps/lazuar-api/Modules/One/Infrastructure/Repositories/OneRepository.cs` | Same, `IgnoreQueryFilters` |
| `apps/lazuar-api/Modules/One/Application/Commands/RedeliverWebhookDeliveryCommand.cs` | **New.** Command + handler |
| `apps/lazuar-api/Modules/One/Infrastructure/Endpoints/WebhookEndpoints.cs` | `MapPost` redeliver |
| `packages/api-spec/modules/one/routes.tsp` | POST route |
| `packages/api-spec/modules/one/models/webhook.tsp` | Optional `@doc` on `WebhookDeliveryLogDto` only — no new fields |
| generated clients via `task gen` | `api-types-ts`, `api-types-dotnet`, `dist/one/openapi.yaml` |
| `apps/lazuar-ops/src/modules/workspace/pages/DeliveryLogsPage.tsx` | Button + mutation |
| `apps/lazuar-api/tests/Lazuar.ModuleTests/One/OutboundWebhookClaimTests.cs` | One backoff schedule test |
| `apps/lazuar-api/tests/Lazuar.ModuleTests/One/RedeliverWebhookDeliveryTests.cs` | **New.** Command tests |
| `apps/lazuar-docs/docs/integrations/webhooks.md` | 4xx honesty + redeliver |
| `apps/lazuar-docs/docs/guide/architecture-who-does-what.md` | 401 row |

**Do not edit:** `OutboundWebhookSignature.cs`, `OutboundWebhookDispatcherJob.cs`, `WebhookDeliveryOutbox` state machine (clone uses the existing ctor), `OutboundWebhookEventHandlers.cs`, LHDN webhook façade, TypeSpec payment webhook DTO, sample cashier (already idempotent enough).

---

## 7. Tests (this ticket)

All in `Lazuar.ModuleTests`. Prefer in-memory `OneDbContext` + real `OneRepository` + handler (same factory as `OutboundWebhookClaimTests.CreateDb`). Do not stand up HTTP or the hosted job.

### 7.1 Domain retry (gap in existing file)

`OutboundWebhookClaimTests`:

- `RecordFailure_Backoff_ThenFailedAtFive`  
  - After fails 1–4: `Status == PENDING`, `NextAttemptAt` ≈ now + 2/4/8/16 minutes  
  - After 5th: `Status == FAILED`

### 7.2 Redeliver command (new file)

Seed: org id, active `TenantWebhookEndpoint`, one outbox row.

| Test | Assert |
|------|--------|
| `Redeliver_Failed_InsertsPendingClone_SamePayload_NewId` | Source still FAILED; clone PENDING, attempt 0, same `EventType`/`Payload`/`EndpointId`/`OrganizationId`, different `Id`, `NextAttemptAt` ≈ now |
| `Redeliver_Success_InsertsPendingClone` | Same, source stays SUCCESS |
| `Redeliver_Pending_Throws` | Message mentions pending; row count unchanged |
| `Redeliver_Missing_ThrowsNotFound` | |
| `Redeliver_WrongWorkspace_ThrowsNotFound` | Delivery exists under org B; command for org A → not found; no clone |
| `Redeliver_InactiveEndpoint_Throws` | Soft-disabled endpoint; no clone |
| `Redeliver_Clone_IsClaimable` | After redeliver, `ClaimPendingDeliveriesAsync` returns the **clone**, not the FAILED original |

Auth is already covered by `ProvisionAuraWorkspaceTests` (`manageRequired: true`). Do not duplicate unless the new endpoint accidentally uses `manageRequired: false` — one test that the map uses `true` is enough (or just code-review; the endpoint should call the same helper as rotate).

No ops-page unit tests. `tsc --noEmit` after gen.

### 7.3 Honesty / gen

```text
task gen
task contracts:honesty
```

OpenAPI path must equal Minimal `MapPost`. Allowlist stays empty of phantoms.

---

## 8. Acceptance (LP-133 → Y)

1. A FAILED delivery in Ops → **Redeliver** → new PENDING row in the table within refresh; within ~10s (dispatcher tick) a new POST hits the endpoint with a **new** `X-Lazuar-Delivery-Id` and a **fresh** `t=,v1=` over the **same** body.  
2. A SUCCESS delivery can be resent the same way; receiver that keys on envelope / checkout / txn does not double-fulfill.  
3. PENDING has no button; API 409 if called.  
4. Cross-tenant delivery id → 404. Read-only member → 401.  
5. 4xx from the receiver still marks the **clone** FAILED immediately; operator can redrive again after they fix the app.  
6. Existing signature vectors and fan-out tests still pass. No migration.  
7. VitePress no longer says 401 “may retry.”

---

## 9. Sequencing

```text
1. IOneRepository + OneRepository
2. RedeliverWebhookDeliveryCommand + tests (red + green without HTTP)
3. WebhookEndpoints MapPost
4. TypeSpec + task gen + contracts:honesty
5. DeliveryLogsPage button
6. Docs 4xx / redeliver
7. Backoff unit test in OutboundWebhookClaimTests
```

Estimated size: ~1 command file, ~30 lines of endpoint, ~40 lines of ops UI, ~8 tests. That is the whole ticket.
