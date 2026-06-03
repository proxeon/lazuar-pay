
# Lazuar API — .NET 10 Modular Monolith

This directory houses the backend engine of the Lazuar Platform—a robust, highly decoupled **Modular Monolith** built on **.NET 10.0**. The architecture is structured around DDD (Domain-Driven Design), CQRS (Command Query Responsibility Segregation), and Clean Architecture principles, enforced through physical compilation boundaries (`.csproj`).

---

## 1. Essential Commands

All tasks can be executed globally from the monorepo root via `pnpm` or run locally within this folder using the `.NET CLI`.

### Global Workspace Commands (From Repository Root)
```bash
# Compile both frontends and backend in parallel (caches on success)
pnpm build

# Run local development servers concurrently (includes .NET hot-reload watcher)
pnpm dev

# Execute all frontend tests and .NET architecture tests
pnpm test

# Verify code style and formatting across the monorepo
pnpm lint

# Format C# and TypeScript files automatically
pnpm format
```

### Local .NET CLI Commands (From `apps/lazuar-api`)
```bash
# Restore dependencies across the .slnx solution
dotnet restore Lazuar.slnx

# Compile the .NET solution
dotnet build

# Execute NUnit architecture tests
dotnet test tests/Lazuar.ArchitectureTests/Lazuar.ArchitectureTests.csproj

# Format C# files using the rules defined in .editorconfig
dotnet format
```

---

## 2. Shared Foundations: BuildingBlocks vs. SharedKernel

To maintain clean boundaries, we separate cross-cutting code into two distinct foundational layers:

```
                      ┌───────────────────────┐
                      │      BuildingBlocks   │ (Purely Technical, Domain-Agnostic)
                      └───────────┬───────────┘
                                  │
                                  ▼
                      ┌───────────────────────┐
                      │       SharedKernel    │ (Ubiquitous Business Concepts)
                      └───────────────────────┘
```

### BuildingBlocks
* **Purpose:** Contains purely technical, infrastructure-focused, and generic utility code.
* **Domain Awareness:** **Completely blind to the business domain.** It must never import any classes or refer to concepts belonging to your business vertical (such as "Tenant", "Booking", or "Subscriber").
* **Examples:** Cryptographic hashing, JWT generation, S3 storage wrappers, MediatR command/query base interfaces, and global exception handlers.

### SharedKernel
* **Purpose:** Holds ubiquitous business concepts, database entities, and identifiers that are naturally shared and referenced across multiple operational modules.
* **Domain Awareness:** **Strictly business-domain aware.** It contains the foundational data schemas that glue the platform together.
* **Examples:** `OrganizationEntity`, `BranchEntity`, `UserEntity`, and `ClientProfileEntity`.

---

## 3. Deep Dive: BuildingBlocks Layers

The `BuildingBlocks` folder is divided into three physical projects to enforce Clean Architecture boundaries at the core level:

