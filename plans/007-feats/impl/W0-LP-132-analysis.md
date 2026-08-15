# W0 LP-132 — Outbound tenant webhooks must not silent-drop

**Ticket:** `LP-132` — Outbound webhooks that don’t silent-drop  
**Wave:** 0 (close loops)  
**Date:** 2026-08-16  
**Workspace:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay`  
**Status:** Analysis only. **Do not implement from this file.**  
**Sibling:** Redrive UI / redeliver API / delivery-log HTTP bodies are **`LP-133`**. Do not fold them in.

Tracker row (`plans/007-feats/00-implement-ids.md`): *“Outbound webhooks that don’t silent-drop (exact URL match bugs, missing fan-out, unpublished outbox).”*

This note answers: what is still true in code, what `docs/001-gaps/18-outbound-customer-webhooks.md` got wrong after later work, and the **smallest** change set that makes workspace tenant webhooks actually leave a durable row.

---

## 1. Verdict

| Bug class named in LP-132 | Live status (2026-08-16) |
|---------------------------|--------------------------|
| Exact URL match (`TenantWebhookEndpoint.Url == TargetUrl`) | **Already gone** in `OutboundWebhookEventHandlers`. `TargetUrl` is ignored. |
| Missing fan-out (workspace multi-endpoint) | **Already shipped.** Handler enqueues every active endpoint that `AcceptsEvent`. |
| Unpublished outbox | **Still broken** for the CaaS lifecycle path. Second-hop Commerce handlers `PublishAsync` `OutboundWebhookRequestedIntegrationEvent` onto `CommerceEventBus` and **never `SaveChanges`**. `InMemoryEventBus` then disposes that scope. The row never hits `commerce.OutboxMessages`, so One never writes `WebhookDeliveryOutbox`, so the dispatcher has nothing to POST. |

**LP-132 remaining P0 is only the unpublished second hop.** URL-match and fan-out work when the integration event actually persists.

That is why the tracker can still say `P` while inventory docs (`plans/007-feats/01-lazuar-feature-inventory.md`, `14-developer-dx-api-webhooks.md`) honestly say “silent product-URL equality is gone.” Both are right: the gate is gone; **activate / resume / suspend / cancel / order.completed still vanish after the handler returns.**

Direct-publish paths that `PublishAsync` **and** `SaveChanges` on the same module `DbContext` already work:

- `payment_link.paid` (Commerce open checkout)
- `subscription.past_due` (billing engine + gateway-fail handler)
- `payment.completed` / `payment.failed` (Payments M2M)
- `invoice.valid` / `invoice.invalid` (LHDN poller `finally` saves the same `LhdnDbContext`)

---

## 2. Scope cut

### In LP-132

1. Persist `OutboundWebhookRequestedIntegrationEvent` from Commerce **second-hop** handlers.  
2. Prove with tests that use a real `OutboxEventBus<CommerceDbContext>`, not a mock `IEventBus`.  
3. Keep existing fan-out / null-`TargetUrl` behavior. Do **not** reintroduce URL equality.

### Not LP-132 (do not implement here)

| Item | Owner |
|------|--------|
| `POST …/webhooks/logs/{id}/redeliver`, Resend button, request/response body in logs | **LP-133** |
| Signature `t=,v1=`, lease/`SKIP LOCKED`, 4xx permanent fail, retry schedule | Already shipped; LP-133 only if changing redrive semantics |
| LHDN `/lhdn/webhooks` zombie registry vs `one.TenantWebhookEndpoints` | Honesty / façade debt; not this silent-drop |
| `invoice.submitted` / `invoice.cancelled` / `payment.refunded` catalog | Wave 1 `LP-135` |
| Payload enrichment beyond what publishers already emit | Residual B.4.2 |
| SSRF (HTTPS-to-metadata still possible) | Residual B.4.2 |
| Restore POSTs to product-form HTTP `FulfillmentTargets` | Explicitly abandoned in B.4.1 MVP; form copy already says so |
| Failure row when workspace has **zero** endpoints | Deferred in B.4.1 (structured log only) |

---

## 3. `docs/001-gaps/18-outbound-customer-webhooks.md` is stale

Treat 18 as a **pre-platform snapshot** (paths even say `lazuar-hub`). Do not re-implement its P0 as if it were current.

| 18 claim | Live code |
|----------|-----------|
| One endpoint per org (`FirstOrDefault` by `OrganizationId`) | Multi-row `TenantWebhookEndpoint`; create/list/rotate/disable |
| Enqueue only if `e.Url == @event.TargetUrl && e.IsActive` | Fan-out to all active endpoints; `TargetUrl` unused |
| HMAC hex of body only | `OutboundWebhookSignature`: `t={unix},v1={hex}` over `{t}.{body}` |
| No event filters | `EnabledEvents`; empty = all (`AcceptsEvent`) |
| `custom_payment_link` early-return, no outbound | Emits `payment_link.paid` with `TargetUrl: null` then `SaveChanges` |
| LHDN fire-and-forget `WebhookSenderService` | R42/R43: publish onto One path |
| Zero outbound tests | `OutboundWebhookTests`, `OutboundWebhookClaimTests`, publisher unit tests exist — **none prove second-hop persist** |
| Product textarea is the delivery URL | Ops copy now points at Developer → Outbound Webhooks; HTTP lines are leftover |

Solidification already checked B.4.1 URL-match (`plans/001-backend/001-backend-solidification-checklist.md`). It did **not** catch unpublished second-hop outbox. `docs/001-gaps/20-architecture-intent-vs-implementation.md` still quotes the deleted equality filter — also stale.

---

## 4. As-is pipeline

```
Commerce / Payments / Lhdn
  PublishAsync(OutboundWebhookRequested) → module OutboxMessages
        │
        │  requires SaveChanges on THAT module DbContext
        ▼
