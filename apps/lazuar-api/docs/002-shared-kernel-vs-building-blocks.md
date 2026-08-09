# 002 — The "SharedKernel" vs. "BuildingBlocks" Boundary

To maintain decoupled boundaries, all non-business shared classes are partitioned into two distinct infrastructure libraries: `BuildingBlocks` and `SharedKernel`. This document defines their boundaries and prevents architectural degradation.

> **Ownership map (stay / move / defer):** see [`009-building-blocks-ownership.md`](./009-building-blocks-ownership.md) — Phase 15 source of truth for product vs technical placement.  
> **Analysis:** [`plans/004-maintenance/06-building-blocks-shared-kernel.md`](../../../plans/004-maintenance/06-building-blocks-shared-kernel.md)

---

## 1. Architectural Blueprint

```
                      ┌───────────────────────┐
                      │      BuildingBlocks   │ (Technical spine; platform tenancy OK)
                      └───────────┬───────────┘
                                  │
                                  ▼
                      ┌───────────────────────┐
                      │       SharedKernel    │ (Marker today; shared VOs only when real)
                      └───────────────────────┘
```

Module Domain projects typically reference **both** `BuildingBlocks.Domain` and `SharedKernel`. SharedKernel itself references only `BuildingBlocks.Domain`. BuildingBlocks must **never** reference `Modules.*` (enforced by architecture tests).

---

## 2. BuildingBlocks Layer

### Purpose

`BuildingBlocks` houses the **technical spine** of the modular monolith: DDD structural patterns, CQRS/integration messaging contracts, outbox/inbox workers, platform persistence base, generic security, and thin multi-module adapters (e.g. object storage).

### Core rules (refined)

| Allow | Forbid |
|-------|--------|
| Platform multi-tenancy markers (`IMustHaveTenant`, OrganizationId stamp/filter on `PlatformDbContext`) | Module **business aggregates** or write models (User, Subscription, TaxDocument, …) |
| CQRS, IIntegrationEvent, outbox/inbox, job trigger | Private-schema **product SQL** that encodes one module’s domain vocabulary (e.g. LHDN status enums) — prefer module metric contributors |
| Generic crypto, password, token, thin storage port | Brand / product HTML, subscription-shaped magic-link APIs long-term |
| Technical dead-letter / outbox lag observability | Growing kitchen-sink of product counters without a plugin path |

**Historical claim “completely domain-blind / never say Tenant” is too absolute.** Multi-tenancy is platform technical policy here. The test is: *does this type encode a product module’s private concept?* If yes, it belongs in that module (or as a registered plugin), not in BB forever.

### Structural projects

#### 1. `BuildingBlocks.Domain`

* **Dependencies:** MediatR only (because `IDomainEvent : INotification` — accepted purity tradeoff).
* **Contents:** Structural patterns:
  * `Entity`, `ValueObject`, `IAggregateRoot`
  * `IBusinessRule` / validation exception
  * `IDomainEvent`
  * `IMustHaveTenant` — platform tenancy stamp

#### 2. `BuildingBlocks.Application`

* **Dependencies:** `BuildingBlocks.Domain` (+ MediatR; Markdig/OpenAI currently present — **debt**, see 009).
* **Contents (intended core):**
  * `ICommand` & `IQuery` (CQRS)
  * `IIntegrationEvent`, event bus ports
  * `IExecutionContextAccessor`, `ISqlConnectionFactory`
  * Security ports: password, secret vault, token generator
  * `PaginatedResponse` / paging helpers
* **Present but product-shaped (tracked in 009 for move):** `IEmailService`, `IMessagingService`, `EmailTemplateBuilder`, `MarkdownParser`, product metric names.
* **Moved out of BB:** LLM factory (R31), `AgentToolAttribute` / `IAgentPromptProvider` (R32 → Ops.Contracts), magic-link port (R33 → Commerce.Contracts).

#### 3. `BuildingBlocks.Infrastructure`

* **Dependencies:** `BuildingBlocks.Application`.
* **Contents (intended core):**
  * `PlatformDbContext` — multi-tenant filter, domain-event dispatch, job trigger (**single owner**; host must not ship a parallel base)
  * Outbox / inbox jobs and message entities
  * Password / JWT / AES vault / token generators
* **Present but fat (tracked in 009):** Resend email, console messaging, full LLM client stack, hardcoded multi-schema metrics SQL, global `BackgroundWorkerOptions`, document payload helpers.

Allowed mental folders under Infrastructure (even if not separate csproj yet): Persistence, Messaging, Security, Storage, Observability, Hosting; Email/Llm only until owned by modules.

---

## 3. SharedKernel Layer

### Purpose

`SharedKernel` is a **future pressure valve** and architecture-test assembly anchor for cross-module, non-entity primitives.

### Decision (Phase 15)

**Keep as intentional empty marker.** Do not force-fill. Populate only when a real shared value object / ID type is needed by multiple modules.

### Core rule

**Strictly free of write-model business entities.** `UserEntity`, `OrganizationEntity`, etc. stay inside owning module Domain projects.

### What is allowed

* Marker type (`SharedKernelMarker`) for assembly scanning / NetArchTest.
* Global ID value objects and pure domain-agnostic value types **when they exist**.

### What is not allowed

* Dumping “shared” aggregates to avoid integration events.
* Types that force module Domain → SharedKernel → other module concepts (cycle pressure).

See also: `SharedKernel/SharedKernelMarker.cs` and optional `SharedKernel/README.md`.

---

## 4. Why this separation matters

Maintaining these boundaries prevents circular dependency loops. If an aggregate in `Commerce` referenced a concrete aggregate in `One` via a shared project, extracting `Commerce` later would be hard.

By keeping BuildingBlocks technical (with platform tenancy as the exception) and SharedKernel free of entities (and empty until needed), modules stay isolatable.

For the full stay/move matrix (LLM, email, metrics, R2, worker options), use **[009 — BuildingBlocks ownership map](./009-building-blocks-ownership.md)**.
