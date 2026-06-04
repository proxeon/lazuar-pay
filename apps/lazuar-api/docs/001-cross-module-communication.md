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

```
┌───────────────────────────┐                ┌───────────────────────────┐
│      Payments Module      │                │     Community Module      │
└─────────────┬─────────────┘                └─────────────▲─────────────┘
              │                                            │
              │ 1. Save event to DB Outbox                 │ 4. Consume from Inbox
              ▼                                            │
┌───────────────────────────┐                              │
│       Outbox Table        │                              │
└─────────────┬─────────────┘                              │
              │                                            │
              │ 2. Read by Outbox Job                      │
              ▼                                            │
┌───────────────────────────┐                              │
│       InMemoryBus         ├──────────────────────────────┘
│       (Publisher)         │ 3. Dispatch to local Inbox
└───────────────────────────┘
```

### Rules for Asynchronous Integration Events:
1. **Outbox-Backed:** The publishing module must write the integration event to its local `OutboxMessages` table within the active transaction boundary.
2. **Inbox-Backed:** The receiving module must capture the incoming integration event, write it directly to its local `InboxMessages` table, and return an acknowledgment immediately.
3. **Asynchronous Fulfillment:** Background workers (`OutboxPublisherJob` and `InboxConsumerJob`) process the message queues out-of-process, guaranteeing eventual consistency and mitigating dual-write failures.

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
        if (!@event.Metadata.TryGetValue("type", out var type) || type != "community_subscription")
        {
            return; 
        }

        if (!@event.Metadata.TryGetValue("subscription_id", out var subIdStr) || 
            !Guid.TryParse(subIdStr, out var subscriptionId))
        {
            throw new InvalidOperationException("Missing valid subscription_id in metadata.");
        }

        // Dispatch local command to alter write-model state within community transaction
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
