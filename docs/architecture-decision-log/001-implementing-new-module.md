

What should we take into account when implementing a new module?


---


## When Implementing a New Module

When adding a new module (e.g., `Billing`, `CRM`, or `Analytics`), you must take into account compile-time boundaries, database isolation, asynchronous communication, and automated test guards.

### A. Strict Compilation Boundaries (`.csproj`)
You must physically separate the module into **four distinct `.csproj` projects** rather than just creating folders. You must adhere strictly to this reference structure:
1. **`Contracts`** references `BuildingBlocks.Application`. (Only other modules can reference this).
2. **`Domain`** references `BuildingBlocks.Domain` and `SharedKernel`. It has **no** references to any other project or database driver.
3. **`Application`** references `Domain`, `Contracts`, and `BuildingBlocks.Application`.
4. **`Infrastructure`** references `Application` and `BuildingBlocks.Infrastructure`.

### B. Database Schema & Migration Isolation
* **Private Database Schema:** Your module’s tables must live in its own PostgreSQL schema (e.g., `billing.PaymentRecords`, `billing.OutboxMessages`).
* **Isolated Migration History:** Configure the module's `DbContext` inside its `OnModelCreating` to use a localized migrations history table inside its private schema to prevent cross-module migration locks:
  ```csharp
  npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "billing");
  ```
* **No Database Joins Across Schemas:** You must never write a raw Dapper SQL query that joins a table in your module’s schema with a table in another module’s schema.

### C. Asynchronous and Decoupled Communication
* **No Direct In-Memory Calls:** A command handler in Module A must never directly instantiate or call a repository/service inside Module B's internal layers.
* **Events and Inbox/Outbox:** Cross-module operations must be executed asynchronously via the inbox/outbox queues. 
  * The triggering module writes state and publishes an `IIntegrationEvent` (defined in `Contracts`).
  * The receiving module subscribes to the event, writes an `InboxMessage` to its own schema, and processes it asynchronously via its own background worker.

### D. Keyed SQL Connection Pool Isolation
* If your module uses Dapper for fast read-model queries, you must register a keyed `ISqlConnectionFactory` (using `AddKeyedScoped`) bound specifically to your module's connection string. This isolates your module's read connection pool from other modules.

### E. Defensive Host Integration
* **Host Reference:** The central executable `Lazuar.Api.csproj` must **only** reference the module's `Infrastructure` project. It must never reference `Domain` or `Application` directly.
* **Clean DI & Endpoint Mapping:** Register the module services (`Add[Module]Module`), cross-module event subscriptions (`Use[Module]Subscriptions`), and minimal API routes (`Map[Module]Endpoints`) cleanly in `Program.cs`.

### F. Architectural Unit Tests
* Always register the new module's namespace within `ModuleBoundaryTests.cs` to ensure that standard layer dependency rules are enforced automatically during CI/CD pipelines.


