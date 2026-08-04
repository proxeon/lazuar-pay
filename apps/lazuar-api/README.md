
# Lazuar API — .NET 10 Modular Monolith

This directory houses the backend engine of the Lazuar Platform—a robust, strictly decoupled **Modular Monolith** built on **.NET 10.0**. The architecture is structured around strategic Domain-Driven Design (DDD), CQRS (Command Query Responsibility Segregation), and Clean Architecture principles, enforced through physical compilation boundaries (`.csproj`) and programmatic architectural guards.

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

### Local secrets (do not commit)

Base `appsettings.json` has empty placeholders for secrets. Development defaults live in
`appsettings.Development.json` (JWT + KMS only). For anything sensitive (Resend, AI, prod-like JWT):

```bash
cd src/Lazuar.Api
dotnet user-secrets set "Jwt:Secret" "your-local-jwt-secret-min-32-chars"
dotnet user-secrets set "Kms:MasterKey" "your-local-kms-master-key-min-32"
dotnet user-secrets set "Resend:ApiKey" "re_..."
dotnet user-secrets set "Ai:ProviderKeys:OPENROUTER" "sk-or-..."
```

Production: set `Jwt__Secret`, `Kms__MasterKey`, connection strings, and optional Resend via env
(see `deploy/prod/env.example`). Optional Azure Key Vault when `KeyVault:Uri` is configured.
Tenant payment/LHDN/email credentials are BYOK and encrypted at rest (`ISecretVault` / certificate vault).

---

## 2. Shared Foundations: BuildingBlocks vs. SharedKernel

To maintain strictly decoupled, independent module boundaries, cross-cutting code is separated into two distinct foundational layers:

```
                      ┌───────────────────────┐
                      │      BuildingBlocks   │ (Purely Technical, Domain-Agnostic)
                      └───────────┬───────────┘
                                  │
                                  ▼
                      ┌───────────────────────┐
                      │       SharedKernel    │ (Cross-Cutting Domain-Agnostic Types)
                      └───────────────────────┘
```

### BuildingBlocks
* **Purpose:** Contains purely technical, infrastructure-focused, and generic utility code.
* **Domain Awareness:** **Completely blind to the business domain.** It must never import any classes or refer to concepts belonging to your business vertical (such as "Tenant", "Billing", or "User").
* **Examples:** Cryptographic hashing, JWT generation, S3 storage wrappers, MediatR command/query base interfaces, and global exception handlers.

### SharedKernel
* **Purpose:** Holds business-neutral types, global value objects, system identifiers, and markers that are naturally shared across multiple modules.
* **Domain Awareness:** **Completely free of write-model business entities.** To ensure strict architectural decoupling, business entities (such as `OrganizationEntity` or `UserEntity`) must never live here. They reside strictly within their authoritative module domains.
* **Examples:** `SharedKernelMarker`, generic primitive IDs, common audit markers, and base value objects.
* **Verification:** Enforced programmatically in CI/CD via NUnit architectural unit tests.

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
  * `IIntegrationEvent`: Pure asynchronous messaging abstractions inheriting from MediatR's `INotification`.
  * `IEmailService` & `IMessagingService`: Port definitions for external communications.
  * `IExecutionContextAccessor`: Resolves claims (Tenant ID, User ID) from the active context.

### 3. `BuildingBlocks.Infrastructure`
* **Dependency:** References `BuildingBlocks.Application`.
* **Role:** Implements the concrete technical adapters required by the application layer. This is where external I/O, database systems, and security layers are introduced.
* **Core Abstractions:**
  * `PlatformDbContext`: Abstract database context base class. It intercepts operations to automatically apply multi-tenancy context filters and serializes raised domain events into the active context's private `OutboxMessages` schema table on every `SaveChangesAsync` call.
  * `InboxConsumerJob`: Base background service that processes local inbox messages using `IMediator` to dispatch them locally to use-case handlers.
  * `OutboxPublisherJob`: Base background worker that reads, processes, and dispatches outbox records asynchronously via the `IEventBus`.

---

## 4. The 4-Layer Module Architecture

