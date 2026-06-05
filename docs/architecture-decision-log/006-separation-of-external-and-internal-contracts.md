# ADR 006: Separation of External API Contracts (TypeSpec) and Internal Module Contracts (MediatR)

**Status:** Accepted  
**Date:** January 2025  

## Context

Our application utilizes **TypeSpec** as the single source of truth for generating API models, generating both TypeScript interfaces for the frontend and C# DTOs via NSwag for the .NET backend. 

With the introduction of the Modular Monolith architecture, each module possesses its own `Contracts` project (e.g., `Modules/Community/Contracts`), which contains MediatR Commands, Queries, and Integration Events used for inter-module communication.

A question arose: **Should we adhere to DRY (Don't Repeat Yourself) by replacing our internal module `Contracts` with the auto-generated TypeSpec DTOs?**

## Decision

**No. We will keep external API contracts and internal module contracts strictly separate.** 

TypeSpec-generated DTOs will solely represent the HTTP Edge Boundary, while internal `Contracts` projects will exclusively handle internal business operations and CQRS messaging.

## Rationale

Merging these contracts would severely compromise our Clean Architecture for the following reasons:

### 1. Different Boundaries (External vs. Internal)
* **TypeSpec Types (External):** These represent the **Edge API Boundary**. They define exactly how the *Outside World* (React apps, third-party consumers, mobile apps) interacts with our system over HTTP.
* **Module Contracts (Internal):** These represent our **Internal Boundary**. They define how *Module A* talks to *Module B* (e.g., how the `Community` module asks the `CRM` module for a user's profile).

**The Risk:** If we use TypeSpec types for internal communication, we couple internal system logic to external UI needs. If the frontend team needs a field renamed or formatted differently for the UI, changing the TypeSpec definition will break the internal communication between our backend modules.

### 2. Behavioral Interfaces (MediatR)
Our internal `Contracts` folders contain MediatR Commands, Queries, and Integration Events.
* Internal commands and events must implement specific interfaces (e.g., `ICommand<T>`, `IIntegrationEvent`) for our CQRS pipeline, Event Bus, and Inbox/Outbox workers to function.
* TypeSpec generators (like NSwag) generate plain, behaviorless POCOs (Plain Old C# Objects). Trying to force NSwag to generate classes that implement our specific MediatR interfaces requires highly brittle, custom code-generation scripting.

### 3. Asymmetric Payloads
Often, what the API receives is not a 1:1 match with what the internal module needs to process a command.
* *Example:* A TypeSpec `CreateSubscriberRequestDto` receives a `plan_id` (string) from the frontend. However, the internal `CreateSubscriberCommand` requires the `OrganizationId` (resolved securely from the JWT) and parsed `Guid` types. 
* Separating them allows our `Endpoints.cs` to act as an **Anti-Corruption Layer**—taking the TypeSpec DTO, validating the HTTP context, injecting implicit security context data, and mapping it to a secure, strictly-typed internal Command.

### 4. Integration Events are Backend-Only
Our `Contracts` folders hold Integration Events (e.g., `CommunityCheckoutInitiatedIntegrationEvent`). These are backend-only pub/sub messages passed through our Outbox. TypeSpec has absolutely no business knowing about these, as they are never exposed over HTTP to the frontend.

## Consequences & The Correct Flow

To maintain a pristine, enterprise-grade architecture, all developers must adhere to the following data flow:

1. **Frontend** sends an HTTP request matching the **TypeSpec DTO**.
2. **API Layer** (`Endpoints.cs`) receives the **TypeSpec DTO**.
3. **API Layer** acts as the Anti-Corruption Layer: it validates context, applies tenant/user claims, and maps the TypeSpec DTO into an **Internal Module Contract** (MediatR Command).
4. **Application Layer** processes the internal Command.
5. **API Layer** takes the internal result, maps it back to a **TypeSpec DTO**, and sends it to the Frontend.

By keeping these structures separate, we allow our API to evolve independently from our core business logic.