### 1. `BuildingBlocks.Domain`
* **Dependency:** None (Pure C#).
* **Role:** Defines the mathematical and structural invariants of Domain-Driven Design. It remains completely insulated from database engines, HTTP contexts, or third-party libraries.
* **Core Abstractions:**
  * `Entity`: Tracks domain event registration and enforces invariant business rules.
  * `ValueObject`: Provides value-based structural equality (`IEquatable`).
  * `IDomainEvent`: Marks internal messages mapped to transaction boundaries.
  * `IBusinessRule`: Defines validation check invariants.

### 2. `BuildingBlocks.Application`
* **Dependency:** References `BuildingBlocks.Domain`.
* **Role:** Establishes application layer contracts and the CQRS command/query pipeline abstractions.
* **Core Abstractions:**
  * `ICommand` & `IQuery`: Pipeline wrappers for MediatR.
  * `IEmailService` & `IMessagingService`: Port definitions for external communications.
  * `IExecutionContextAccessor`: Resolves claims (Tenant ID, User ID) from the active context.

### 3. `BuildingBlocks.Infrastructure`
* **Dependency:** References `BuildingBlocks.Application`.
* **Role:** Implements the concrete technical adapters required by the application layer. This is where external I/O, database systems, and security layers are introduced.
* **Core Abstractions:**
  * `PlatformDbContext`: Implements database-level multi-tenancy filters and automated Auditing.
  * `JwtService` & `PasswordService`: Cryptographic security engines.
  * `R2StorageService`: Concrete Cloudflare S3-compatible adapter.

---

## 4. The 4-Layer Module Architecture

Each operational module (such as `Tenant` and `Messaging`) is physically divided into four `.csproj` projects. This layered separation prevents domain and database leaks:

```
   ┌────────────────────────────────────────────────────────┐
   │                  Modules.[Module].Contracts            │ <── (Only project other modules can reference)
   └──────────────────────────┬─────────────────────────────┘
                              │
                              ▼
   ┌────────────────────────────────────────────────────────┐
   │                  Modules.[Module].Domain               │
   └──────────────────────────┬─────────────────────────────┘
                              │
                              ▼
   ┌────────────────────────────────────────────────────────┐
   │                  Modules.[Module].Application          │
   └──────────────────────────┬─────────────────────────────┘
                              │
                              ▼
   ┌────────────────────────────────────────────────────────┐
   │                  Modules.[Module].Infrastructure        │
   └────────────────────────────────────────────────────────┘
```

### 1. `Contracts` (The Module's Public Interface)
* **Why it's needed:** This acts as the module's public contract boundary. Other modules are only allowed to reference this project.
* **What it contains:** Public query interfaces (`ITenantQueryService`), read DTOs, and integration events.
* **Purpose:** It completely hides the internal domain details and database implementations, ensuring modules remain loosely coupled.

### 2. `Domain` (The Heart of the Module)
* **Why it's needed:** Houses the pure business logic, calculations, aggregates, and state transitions of the module.
* **What it contains:** Entities, aggregate roots, specific business rules, and domain events.
* **Purpose:** Ensures business rules are written in plain, highly testable C# without being influenced by web APIs, databases, or third-party integrations.

### 3. `Application` (The Use Case Orchestrator)
* **Why it's needed:** Implements the actual use cases of the module. It coordinates loading aggregates, executing domain rules, and persisting changes.
* **What it contains:** Command/Query Handlers, MediatR behaviors, and background jobs.
* **Purpose:** Translates incoming client inputs into domain operations, ensuring the domain remains clean and task-focused.

### 4. `Infrastructure` (The Physical Execution Layer)
* **Why it's needed:** Integrates the module with physical systems (databases, external messaging, files).
* **What it contains:** Module-specific DbContexts, EF Core entity mappings, database migrations, and external API connectors (such as WhatsApp/Telegram integrations).
* **Purpose:** Isolates data access and external libraries, allowing the rest of the module to remain highly stable.

---

## 5. Checklist: How to Add a New Module

When adding a new module (for example, `Billing`), follow this structured, compile-safe checklist:

### Phase A: Project Creation & References
- [ ] Create a new directory under `Modules/` named `Billing`.
- [ ] Inside `Modules/Billing/`, create the four standard projects:
  - [ ] `Contracts/Modules.Billing.Contracts.csproj`
  - [ ] `Domain/Modules.Billing.Domain.csproj`
  - [ ] `Application/Modules.Billing.Application.csproj`
  - [ ] `Infrastructure/Modules.Billing.Infrastructure.csproj`
- [ ] Configure project dependencies strictly:
  - [ ] `Contracts` references `BuildingBlocks.Application`.
  - [ ] `Domain` references `BuildingBlocks.Domain` and `SharedKernel`.
  - [ ] `Application` references `Domain`, `Contracts`, and `BuildingBlocks.Application`.
  - [ ] `Infrastructure` references `Application` and `BuildingBlocks.Infrastructure`.

### Phase B: Solution Registration
- [ ] Open `/apps/lazuar-api/Lazuar.slnx` and register your new projects inside a dedicated solution folder:
  ```xml
  <Folder Name="/Modules/Billing/">
    <Project Path="Modules/Billing/Contracts/Modules.Billing.Contracts.csproj" />
    <Project Path="Modules/Billing/Domain/Modules.Billing.Domain.csproj" />
    <Project Path="Modules/Billing/Application/Modules.Billing.Application.csproj" />
    <Project Path="Modules/Billing/Infrastructure/Modules.Billing.Infrastructure.csproj" />
  </Folder>
  ```

### Phase C: Implementation baseline
- [ ] Create a `DependencyInjection.cs` marker class inside `Modules.Billing.Application` to allow MediatR assembly scanning.
- [ ] Create a `DependencyInjection.cs` class inside `Modules.Billing.Infrastructure` to register your module services:
  ```csharp
  public static class DependencyInjection {
      public static IServiceCollection AddBillingModule(this IServiceCollection services) {
          // Register repositories and services here
          return services;
      }
  }
  ```
- [ ] Create an `Endpoints.cs` class inside `Modules.Billing.Infrastructure` to map your Minimal API routes:
  ```csharp
  public static class Endpoints {
      public static IEndpointRouteBuilder MapBillingEndpoints(this IEndpointRouteBuilder endpoints) {
          var group = endpoints.MapGroup("/billing");
          // Map routes here
          return endpoints;
      }
  }
  ```

### Phase D: Host Integration
- [ ] Open `/apps/lazuar-api/src/Lazuar.Api/Lazuar.Api.csproj` and reference your new module's infrastructure project:
  ```xml
  <ProjectReference Include="..\..\Modules\Billing\Infrastructure\Modules.Billing.Infrastructure.csproj" />
  ```
- [ ] Open `/apps/lazuar-api/src/Lazuar.Api/Program.cs`:
  - [ ] Register your MediatR assembly:
    ```csharp
    cfg.RegisterServicesFromAssembly(typeof(Modules.Billing.Application.DependencyInjection).Assembly);
    ```
  - [ ] Register your module services:
    ```csharp
    builder.Services.AddBillingModule();
    ```
  - [ ] Map your API endpoints under the `/api/v1` group:
    ```csharp
    apiGroup.MapBillingEndpoints();
    ```
- [ ] Run `pnpm build` to compile the solution and verify that the build succeeds without error.
