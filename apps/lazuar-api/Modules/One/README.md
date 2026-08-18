# One Module (The "Global Identity & Provisioning Core")

## 1. Overview
The `One` module is the central nervous system of the Lazuar platform. It acts as the global CIAM (Customer Identity and Access Management) system, the multi-tenant workspace provisioner, and the authoritative registry for cross-module application entitlements. Every request in the ecosystem ultimately traces its authorization back to the identity and tenant mappings managed here.

## 2. Core Responsibilities
* **Global Authentication:** Managing master user credentials (`GlobalUser`), JWT generation, password hashing (BCrypt), and email verification flows.
* **Workspace Provisioning:** Creating and managing tenant organizations (`Organization`), including slug validation and archival.
* **Entitlement Management:** Toggling access to specific ecosystem apps (e.g., `COMMERCE`, `OPS`, `BILLING`) per workspace via `TenantAppEntitlement`. Legacy app IDs such as `COMMUNITY` / `VAULT` may still appear in older rows or handlers but those modules are removed (ADR 022).
* **Public self-serve signup:** `POST /one/public/register` creates the user, first workspace, ADMIN membership, and core entitlements immediately. There is no Superadmin approval queue.
* **Workspace Invitations:** Generating secure, time-bound magic links to invite staff/admins to existing workspaces.
* **Identity Synchronization:** Broadcasting profile updates so downstream modules (like `CRM`) can keep localized tenant records in sync with the global master identity.

## 3. Architectural Boundaries (What this module is NOT)
* **Not a Tenant-Specific Business Engine:** It does not manage subscription billing, Commerce products/plans, or localized message templates. 
* **Not a PII Registry for Customers:** While it holds the *master* identity of platform users (Admins/Staff), the localized PII of a tenant's *customers* (subscribers, leads) is strictly managed by the `CRM` module.
* **No Cross-Schema Foreign Keys:** Downstream modules reference `OrganizationId` and `GlobalUserId` strictly as primitive `Guid` values. The `One` module does not hold foreign keys pointing to downstream business entities.

## 4. Key Domain Aggregates & Entities
* **`GlobalUser`**: The aggregate root for platform-wide identity. Stores email, BCrypt password hash, security stamps, and system admin flags.
* **`Organization`**: Represents a tenant/workspace. Enforces strict slug validation rules (e.g., blocking reserved system slugs).
* **`TenantMembership`**: The junction entity linking a `GlobalUser` to an `Organization` with a staff `Role` (`ADMIN`, `MEMBER`, `VIEWER`). Cookie JWT role is separately `CLIENT` or `SUPER_ADMIN` and is injected with membership only after `X-Tenant-Id` / slug resolves.
* **`TenantAppEntitlement`**: Tracks which ecosystem modules are actively provisioned and billed for a specific workspace.
* **`WorkspaceInvitation`**: Time-bound, cryptographically hashed invitation tokens for onboarding new staff.

## 5. Integration Events
### Published
* **`TenantProvisionedIntegrationEvent`**: Fired when a new workspace is created. Triggers downstream modules to initialize tenant-specific schemas/replicas.
* **`WorkspaceUpdatedIntegrationEvent`**: Fired when workspace name/slug changes.
* **`GlobalUserProfileUpdatedIntegrationEvent`**: Fired when a user changes their master name/email.
* **`AppEntitlementGrantedIntegrationEvent`**: Fired when a new app (e.g., `COMMERCE`) is toggled on for a tenant. Triggers JIT (Just-In-Time) seeding of default templates or configurations in the target module.

### Consumed
* Subscription / portal membership activation is driven by live Commerce lifecycle integration events. Paying does **not** insert a `CLIENT` `TenantMembership` — that string is a JWT role, not staff. Invite still rejects `CLIENT`.

## 6. Background Workers
* **`SystemGenesisBootstrapperJob`**: Runs on startup to guarantee the System Tenant exists and securely upserts root Superadmin credentials from environment variables.
* **`OneInboxConsumerJob` / `OneOutboxPublisherJob`**: Standard transactional outbox/inbox workers for asynchronous event processing.
* **`OutboundWebhookDispatcherJob`**: Claims `WebhookDeliveryOutbox` rows and delivers signed HTTP webhooks to customer endpoints (retries / fail terminal).

