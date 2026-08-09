# Messaging Module (The "Dispatch Router")

## 1. Overview
The `Messaging` module is the centralized, domain-agnostic dispatch engine for outbound communications. It acts as a "dumb pipe" that receives pre-rendered communication payloads from business modules and routes them to physical infrastructure gateways (e.g., Resend for email).

### Product freeze (decision 00.4 / Phase 17)
* **WhatsApp / multi-channel is frozen** for the maintenance horizon (no production WhatsApp channel in the next 6 months per `plans/004-maintenance/decisions.md` §00.4).
* Messaging stays a **thin transport**; Communications remains content/policy owner.
* **Console WhatsApp is not a production channel** — docs and defaults must not claim automated WhatsApp dunning as live.
* **No merge into Communications** until product funds a real multi-channel provider (then reopen 00.4 / Phase 16).
* Email + channel ports (`IEmailService`, `IMessagingService`, Resend, brand HTML) are **module-owned** (R34); channel product work is not “just another adapter PR” without reopening 00.4.

## 2. Core Responsibilities
* **Universal Dispatch:** Consuming the generic `DispatchMessageIntegrationEvent` and routing the payload to module-owned `IEmailService` (Resend) or `IMessagingService` (console WA stub) based on the requested `Channel`.
* **Physical send adapters:** Owns Resend HTTP, Console email, brand HTML wrapper (`EmailTemplateBuilder`), and the WhatsApp console stub. Communications owns BYOK config, suppressions, and inbound Resend webhooks.
* **Tenant Replication:** Maintaining a localized, read-only replica of Tenant metadata (`TenantReplica`) to allow the messaging infrastructure to resolve tenant slugs and statuses without querying the `One` module's database.
* **HTML/Text Sanitization:** Stripping HTML tags from email bodies when routing to SMS/plain-text channels to ensure clean plain-text delivery.

## 3. Architectural Boundaries (What this module is NOT)
* **Not a Template Engine:** *Crucial architectural shift.* This module does **not** store message templates, subject lines, or automation rules. Templates are owned by **Communications** (and rendered by the domain modules that trigger them, e.g. Commerce dunning). The `Messaging` module only receives the *final rendered HTML/Text*.
* **Not a Campaign Manager:** It does not handle bulk marketing logic, A/B testing, or subscriber segmentation. It simply processes the dispatch queue.
* **Completely Context-Blind:** It does not know *why* an email is being sent. It only knows *who* to send it to and *what* the final content is.

## 4. Key Domain Aggregates & Entities
* **`TenantReplica`**: A localized cache of the `Organization` entity from the `One` module. Ensures the messaging module can operate independently even if the core identity database is under heavy load.

## 5. Integration Events
### Consumed
* **`DispatchMessageIntegrationEvent`**: The universal event published by *any* live module (Commerce, One, Communications, Billing, Ops, etc.) when a message needs to be sent. Contains `ToEmail`, `ToPhone`, `Subject`, `HtmlBody`, and `Channel`.
* **`TenantProvisionedIntegrationEvent`** (from `One`): Creates a new `TenantReplica` when a new workspace is born.
* **`WorkspaceUpdatedIntegrationEvent`** (from `One`): Updates the `TenantReplica` if the workspace name or slug changes.

### Published
* *None.* The Messaging module is a terminal sink for communication events. It does not publish events that trigger downstream business logic.

## 6. Background Workers
* **`MessagingInboxConsumerJob`** (`Infrastructure/Workers/`): Processes incoming tenant replication events and universal dispatch requests transactionally.
* **`MessagingOutboxPublisherJob`** (`Infrastructure/Workers/`): Standard outbox dispatcher for any events the module might need to emit in the future.

Integration-event handlers (inbox enqueue + dispatch) live under `Infrastructure/EventHandlers/`. Domain-side tenant replica updates are MediatR notification handlers under `Application/EventHandlers/`.

## 7. Database Schema
All tables reside in the isolated `messaging` schema.
* `messaging.TenantReplicas`
* `messaging.OutboxMessages`
* `messaging.InboxMessages`

## 8. The Golden Rule of Messaging
**"Render at the Source, Dispatch at the Edge."**
If you are writing a new feature in `Commerce`, `Communications`, or another live module, you must fetch the template, inject the variables (e.g., `{{customer_name}}`), and publish the *final rendered string* via `DispatchMessageIntegrationEvent`. Never attempt to pass raw template IDs or variable dictionaries to the `Messaging` module.