Each operational module (such as `Tenant` and `Messaging`) is physically divided into four `.csproj` projects. This layered separation prevents domain, database, and transaction leaks:

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
* **What it contains:** Read-only data-transfer objects (DTOs) and public Integration Events.
* **Purpose:** It completely hides the internal domain details and database implementations, ensuring modules remain loosely coupled.

### 2. `Domain` (The Heart of the Module)
* **Why it's needed:** Houses the pure business logic, calculations, aggregates, and state transitions of the module.
* **What it contains:** Entities, aggregate roots, specific business rules, and domain events.
* **Purpose:** Ensures business rules are written in plain, highly testable C# without being influenced by web APIs, databases, or third-party integrations.

### 3. `Application` (The Use Case Orchestrator)
* **Why it's needed:** Implements the actual use cases of the module. It coordinates loading aggregates, executing domain rules, and persisting changes.
* **What it contains:** Command/Query Handlers, MediatR notification handlers (for processing inbox messages), and validator models.
* **Purpose:** Translates incoming client inputs or integration messages into domain operations, ensuring the domain remains clean and task-focused.

### 4. `Infrastructure` (The Physical Execution Layer)
* **Why it's needed:** Integrates the module with physical systems (databases, external messaging, files) and maps the outbox/inbox worker loops.
* **What it contains:** Module-specific DbContexts (mapped to private schemas like `tenant` and `messaging` with isolated EF migration tables), repository implementations, and background `OutboxPublisherJob` and `InboxConsumerJob` workers.
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

### Phase C: Implementation Baseline
- [ ] Create a `DependencyInjection.cs` marker class inside `Modules.Billing.Application` to allow MediatR assembly scanning.
- [ ] Create a `BillingDbContext.cs` inside `Modules.Billing.Infrastructure` inheriting from `PlatformDbContext` mapped to a private schema:
  ```csharp
  protected override void OnModelCreating(ModelBuilder modelBuilder) {
      base.OnModelCreating(modelBuilder);
      modelBuilder.HasDefaultSchema("billing");
      // Map domain entities and local Inbox/Outbox tables here
  }
  ```
- [ ] Implement module background workers:
  - [ ] Create `BillingOutboxPublisherJob` inheriting from `OutboxPublisherJob<BillingDbContext>`.
  - [ ] Create `BillingInboxConsumerJob` inheriting from `InboxConsumerJob<BillingDbContext>`.
- [ ] Create a `DependencyInjection.cs` class inside `Modules.Billing.Infrastructure` to register your module services, database contexts, and workers:
  ```csharp
  public static class DependencyInjection {
      public static IServiceCollection AddBillingModule(this IServiceCollection services, IConfiguration configuration) {
          var connectionString = configuration.GetConnectionString("Default");
          services.AddDbContext<BillingDbContext>(options =>
              options.UseNpgsql(connectionString, npgsqlOptions => {
                  npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "billing");
              }));
              
          // Register repositories and background services
          services.AddHostedService<BillingOutboxPublisherJob>();
          services.AddHostedService<BillingInboxConsumerJob>();
          return services;
      }
      
      public static IApplicationBuilder UseBillingSubscriptions(this IApplicationBuilder app) {
          var eventBus = app.ApplicationServices.GetRequiredService<IEventBus>();
          // Subscribe to external integration events here
          return app;
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
    builder.Services.AddBillingModule(builder.Configuration);
    ```
  - [ ] Register your cross-module event subscriptions:
    ```csharp
    app.UseBillingSubscriptions();
    ```
  - [ ] Map your API endpoints under the `/api/v1` group:
    ```csharp
    apiGroup.MapBillingEndpoints();
    ```
- [ ] **NOTE**: Because our `Dockerfile` uses Docker BuildKit's `--parents` feature, you **do not** need to manually update the Dockerfile when adding new `.csproj` files!
- [ ] Open `tests/Lazuar.ArchitectureTests/Lazuar.ArchitectureTests.csproj` and reference the new Billing domain projects.
- [ ] Update `ModuleBoundaryTests.cs` to include the `Modules.Billing` namespace within architectural boundaries.
- [ ] Run `pnpm build` to compile the solution and verify that the build succeeds without error.
