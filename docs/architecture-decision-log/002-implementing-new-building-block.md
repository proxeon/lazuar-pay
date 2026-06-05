

When we want to add new implementations to a building block, what do we need to take into account?


---


## When Implementing a New Building Block

The `BuildingBlocks` layer contains the technical foundation of your application. When introducing a new implementation here (e.g., a new storage adapter, a payment gateway abstraction, or an encryption service), you must consider domain-agnosticism, layering, and thread safety.

### A. Central Rule: Total Domain Agnosticism
* **Completely Blind to Business Domain:** Code in `BuildingBlocks` must be generic. It must **never** reference any business vertical concepts (e.g., "Tenant", "Booking", "Organization", "CRM").
* **No Domain References:** A building block must never import namespaces belonging to your modules or reference database entities like `ClientProfileEntity`. 
* **Generic Abstractions:** If an infrastructure utility requires data from a domain entity, define a generic contract or a simple DTO inside `BuildingBlocks.Application` and let the calling module map its entities into that contract.

### B. Structural Layering
Ensure your new implementation is placed in the correct `BuildingBlocks` assembly:
* **`BuildingBlocks.Domain`:** Pure C# containing only logical abstractions (e.g., `IBusinessRule`, `Entity`, `ValueObject`). It has **no** external library dependencies.
* **`BuildingBlocks.Application`:** Contains ports, system-wide interfaces, and CQRS pipeline contracts (e.g., `IEmailService`, `IEventBus`).
* **`BuildingBlocks.Infrastructure`:** Contains concrete adapters, I/O systems, and third-party integrations (e.g., `R2StorageService` wrapping the AWS S3 SDK, `PasswordService` wrapping BCrypt).

### C. Thread Safety and Singleton Lifecycles
* Many services in `BuildingBlocks` (such as `InMemoryEventBus`, `DatabaseJobTrigger`, `TypeResolver`) are registered as **Singletons**.
* **No Scoped Captures:** A Singleton building block must never directly capture a scoped service (like an EF Core `DbContext`). If a background job needs database access, it must inject `IServiceScopeFactory` and create a short-lived transient scope inside its execution loop.
* **Concurrency Handling:** Ensure all shared states, dictionaries, or channels inside Singleton building blocks are thread-safe (e.g., using `ConcurrentDictionary` or bounded `System.Threading.Channels`).

### D. Fail-Safe Operations and Logging
* Infrastructure adapters deal with external networks, third-party APIs, and disk I/O, which are inherently unreliable. 
* Implement robust error boundaries (such as exponential backoff retries in `LlmClientService` or try-catch blocks in `R2StorageService`) to ensure that infrastructure-level failures are logged properly and do not unexpectedly crash the main application process.

