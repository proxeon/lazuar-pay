# 002 — The "SharedKernel" vs. "BuildingBlocks" Boundary

To maintain decoupled boundaries, all non-business shared classes are partitioned into two distinct infrastructure libraries: `BuildingBlocks` and `SharedKernel`. This document defines their boundaries and prevents architectural degradation.

---

## 1. Architectural Blueprint

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

---

## 2. BuildingBlocks Layer

### Purpose:
`BuildingBlocks` houses purely technical infrastructure components, interfaces, and base patterns. 

### Core Rule:
**The BuildingBlocks project is completely domain-blind.** It must never import, reference, or mention concepts belonging to the business domain (such as "Tenant", "Client", "Organization", "Subscription", or "Payer"). If a file in this folder contains a business keyword, it is in the wrong project.

### Structural Projects:

#### 1. `BuildingBlocks.Domain`
* **Dependencies:** None.
* **Contents:** Plain C# classes defining structural patterns:
  * `Entity`: Core aggregate base class tracking raised `IDomainEvent` collections.
  * `ValueObject`: Structurally evaluated domain values.
  * `IBusinessRule`: Invariant business checks (`IsBroken()`).

#### 2. `BuildingBlocks.Application`
* **Dependencies:** References `BuildingBlocks.Domain`.
* **Contents:** Messaging and pipeline contracts:
  * `ICommand` & `IQuery`: CQRS interfaces wrapping MediatR.
  * `IIntegrationEvent`: Base async integration contracts.
  * `IEmailService` & `IMessagingService`: Outbound port definitions.

#### 3. `BuildingBlocks.Infrastructure`
* **Dependencies:** References `BuildingBlocks.Application`.
* **Contents:** Concrete technical adapters:
  * `PlatformDbContext`: Automatic multi-tenancy filter assignment and recursive pre-save event dispatching.
  * `InboxConsumerJob` & `OutboxPublisherJob`: Postgres `SKIP LOCKED` worker implementations.
  * `PasswordService` & `JwtService`: Security implementations.

---

## 3. SharedKernel Layer

### Purpose:
`SharedKernel` houses common primitives, markers, or global shared structures that must be accessible across multiple modules.

### Core Rule:
**The SharedKernel project is strictly free of write-model business entities.** 
To prevent dependency loops, business write models (such as `UserEntity`, `OrganizationEntity`, or `BranchEntity`) must never reside in `SharedKernel`. They belong strictly within the internal, private `Domain` projects of their owning modules.

### What is allowed in SharedKernel:
* Marker interfaces (e.g., `SharedKernelMarker`).
* Global ID value objects.
* Pure domain-agnostic value types used globally.

---

## 4. Why this Separation Matters
Maintaining these boundaries prevents "circular dependency loops." If an aggregate in the `Commerce` module were to reference a concrete aggregate class in `One` via a shared project, extracting `Commerce` into an independent microservice later would be difficult. 

By keeping `BuildingBlocks` purely technical and `SharedKernel` free of domain entities, modules remain isolated, allowing them to be developed, tested, and scaled independently.
