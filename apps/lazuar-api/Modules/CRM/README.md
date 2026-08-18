# CRM Module (The "PII Registry")

## 1. Overview
The `CRM` (Customer Relationship Management) module acts as the centralized, tenant-scoped registry for all customer Personally Identifiable Information (PII). It bridges the gap between the platform's global identity system (the `One` module) and the localized, tenant-specific customer records required by fulfillment modules such as **Commerce**.

## 2. Core Responsibilities
* **Customer Directory:** Maintaining a strict, isolated directory of client profiles (Name, Email, Phone) for each tenant.
* **Global Identity Sync:** Listening to the `One` module to automatically update a tenant's local customer record when a user updates their global master profile.
* **GDPR/PDPA Compliance:** Executing hard anonymization of customer PII and broadcasting the deletion event so downstream modules can revoke access.
* **Read-Model Provision:** Exposing high-performance, cross-module query contracts (`ICrmQueryService`) so other modules can resolve customer details without violating database schema boundaries.

## 3. Architectural Boundaries (What this module is NOT)
* **Not an Access Control System:** It does not know if a user is subscribed to a plan, has an active billing cycle, or possesses a login token. 
* **Not a Messaging Engine:** It does not send emails or SMS messages. It merely holds the contact data that other modules use to dispatch messages.
* **No Business Context:** It does not store "Leads", "Deals", or "Support Tickets". It is strictly a primitive contact registry.

## 3.1 Layer shape (no Application project)

CRM is a **documented 3-layer exception**: `Contracts` + `Domain` + `Infrastructure` only — **no `Application` project**. Command handlers, query service, and workers live in Infrastructure. Host MediatR registers only the Infrastructure assembly for CRM (`ModulesWithoutApplication` in architecture tests).

Do not invent an Application layer without an intentional epic (ports extraction + architecture-test update). The module is internal-only (no HTTP `Endpoints.cs`); other modules use `ICrmQueryService` / commands via Contracts. Merchants trigger PDPA wipe via Commerce `POST /admin/commerce/subscribers/{id}/anonymize` (OrgAdmin), which sends `AnonymizeClientProfileCommand`.

## 4. Key Domain Aggregates & Entities
* **`ClientProfileEntity`**: The core aggregate representing a customer within a specific tenant. Contains `FullName`, `Email`, `Phone`, `ConsentedToMarketing`, and an optional `GlobalUserId` link to the `One` module.
  * *Anonymize Behavior:* When triggered, it overwrites PII with dummy data (e.g., `deleted_{Id}@localhost`) and severs the `GlobalUserId` link.

## 5. Integration Events
### Consumed
* **`GlobalUserProfileUpdatedIntegrationEvent`** (from `One`): When a user changes their name or email in their global launchpad, this handler finds all linked `ClientProfileEntity` records across all tenants and updates them to maintain data consistency.

### Published
* **`ClientProfileAnonymizedIntegrationEvent`**: Fired when a GDPR deletion request is processed. Downstream modules listen for the affected `ClientProfileId`: **Commerce** cancels subscriptions, **Communications** suppresses mail, **Messaging** scrubs delivery-log recipients. Official receipt PDFs and MyInvois submissions keep the buyer identity that was filed — they are legal records, not rewritten.

## 6. Cross-Module Contracts (Synchronous Queries)
To prevent cross-schema database joins, the CRM module exposes a read-only contract for other modules to consume:
* **`ICrmQueryService`**: 
  * `GetClientProfileAsync(Guid profileId)`
  * `GetClientProfilesAsync(IEnumerable<Guid> profileIds)` (Used for bulk resolution in exports/lists)
  * `GetClientProfileByEmailAsync(Guid organizationId, string email)`

## 7. Database Schema
All tables reside in the isolated `crm` schema.
* `crm.ClientProfiles`
* `crm.OutboxMessages`
* `crm.InboxMessages`
