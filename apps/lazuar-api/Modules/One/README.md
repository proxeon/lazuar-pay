# One Module (The "Global Identity & Provisioning Core")

## 1. Overview
The `One` module is the central nervous system of the Lazuar platform. It acts as the global CIAM (Customer Identity and Access Management) system, the multi-tenant workspace provisioner, and the authoritative registry for cross-module application entitlements. Every request in the ecosystem ultimately traces its authorization back to the identity and tenant mappings managed here.

## 2. Core Responsibilities
* **Global Authentication:** Managing master user credentials (`GlobalUser`), JWT generation, password hashing (BCrypt), and email verification flows.
* **Workspace Provisioning:** Creating and managing tenant organizations (`Organization`), including slug validation and archival.
* **Entitlement Management:** Toggling access to specific ecosystem apps (e.g., `COMMUNITY`, `VAULT`, `OPS`) per workspace via `TenantAppEntitlement`.
* **Onboarding Queue:** Managing the B2B application and approval flow (`AppAccessRequest`) for new Superadmin-led workspace provisioning.
* **Workspace Invitations:** Generating secure, time-bound magic links to invite staff/admins to existing workspaces.
* **Identity Synchronization:** Broadcasting profile updates so downstream modules (like `CRM`) can keep localized tenant records in sync with the global master identity.

## 3. Architectural Boundaries (What this module is NOT)
* **Not a Tenant-Specific Business Engine:** It does not manage subscription billing, community plans, or localized message templates. 
* **Not a PII Registry for Customers:** While it holds the *master* identity of platform users (Admins/Staff), the localized PII of a tenant's *customers* (subscribers, leads) is strictly managed by the `CRM` module.
* **No Cross-Schema Foreign Keys:** Downstream modules reference `OrganizationId` and `GlobalUserId` strictly as primitive `Guid` values. The `One` module does not hold foreign keys pointing to downstream business entities.

## 4. Key Domain Aggregates & Entities
* **`GlobalUser`**: The aggregate root for platform-wide identity. Stores email, BCrypt password hash, security stamps, and system admin flags.
* **`Organization`**: Represents a tenant/workspace. Enforces strict slug validation rules (e.g., blocking reserved system slugs).
* **`TenantMembership`**: The junction entity linking a `GlobalUser` to an `Organization` with a specific `Role` (e.g., `ADMIN`, `CLIENT`).
* **`TenantAppEntitlement`**: Tracks which ecosystem modules are actively provisioned and billed for a specific workspace.
* **`WorkspaceInvitation`**: Time-bound, cryptographically hashed invitation tokens for onboarding new staff.
* **`AppAccessRequest`**: The aggregate managing the "Request Access" onboarding queue for prospective Superadmin approval.

## 5. Integration Events
### Published
* **`TenantProvisionedIntegrationEvent`**: Fired when a new workspace is created. Triggers downstream modules to initialize tenant-specific schemas/replicas.
* **`WorkspaceUpdatedIntegrationEvent`**: Fired when workspace name/slug changes.
* **`GlobalUserProfileUpdatedIntegrationEvent`**: Fired when a user changes their master name/email.
* **`AppEntitlementGrantedIntegrationEvent`**: Fired when a new app (e.g., `COMMUNITY`) is toggled on for a tenant. Triggers JIT (Just-In-Time) seeding of default templates or configurations in the target module.

### Consumed
* **`CommunitySubscriptionActivatedIntegrationEvent`**: Listens to the Community module. When a public user pays for a subscription, `One` automatically generates a `TenantMembership` with the `CLIENT` role, granting them portal access to that specific workspace.

## 6. Background Workers
* **`SystemGenesisBootstrapperJob`**: Runs on startup to guarantee the System Tenant exists and securely upserts root Superadmin credentials from environment variables.
* **`OneInboxConsumerJob` / `OneOutboxPublisherJob`**: Standard transactional outbox/inbox workers for asynchronous event processing.

## 7. Database Schema
All tables reside in the isolated `one` schema.
* `one.GlobalUsers`
* `one.Organizations`
* `one.TenantMemberships`
* `one.TenantAppEntitlements`
* `one.WorkspaceInvitations`
* `one.AppAccessRequests`
* `one.OutboxMessages` / `one.InboxMessages`
