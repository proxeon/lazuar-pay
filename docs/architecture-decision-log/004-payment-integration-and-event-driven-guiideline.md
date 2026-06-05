# Lazuar Modular Monolith: Payment Integration & Event-Driven Architecture Guidelines

This document provides a technical post-mortem and architectural guidelines based on payment-related integration issues resolved during the development of the Lazuar modular monolith. 

To maintain a strictly decoupled architecture, the platform enforces physical compilation boundaries (`.csproj` separation) and database schema isolation. The following three critical pitfalls were identified and resolved in the event-driven handoff between the **Payments** and **Community** modules.

---

## Pitfall 1: Stateless Webhook Metadata Loss (The Billplz Callback Limitation)

### Context & Architecture
In the multi-tenant monolith, the **Payments** module is designed to be completely stateless regarding checkout sessions; it only holds payment configurations and transaction logs. When a checkout is initiated, the Payments module generates a payment URL from the respective gateway adapter (e.g., Billplz or Stripe) and forgets it. 

The domain context—such as the target `subscription_id` and module `type`—is stored as metadata. When the payment gateway completes, its webhook callback must return this metadata so the Payments module can publish a `GatewayPaymentCompletedIntegrationEvent` carrying the context back to the subscribing module (e.g., Community).

### The Pitfall
While gateways like Stripe allow developers to attach a persistent `metadata` object that is guaranteed to return in the webhook payload, **Billplz does not return custom reference fields (such as `reference_1` or `reference_2`) in its server-to-server POST callback payload.** They are only returned in the client-side browser redirect query string.

Because the new modular monolith strictly forbids cross-database queries (the Payments module cannot query the `community.Subscriptions` table to match the Bill ID), the webhook handler received the callback but could not reconstruct the metadata. The integration event was published with an empty metadata payload, causing the Community module to silently ignore the payment.

### The Architectural Solution
To resolve this without violating context boundaries or making the Payments module stateful, **leverage the callback URL's query string to transfer state across third-party servers:**

1. **Gateway Generation:** When constructing the Billplz bill payload, append the metadata values directly to the `callback_url` as query parameters:
   $$\text{webhookUrl} = \text{ApiBaseUrl} + \text{/webhooks/payments/billplz/} + \text{tenantId} + \text{?type=community\_subscription\&subscription\_id=id}$$
2. **Endpoint Ingestion:** Modify the global webhook receiver endpoint (`Endpoints.cs` in Payments) to iterate through the request's query string and inject them into the `headers` collection with a specific prefix (e.g., `Query-`).
3. **Adapter Resolution:** Modify the gateway adapter (`BillplzGatewayAdapter.cs`) to reconstruct the metadata dictionary by reading these custom `Query-` headers.

---

## Pitfall 2: Static Generic Type Binding (The Event Bus Dispatch Trap)

### Context & Architecture
The monolith uses a transactional Outbox pattern to guarantee at-least-once delivery of integration events. The background worker (`OutboxPublisherJob`) polls the `payments.OutboxMessages` table, deserializes the JSON payload into an `IIntegrationEvent` concrete instance, and dispatches it locally using the `InMemoryEventBus`.

### The Pitfall
The `InMemoryEventBus` dispatch method was defined using .NET generics:
```csharp
public async Task PublishAsync<TEvent>(TEvent @event) where TEvent : IIntegrationEvent
```
Because the background job deserializes the event dynamically and casts it to the base interface:
```csharp
if (integrationEvent is IIntegrationEvent @event)
{
    await eventBus.PublishAsync(@event);
}
```
The C# compiler statically bound the generic type parameter `TEvent` at compile-time to **`IIntegrationEvent` (the interface)**, rather than the concrete subclass (`GatewayPaymentCompletedIntegrationEvent`). 

Inside the event bus, `typeof(TEvent).Name` evaluated to `"IIntegrationEvent"`. The bus looked up handlers registered under that name, found nothing, and exited silently with success. The outbox message was marked as processed, but the actual domain handlers were never executed.

### The Architectural Solution
Never rely on compile-time generic type arguments (`typeof(TEvent)`) when dispatching events from a dynamic, polymorphic queue (like an Outbox or Inbox). 

* **The Fix:** Refactor the event bus to resolve the event name using the concrete object type at runtime:
  ```csharp
  var eventName = @event.GetType().Name;
  ```
  This guarantees that even if the object is cast to a base interface or object type, the dispatcher will resolve the correct class name.

---

## Pitfall 3: Aggregate Root Tracking Mismatch (The EF Core Concurrency Trap)

### Context & Architecture
In Domain-Driven Design (DDD), write operations are governed strictly through Aggregate Roots. In our domain model, `CommunitySubscription` is the Aggregate Root, and `PaymentRecord` is a child entity. To activate a subscription, the command handler loads the subscription, calls the domain method `subscription.Activate()`, which instantiates a `PaymentRecord` and appends it to the internal navigation collection:
```csharp
_paymentRecords.Add(payment);
```
Finally, the repository persists the aggregate:
```csharp
await _subscriptionRepository.SaveChangesAsync();
```

### The Pitfall
The primary key `Id` of `PaymentRecord` was assigned a sequential, non-empty Guid (`Guid.CreateVersion7()`) inside its constructor to ensure optimal database index page writes. 

When the modified aggregate was saved, EF Core's change-tracker analyzed the new `PaymentRecord` entity. Because its primary key was **not the default value** (`Guid.Empty`), and because it was added to an already-tracked navigation collection, the tracker assumed it was an existing record loaded from the database that had been edited. It flagged its state as **`Modified`** instead of **`Added`**.

EF Core compiled and executed an `UPDATE` statement on the non-existent `PaymentRecord` row. PostgreSQL reported `0` rows affected, triggering a **`DbUpdateConcurrencyException`** and rolling back the transaction.

### The Architectural Solution
To prevent EF Core from misinterpreting pre-assigned IDs of child entities added through aggregate navigation collections, intercept the tracker state before database persistence.

* **The Fix:** Override `SaveChangesAsync` inside the module's specific DbContext (e.g., `CommunityDbContext`) to intercept and force the state of any new child entities back to `Added`:
  ```csharp
  foreach (var entry in ChangeTracker.Entries<PaymentRecord>())
  {
      if (entry.State == EntityState.Modified)
      {
          entry.State = EntityState.Added;
      }
  }
  ```
  This is clean, fully isolated to the module boundary, does not leak database details into your domain entities, and preserves the database benefits of sequential Version 7 UUIDs.

---

## Key Architectural Rules for Future Gateway Integrations (Stripe, Paddle, etc.)

To prevent these errors from resurfacing when integrating future payment providers, enforce the following development rules:

1. **Callback Urls Must Be Self-Describing:** If a payment gateway does not guarantee native metadata pass-through in its server-to-server webhook payload, you must append the state parameters (such as `type`, `subscription_id`, or `order_id`) directly to the `callback_url` query string during checkout generation.
2. **Stateless Webhook Parsing:** Webhook endpoints must capture both the raw body and the query string parameters, consolidating them into the `headers` collection before passing them to the gateway adapters.
3. **Runtime Type Dispatching:** All event dispatchers, outbox publishers, and message buses must resolve type names at runtime using `.GetType()` rather than compile-time generic types (`typeof(T)`).
4. **Aggregate Persistence Integrity:** When using rich domain model navigation collections with pre-assigned primary keys, always override `SaveChangesAsync` in your module's DbContext to ensure that child entities are explicitly saved as `EntityState.Added` instead of `Modified`.