*OutboxPublisherJob  →  InMemoryEventBus (new DI scope per publish)
        │
        ▼
One.OutboundWebhookEventHandlers
  fan-out → one.WebhookDeliveryOutboxes  (handler SaveChanges — OK)
        │
        ▼
OutboundWebhookDispatcherJob
  SKIP LOCKED claim → HMAC POST → SUCCESS / FAILED
```

Rule from `apps/lazuar-api/docs/001-cross-module-communication.md`:

> `PublishAsync` then a **single `SaveChanges`** that covers domain + outbox.

`OutboxEventBus<T>` only `AddAsync`s an `OutboxMessage`. It never flushes.

`InMemoryEventBus.PublishAsync` **always** `CreateScope()`. Handlers resolved in that scope get a **fresh** keyed `*EventBus` / `DbContext`. When the handler returns, the scope is disposed. Anything still `Added` and unsaved is gone. The publisher job’s later `SaveChanges` is on a **different** context and cannot see it.

That is the unpublished-outbox class.

---

## 5. File evidence

### 5.1 `TenantWebhookEndpoint`

`/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Domain/TenantWebhookEndpoint.cs`

- Multi-endpoint aggregate: `Url`, encrypted `SecretKey`, `IsActive`, `EnabledEvents`.  
- `AcceptsEvent`: blank/whitespace event type → false; empty filter → all; otherwise ordinal-ignore-case contains.  
- `Update` / `RotateSecret` / `Disable` exist. No URL uniqueness in the domain (create command de-dupes same URL).  
- **No equality-to-`TargetUrl` API.** Nothing here causes silent drop.

### 5.2 `OutboundWebhookEventHandlers`

`/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/EventHandlers/OutboundWebhookEventHandlers.cs`

```csharp
// Fan-out to ALL active workspace endpoints. No product-URL equality gate.
var endpoints = await _dbContext.TenantWebhookEndpoints
    .IgnoreQueryFilters()
    .Where(e => e.OrganizationId == @event.OrganizationId && e.IsActive)
    .ToListAsync();