## 7. Platform outbound webhooks (durable model)

One owns the **only platform-grade** customer webhook system (maintenance decision **00.2**). Other modules request delivery by publishing `OutboundWebhookRequestedIntegrationEvent` (Commerce.Contracts); they do **not** implement their own outbox/signing stacks.

| Piece | Responsibility |
|-------|----------------|
| **`TenantWebhookEndpoint`** | Per-workspace multi-endpoint registry (URL, secret, active, `EnabledEvents`) |
| **`WebhookDeliveryOutbox`** | Durable per-delivery queue (claim lease, up to 5 attempts, exponential backoff) |
| **`OutboundWebhookEventHandlers`** | Fan-out integration events → outbox rows for matching endpoints |
| **`OutboundWebhookDispatcherJob`** | HTTP POST via named client `"DeveloperWebhooks"` |
| **`OutboundWebhookSignature`** | Standard Webhooks–style header: `t={unix},v1={hmac_hex}` over `{timestamp}.{body}` |

**Headers on delivery:** `X-Lazuar-Signature`, `X-Lazuar-Event`, `X-Lazuar-Delivery-Id`, `X-Lazuar-Webhook-Id`.

**Typical event types today:** `subscription.*`, `order.completed`, `payment_link.paid`, `payment.completed` / `payment.failed` (from Commerce / Payments publishers); **LHDN** `invoice.valid` / `invoice.invalid` (from Lhdn `DispatchExternalWebhookCommand` → `OutboundWebhookRequestedIntegrationEvent`).

**LHDN invoice events (R42/R43):** MyInvois VALID/INVALID poll publishes data-only payload via Lhdn outbox → this dispatcher fans out to `one.TenantWebhookEndpoints` with `EnabledEvents` matching `invoice.valid` / `invoice.invalid`. Fire-and-forget Lhdn sender is **retired**. See `Modules/Lhdn/README.md` §5. Webhooks stay in One for this maintenance track (no `Modules/Webhooks` extract unless Phase 16).

## 8. Database Schema
All tables reside in the isolated `one` schema.
* `one.GlobalUsers`
* `one.Organizations`
* `one.TenantMemberships`
* `one.TenantAppEntitlements`
* `one.WorkspaceInvitations`
* `one.ApiCredentials` — **platform API keys** (SSoT mint/list/revoke)
* `one.TenantWebhookEndpoints` / `one.WebhookDeliveryOutboxes`
* `one.OutboxMessages` / `one.InboxMessages`

## 9. Platform API credentials (SSoT) — One-only (R05)

* **SSoT:** Machine client keys live in `one.ApiCredentials` (`ApiCredential` aggregate). Mint/list/revoke go through One commands / `IApiCredentialService` (also used by Lhdn `/lhdn/api-keys` façades).
* **Scopes:** Closed catalog on `PlatformApiScopes` (includes `lhdn.documents:*`, payments checkout scopes, webhook manage). LHDN product scopes are modeled on One credentials (decisions 00.1).
* **Host auth (One-only, R05):** `ApiKeyAuthenticationMiddleware` reads **only** `one.ApiCredentials`. Lhdn dual-read is **closed** — Lhdn-only keys receive **401**.
* **Revoke cache:** host subscribes **only** `Modules.One.Contracts.Events.ApiKeyRevokedIntegrationEvent` (Lhdn dual-subscribe removed).
* **DEPLOY gate:** ship One-only middleware to an env **only after** inventory Q8 `active_legacy_only = 0` (or signed residual quarantine). See `plans/005-remaining/r05-notes.md`.
* **Table drop:** `lhdn.DeveloperApiKeys` archive/drop is **R06** (≥ 30 days after One-only in prod) — not R05.
* **Legacy migrator (R03):** host job `Lazuar.Api/Jobs/ApiKeyMigration` for residual rows before cutover. Runbook: `plans/005-remaining/r03-keys-migrator-runbook.md`.
* **Design / inventory:** `plans/004-maintenance/api-key-cutover-design.md`, `plans/005-remaining/01-api-key-one-only-cutover.md`.
