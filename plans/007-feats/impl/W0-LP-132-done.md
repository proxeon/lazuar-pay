# W0-LP-132 — done

Commerce second-hop lifecycle handlers now **commit** `OutboundWebhookRequestedIntegrationEvent` onto `commerce.OutboxMessages`. `SubscriptionActivated` / `Resumed` / `Suspended` / `Canceled` and `OrderCompleted` `PublishAsync` then `SaveChangesAsync` on the same scoped `CommerceDbContext` as `OutboxEventBus`. One can fan out to workspace `TenantWebhookEndpoint`s after the Commerce publisher job drains; activate / resume / suspend / cancel / order.completed no longer vanish when `InMemoryEventBus` disposes the handler scope.

URL-match stay gone. Fan-out / ignore `TargetUrl` unchanged. No redrive UI / redeliver API (LP-133). No product-form HTTP fulfillment POSTs. No LHDN dual-registry work. No event-catalog expansion (LP-135).

## Files changed

### Commerce (hop 2 flush)

- `apps/lazuar-api/Modules/Commerce/Application/EventHandlers/SubscriptionLifecycleIntegrationEventHandlers.cs` — after `PublishAsync(OutboundWebhookRequested)`, `SaveChangesAsync` on existing `ICommerceRepository`
- `apps/lazuar-api/Modules/Commerce/Application/EventHandlers/OrderCompletedIntegrationEventHandler.cs` — inject `ICommerceRepository`; same flush after publish

### Tests

- `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/OutboundWebhookRequestedPersistTests.cs` — real `OutboxEventBus<CommerceDbContext>` + `CommerceRepository`; activated / resumed / suspended / canceled / `order.completed` each leave one unprocessed outbox row with matching `EventType` and null `TargetUrl` (no extra test `SaveChanges`)
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/SubscriptionLifecycleWebhookTests.cs` — mock bus still asserts `TargetUrl == null`; now also `repo.SaveChangesAsync`

### Tracker

- `plans/007-feats/00-checklist-tracker.md` — LP-132 Lazuar **P → Y**

## Tests run

- `Lazuar.ModuleTests` filter `OutboundWebhookRequestedPersistTests|SubscriptionLifecycleWebhookTests|OutboundWebhookTests|OutboundWebhookClaimTests|IntegrationCheckoutOutboundWebhookTests|GatewayPaymentFailedIntegrationEventHandlerTests` — **51 passed**

Not committed. Not pushed.

Fan-out / no URL gate remains locked in existing `OutboundWebhookTests`. Dispatcher / delivery-log redrive stays LP-133. First-hop `payment_link.paid` / `subscription.past_due` / `payment.*` already saved and were not changed.