```

Then `AcceptsEvent`, wrap `{ id, event_type, created_at, data }`, one `WebhookDeliveryOutbox` per match, **`SaveChangesAsync`**.

Skips (logged, not URL-match):

- Zero active endpoints → information log, return.  
- Active endpoints but none subscribe to this type → information log, return.

`TargetUrl` is not read. Tests already pass a *different* `TargetUrl` and still enqueue (`OutboundWebhookTests.FanOut_*`).

One is subscribed in `UseOneSubscriptions`:

`eventBus.Subscribe<OutboundWebhookRequestedIntegrationEvent, OutboundWebhookEventHandlers>()`.

### 5.3 `OutboundWebhookDispatcherJob`

`/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/Workers/OutboundWebhookDispatcherJob.cs`

- Poll `OutboundWebhookInterval`; claim 50 `PENDING` with `FOR UPDATE SKIP LOCKED` + `ClaimLease`.  
- Headers: `X-Lazuar-Signature`, `X-Lazuar-Event`, `X-Lazuar-Delivery-Id`, `X-Lazuar-Webhook-Id`.  
- 2xx → success; 4xx → `RecordPermanentFailure`; 5xx/transport → `RecordFailure` (max 5, `2^n` minutes).  
- Missing/inactive endpoint → `RecordFailure("Endpoint not found or inactive.")` — **visible log row**, not a silent drop.  
- Does not enqueue. If `WebhookDeliveryOutbox` is empty, this job is idle. **Not the LP-132 bug.** Leave it for LP-133.

### 5.4 Event contract

`/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Contracts/Events/OutboundWebhookRequestedIntegrationEvent.cs`

Comment already says: null/empty `TargetUrl` = fan-out; non-empty is reserved and **must not** be an equality gate. All live publishers pass `TargetUrl: null`. Do not start using `TargetUrl` in this ticket.

---

## 6. Publisher matrix

| Event | Publisher | Bus | `SaveChanges` after `PublishAsync`? | Reaches One today? |
|-------|-----------|-----|-------------------------------------|--------------------|
| `subscription.activated` | `SubscriptionLifecycleIntegrationEventHandlers` | Commerce | **No** | **No** |
| `subscription.resumed` | same | Commerce | **No** | **No** |
| `subscription.suspended` | same | Commerce | **No** | **No** |
| `subscription.canceled` | same | Commerce | **No** | **No** |
| `order.completed` | `OrderCompletedIntegrationEventHandler` | Commerce | **No** | **No** |
| `subscription.past_due` | `BillingEngineJob` | Commerce | Yes (`db.SaveChanges` after process) | Yes |
| `subscription.past_due` | `GatewayPaymentFailedIntegrationEventHandler` | Commerce | Yes (`_dbContext.SaveChanges`) | Yes |
| `payment_link.paid` | `GatewayPaymentCompleted…OpenCheckout` | Commerce | Yes (`_repository.SaveChanges`) | Yes |
| `payment.completed` / `payment.failed` | `IntegrationCheckoutGatewayEventsHandler` | Payments | Yes (`_sessions.SaveChanges` = same `PaymentsDbContext`) | Yes |
| `invoice.valid` / `invoice.invalid` | `DispatchExternalWebhookCommandHandler` | Lhdn | Poller `finally { db.SaveChanges }` on **same scope** as MediatR | Yes (verify, do not change unless a test shows a split scope) |

First-hop sources of the **lifecycle** events (these *do* persist `Subscription*` / `OrderCompleted` because the originating handler/job saves):

- `GatewayPaymentCompletedIntegrationEventHandler` open checkout + renewal/resume  
- `ProcessZeroAmountCheckoutCommand`  
- `RecordSubscriberPaymentCommandHandler` / `MarkCheckoutAsPaidOfflineCommandHandler` / `CreateManualSubscriberCommandHandler`  
- `DunningEngineJob.PastDue` final CANCEL/SUSPEND (`db.SaveChanges` in `DunningEngineJob.Claim`)  
- `CancelAdminSubscriptionCommandHandler` / `CancelPortalSubscriptionCommandHandler`  
- `ClientProfileAnonymizedIntegrationEventHandler`

All of those rely on the second hop. Fixing the two Application handlers fixes every source.

Dunning / billing only walk `FulfillmentTargets` for `internal:…` → `FulfillmentRequestedIntegrationEvent`. HTTP lines are not posted. Product form already says that. **Do not** add a new product-URL dispatcher in LP-132.

---

## 7. Gaps (LP-132 only)

### G1 — P0 unpublished second hop (the remaining silent drop)

`SubscriptionLifecycleIntegrationEventHandlers.PublishAsync` (`…/Commerce/Application/EventHandlers/SubscriptionLifecycleIntegrationEventHandlers.cs`):

```csharp
await _eventBus.PublishAsync(new OutboundWebhookRequestedIntegrationEvent(
    organizationId, TargetUrl: null, eventType, payloadElement));
