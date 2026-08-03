# 001 — Cross-Module Communication Guidelines (Sync vs. Async)

This document establishes the strict architectural rules governing how different operational modules (such as `Tenant`, `Community`, `Payments`, `CRM`, and `Messaging`) communicate with each other within the Lazuar platform modular monolith.

---

## 1. The Golden Rule of Module Isolation
Modules must remain strictly decoupled. This isolation ensures the codebase is stable, highly testable, and ready to be separated into physical microservices in the future if required.

* **No Direct DB Joins:** A database query in the `Community` schema must never execute a join to a table in the `CRM` or `Payments` schemas.
* **No Direct Write-Model References:** A domain entity or aggregate root in one module must never reference a domain entity or aggregate root in another module.
* **No Cross-Schema Foreign Keys:** Foreign key constraints must never be configured between database tables belonging to different schemas.

---

## 2. Synchronous Cross-Module Queries (The Exception)
When a module requires low-latency, read-only data from another module to fulfill a current use case, it can execute a synchronous query.

```
┌───────────────────────────┐                ┌───────────────────────────┐
│     Community Module      │                │        CRM Module         │
│  (Command/Query Handler)  │                │   (Infrastructure/API)    │
└─────────────┬─────────────┘                └─────────────▲─────────────┘
              │                                            │
              │ 1. Send IQuery via Mediator                │ 3. Query DB
              │    (e.g., GetClientProfileAsync)           │    (Dapper)
              ▼                                            │
┌──────────────────────────────────────────────────────────┴─────────────┐
│                       ICrmQueryService (Contract)                      │
└────────────────────────────────────────────────────────────────────────┘
```

### Rules for Synchronous Queries:
1. **Read-Only:** Synchronous cross-module calls must only read data. They must never mutate state in the target module.
2. **Contract Dependency:** The calling module must only reference the target module's `.Contracts` assembly. It is strictly forbidden to reference the target's `.Application` or `.Infrastructure` assemblies.
3. **Execution Pathway:** The calling handler dispatches the Query via `IMediator` or invokes the targeted public interface (e.g., `ICrmQueryService`).

### Code Example (From `InitiateSubscriptionCheckoutCommandHandler.cs`):
Here, the `Community` module queries client data from the `CRM` module and requests a checkout session from the `Payments` module synchronously:
```csharp
// 1. Fetch Customer Data via CRM Read Model Contract (Cross-module query without DB Join)
var customerProfile = await _crmQueryService.GetClientProfileAsync(subscription.ClientProfileId);
var customerEmail = customerProfile?.Email ?? "";

// 2. Cross-Module Query to get the Checkout URL synchronously from Payments
var query = new GenerateCheckoutSessionQuery(
    request.OrganizationId,
    plan.Price,
    "MYR", 
    plan.Name, 
    customerEmail, 
    request.SuccessUrl,
    request.CancelUrl,
    metadata);

var checkoutUrl = await _mediator.Send(query, ct);
```

---

## 3. Asynchronous Integration Events (The Default)
When a state change in one module must trigger a state mutation, processing, or notification in another module, the communication **must** be asynchronous.

Lazuar uses a **hybrid outbox model**: every publisher persists events to its module outbox; dispatch is primarily inline via the in-process bus; inbox is an **opt-in** durability pattern for handlers that need store-and-ack semantics.

```
┌───────────────────────────┐                ┌───────────────────────────┐
│      Payments Module      │                │     Commerce Module       │
└─────────────┬─────────────┘                └─────────────▲─────────────┘
              │                                            │
              │ 1. Save event to DB Outbox                 │
              │    (same transaction as domain write)      │
              ▼                                            │
┌───────────────────────────┐                              │
│       Outbox Table        │                              │
└─────────────┬─────────────┘                              │
              │                                            │
              │ 2. OutboxPublisherJob drains outbox        │
              ▼                                            │
┌───────────────────────────┐                              │
│     InMemoryEventBus      │ 3. Default: invoke           │
│       (Publisher)         ├──────────────────────────────┤
└───────────────────────────┘    IIntegrationEventHandler  │
                                 inline (must be           │
                                 idempotent)               │
                                                           │
                        Optional (Messaging pattern):      │
                        write InboxMessages → ack →        │
                        InboxConsumerJob processes later   │
```

### Rules for Asynchronous Integration Events:
1. **Outbox-Backed (required):** The publishing module must write the integration event to its local `OutboxMessages` table within the active transaction boundary (`PublishAsync` then a single `SaveChanges` that covers domain + outbox).
2. **OutboxPublisherJob (required):** Every module that registers `OutboxEventBus<TDbContext>` **must** host an `*OutboxPublisherJob` so rows leave `OutboxMessages`. Without it, events are stuck forever.
3. **Default dispatch — InMemoryEventBus → handlers inline:** After the outbox job deserializes a message, `InMemoryEventBus` resolves and runs each subscribed `IIntegrationEventHandler<T>` **in process**. Handlers **must be idempotent** (retries and multi-instance drains will re-deliver).
4. **Inbox is opt-in:** Writing to `InboxMessages` and processing via `InboxConsumerJob` is the **Messaging** (and similar) pattern for store-and-ack / deferred work. It is **not** required for every module or every handler. Modules may keep inbox tables and register an empty inbox consumer; that is OK.
5. **Registering an empty inbox consumer is OK:** Hosting `*InboxConsumerJob` when nothing writes inbox rows is harmless and keeps the module symmetric with other Outbox/Inbox-equipped modules.

### Code Example (From `GatewayPaymentCompletedIntegrationEventHandler.cs`):
```csharp
public class GatewayPaymentCompletedIntegrationEventHandler 
    : IIntegrationEventHandler<GatewayPaymentCompletedIntegrationEvent>
{
    private readonly IMediator _mediator;

    public GatewayPaymentCompletedIntegrationEventHandler(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task HandleAsync(GatewayPaymentCompletedIntegrationEvent @event)
    {
        // Handlers run inline via InMemoryEventBus; keep them idempotent.
        if (!@event.Metadata.TryGetValue("type", out var type) || type != "commerce_subscription")
        {
            return; 
        }

        if (!@event.Metadata.TryGetValue("subscription_id", out var subIdStr) || 
            !Guid.TryParse(subIdStr, out var subscriptionId))
        {
            throw new InvalidOperationException("Missing valid subscription_id in metadata.");
        }

        // Dispatch local command to alter write-model state within commerce transaction
        var command = new RecordSubscriptionPaymentCommand(
            OrganizationId: @event.OrganizationId,
            SubscriptionId: subscriptionId,
            Amount: @event.AmountPaid,
            Currency: @event.Currency,
            PaymentMethod: "ONLINE_GATEWAY",
            ExternalReference: @event.GatewayTransactionId,
            RecordedBy: "SYSTEM"
        );

        await _mediator.Send(command);
    }
}
```

---

## 4. Handling Cross-Schema References
To maintain clean database schemas, you must never define physical foreign keys linking tables across schemas.

* **Reference Strategy:** Store target references as raw primitive `Guid` identifiers. For example, `CommunitySubscription` references `ClientProfileEntity` strictly as a raw `Guid` property:
  ```csharp
  public class CommunitySubscription : Entity, IAggregateRoot, IMustHaveTenant
  {
      public Guid Id { get; private set; }
      public Guid ClientProfileId { get; private set; } // Managed as raw primitive Guid
      public Guid PlanId { get; private set; } // Foreign key allowed because it is local
  }
  ```
* **Data Resolution:** The infrastructure layer resolves the primitive `Guid` to the necessary object using synchronous contract queries or replication models when compiling views.
