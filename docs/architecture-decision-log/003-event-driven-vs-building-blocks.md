
# 003: Defaulting to Event-Driven Communication over Building Blocks

## Context
Our Modular Monolith provides shared technical interfaces via the `BuildingBlocks.Application` layer (e.g., `IMessagingService`, `IEmailService`, `IR2StorageService`). However, when executing a business use case, developers face a choice: Should the module synchronously invoke a Building Block service to trigger a side effect, or should it publish an `IIntegrationEvent`?

In the legacy system, modules routinely invoked services like `IEmailService` or `IPaymentGateway` directly within their HTTP request pipelines. This led to slow response times, transaction rollbacks when third-party APIs failed, and severe logic leakage (e.g., the Community module holding email template strings).

## Decision
**By default, modules must communicate business consequences by publishing Integration Events rather than invoking Building Block services synchronously.**

Building Block services should ONLY be invoked synchronously when the current use case **strictly requires the immediate result** of the technical operation to complete its database transaction.

### 1. The Default: Event-Driven (Asynchronous)
Whenever a domain action occurs that requires a side-effect (like sending a notification, updating an external system, or logging analytics), the authoritative module must state *what happened* by publishing an `IIntegrationEvent`.

**Example: Customer Subscribes to a Plan**
* ❌ **WRONG:** The `Community` module's command handler calls `IMessagingService.SendEmailAsync()` to welcome the user.
* ✅ **RIGHT:** The `Community` module publishes a `CommunitySubscriptionActivatedIntegrationEvent`. The transaction commits instantly. The `Messaging` module subscribes to this event via the Inbox, resolves the correct email template for that specific tenant, and executes the external I/O asynchronously.

**Benefits:**
* **Transactional Outbox Guarantee:** The event is saved to the database in the exact same transaction as the entity. Zero risk of dual-write failures.
* **Separation of Concerns:** The `Community` module doesn't need to know how to render email templates.
* **Performance:** The HTTP request returns to the user instantly, without waiting for network I/O.

### 2. The Exception: Synchronous Building Blocks
You may inject and synchronously call a Building Block service ONLY if your Command/Query absolutely cannot proceed without the immediate technical result.

**Acceptable Synchronous Uses:**
* `IPasswordService`: Hashing a password before saving a new `UserEntity`.
* `IJwtService`: Generating an auth token to return in the HTTP response.
* `IR2StorageService`: Uploading a profile picture because the `ImageUrl` string must be saved to the database record immediately.
* `ISqlConnectionFactory`: Opening a Dapper connection for a fast, read-only query.

## Implementation Guidelines
1. **Never put templates in the origin module:** If you find yourself writing `$"Hi {name}, welcome to..."` inside a Command Handler, you are violating this rule. Publish an event and let the `Messaging` module handle the formatting.
2. **Never wrap external API calls in DB Transactions:** If you must use a Building Block that makes a network call (like `IR2StorageService`), execute the network call *before* opening the Database Transaction or *after* it commits, never during.