// no _repository.SaveChangesAsync()
```

`OrderCompletedIntegrationEventHandler` only injects `IEventBus`. Same missing flush.

Runtime sequence:

1. Checkout/dunning/etc. persists `SubscriptionActivatedIntegrationEvent` (or `OrderCompleted`) on `commerce.OutboxMessages`.  
2. `CommerceOutboxPublisherJob` deserializes and `InMemoryEventBus.PublishAsync`.  
3. New scope → lifecycle / order handler → `OutboxEventBus` `Add`s `OutboundWebhookRequested`.  
4. Scope dispose. Row never written.  
5. Publisher job marks the *lifecycle* outbox processed.  
6. One handler never runs. Delivery logs stay empty. Merchant sees a configured workspace URL and silence.

This matches the ticket phrase **unpublished outbox** more closely than any leftover URL compare.

`ICommerceRepository.SaveChangesAsync` already exists and uses the same scoped `CommerceDbContext` as `CommerceEventBus`. That is the intended flush.

### G2 — Exact URL match

**No remaining code path.** Do not add `e.Url == @event.TargetUrl`. Existing tests (`FanOut_Enqueues_All_Active_Endpoints_Without_Url_Match`, `FanOut_SubscriptionActivated_Without_Product_Url_Match`) are the regression lock.

### G3 — Missing fan-out

**Handler fan-out is done.** “Missing fan-out” in production is G1: fan-out never runs because the request event is not durable.

### G4 — Tests give false confidence

| Test | What it proves | What it misses |
|------|----------------|----------------|
| `SubscriptionLifecycleWebhookTests` | Mock `PublishAsync`, `TargetUrl == null` | Persist |
| `OutboundWebhookTests` | Fan-out + signing **given** a delivered integration event | How the event got into One |
| `IntegrationCheckoutOutboundWebhookTests` | Publish + `_sessions.SaveCount` | Good pattern; Payments only |
| `GatewayPaymentFailedIntegrationEventHandlerTests` | Mock publish | First hop already saves domain; bus is mock |
| *(none)* | `OrderCompleted` → outbound | Entire event |

There is **no** test that `HandleAsync` on the lifecycle/order handlers leaves a row in `commerce.OutboxMessages`.

### G5 — Residuals (record only)

- Zero endpoints: log, no `WebhookDeliveryOutbox` failure row (B.4.1 deferred).  
- `EnabledEvents` miss: log, skip — correct filter, not a bug.  
- Product HTTP `FulfillmentTargets`: never delivered; copy is honest enough.  
- LHDN SDK still writes `lhdn.WebhookSubscriptions` while dispatch fans out to One only.  
- `TargetUrl` is a dead parameter. Leave it; deleting it is unrelated churn.

---

## 8. Minimal changes

Two production files. No schema. No dispatcher. No UI. No `TargetUrl` semantics.

### 8.1 `SubscriptionLifecycleIntegrationEventHandlers`

After the `PublishAsync` in `PublishAsync(...)`:

```csharp
await _repository.SaveChangesAsync();
```

Repository is already constructed. This flushes the `OutboxMessage` on the same `CommerceDbContext` the keyed bus used.

### 8.2 `OrderCompletedIntegrationEventHandler`

- Inject `ICommerceRepository` (Application port; same layer as the existing lifecycle handler).  
- After `PublishAsync`, `await _repository.SaveChangesAsync()`.

Do **not** inject `CommerceDbContext` into Application.

### 8.3 Explicitly do not

- Change `OutboundWebhookEventHandlers` fan-out / `AcceptsEvent`.  
- Change `OutboundWebhookDispatcherJob` (LP-133).  
- Publish `OutboundWebhookRequested` again from `GatewayPaymentCompleted` / dunning (double delivery once G1 is fixed).  
- Change `InMemoryEventBus` to reuse the publisher scope (cross-module blast radius).  
- Auto-`SaveChanges` inside `OutboxEventBus.PublishAsync` (breaks “one commit with domain”).  
- POST to product fulfillment HTTP URLs.  
- Write a no-endpoint failure outbox row (unless you want a tiny extra; not required to close LP-132).

### 8.4 Optional hardening (only if a test proves it)

`DispatchExternalWebhookCommandHandler` does not save. Today `LhdnStatusPollingJob` creates one scope, `IMediator.Send`s in that scope, and `finally` `SaveChanges`s the same `LhdnDbContext`. If a future MediatR scope split appears, add an Lhdn unit-of-work flush in the command. **Do not do this speculatively.**

---

## 9. Tests

Put new tests next to the publishers they lock, using the real outbox — the pattern `IntegrationCheckoutOutboundWebhookTests` almost has (`SaveCount`), but **assert `OutboxMessages`**, not a mock.

Reuse `InMemoryDb.CreateOptions<CommerceDbContext>()` + `FakeExecutionContextAccessor.EmptyTenant()` as in `GatewayPaymentFailedIntegrationEventHandlerTests`.

### 9.1 Must add — persist (this is the ticket)

**File:** `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/OutboundWebhookRequestedPersistTests.cs` (new)

Harness:

- `CommerceDbContext` in-memory  
- `IEventBus` = `new OutboxEventBus<CommerceDbContext>(db)`  
- `ICommerceRepository` = `new CommerceRepository(db)`  
- CRM substitute returning null email is fine  

Cases:

1. `SubscriptionActivated_HandleAsync_Writes_OutboundWebhookRequested_To_Commerce_Outbox`  
   - `HandleAsync(SubscriptionActivatedIntegrationEvent)`  
   - `db.OutboxMessages` has exactly one row  
   - `Type` contains `OutboundWebhookRequestedIntegrationEvent`  
   - `Data` JSON has `"EventType":"subscription.activated"` (or whatever the serializer emits) and null/omitted `TargetUrl`  
   - `ProcessedAt` is null  

2. Same for `subscription.resumed`, `subscription.suspended`, `subscription.canceled` (one parameterized test).  

3. `OrderCompleted_HandleAsync_Writes_OutboundWebhookRequested_To_Commerce_Outbox`  
   - New handler with repository  
   - Event type `order.completed`  
   - One outbox row after `HandleAsync` **with no extra `db.SaveChanges` from the test**  

**These tests fail on current main** (0 rows). That is the proof.

### 9.2 Keep — fan-out / no URL gate

`apps/lazuar-api/tests/Lazuar.ModuleTests/One/OutboundWebhookTests.cs`

- `FanOut_SubscriptionActivated_Without_Product_Url_Match`  
- `FanOut_Enqueues_All_Active_Endpoints_Without_Url_Match`  
- `FanOut_With_Null_TargetUrl_Still_Delivers`  
- `AcceptsEvent_Empty_Means_All`  

Do not weaken them.

### 9.3 Keep / slightly extend — first-hop publishers already flush

- `SubscriptionLifecycleWebhookTests`: still assert `TargetUrl == null` on the mock. After 8.1, `repo.SaveChangesAsync()` will be invoked; default NSubstitute is fine. Optionally `await repo.Received(1).SaveChangesAsync()`.  
- `IntegrationCheckoutOutboundWebhookTests`: already asserts `SaveCount`. No change required for LP-132.  
- `GatewayPaymentFailedIntegrationEventHandlerTests`: already first-hop. No change.

### 9.4 Optional but high value — one hop through One

Not required if 9.1 is tight:

- Seed `TenantWebhookEndpoint` on `OneDbContext`  
- Call `OutboundWebhookEventHandlers.HandleAsync` with the payload deserialized from the Commerce outbox row  
- Assert `WebhookDeliveryOutboxes` count = 1, `Status == PENDING`, `EventType` matches  

Do **not** stand up `CommerceOutboxPublisherJob` + hosted `InMemoryEventBus` unless an existing test host already does that. Scope stays module-level.

### 9.5 Do not add in LP-132

- Dispatcher HTTP / signature vectors (already in `OutboundWebhookTests` / `OutboundWebhookClaimTests`)  
- Redeliver API tests (LP-133)  
- LHDN dual-registry tests  
- Product-form HTTP fulfillment POST tests  

---

## 10. Acceptance (when someone implements)

LP-132 is done when:

1. A workspace with an active `TenantWebhookEndpoint` (empty `EnabledEvents`) receives a `PENDING` `WebhookDeliveryOutbox` after `subscription.activated` and after `order.completed`, without any product fulfillment URL.  
2. `commerce.OutboxMessages` contains `OutboundWebhookRequestedIntegrationEvent` immediately after the lifecycle/order handler returns (no second `SaveChanges` from a worker).  
3. Two active endpoints both get a row; an `EnabledEvents` miss does not; an inactive endpoint does not.  
4. A non-null leftover `TargetUrl` still does not gate (existing One tests).  
5. Delivery Logs in ops start showing lifecycle events after the outbox job + dispatcher run (manual). **No new Resend button.**

---

## 11. Suggested implementation order

1. Write 9.1 tests; confirm they fail (0 `OutboxMessages`).  
2. Add `SaveChangesAsync` in the two Application handlers.  
3. Re-run Commerce + One webhook module tests.  
4. Stop. Open LP-133 for redrive.

---

## 12. Paths (absolute)

| Path | Role in LP-132 |
|------|----------------|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Domain/TenantWebhookEndpoint.cs` | Registry + `AcceptsEvent` — no change |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Domain/WebhookDeliveryOutbox.cs` | Delivery row — no change |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/EventHandlers/OutboundWebhookEventHandlers.cs` | Fan-out — no change |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/Workers/OutboundWebhookDispatcherJob.cs` | HTTP — no change (LP-133) |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Contracts/Events/OutboundWebhookRequestedIntegrationEvent.cs` | Contract — no change |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Application/EventHandlers/SubscriptionLifecycleIntegrationEventHandlers.cs` | **Change: SaveChanges** |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Application/EventHandlers/OrderCompletedIntegrationEventHandler.cs` | **Change: repo + SaveChanges** |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/EventHandlers/GatewayPaymentCompletedIntegrationEventHandler.OpenCheckout.cs` | First-hop `payment_link.paid` — already saves |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/EventHandlers/GatewayPaymentFailedIntegrationEventHandler.cs` | First-hop `past_due` — already saves |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/BillingEngineJob.cs` | First-hop `past_due` — already saves |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Infrastructure/EventHandlers/IntegrationCheckoutGatewayEventsHandler.cs` | First-hop `payment.*` — already saves |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Lhdn/Application/Commands/DispatchExternalWebhookCommand.cs` | LHDN enqueue — poller saves |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/BuildingBlocks/Infrastructure/OutboxEventBus.cs` | Add-only publish |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/BuildingBlocks/Infrastructure/InMemoryEventBus.cs` | New scope per publish — why second hop dies |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/001-gaps/18-outbound-customer-webhooks.md` | Historical; do not re-apply |

---

## 13. Bottom line

LP-132 is **not** “rebuild outbound webhooks.” URL-match and workspace fan-out already match B.4.1 / D4.

The remaining lie: CaaS `subscription.*` and `order.completed` look published in unit tests and never reach `commerce.OutboxMessages`. One handler and the dispatcher never see them.

Two `SaveChangesAsync` calls plus persist tests close the ticket. Redrive stays LP-133.
