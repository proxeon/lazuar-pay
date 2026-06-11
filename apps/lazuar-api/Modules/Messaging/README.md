# Messaging Module (The "Dispatch Router")

## 1. Overview
The `Messaging` module is the centralized, domain-agnostic dispatch engine for all outbound communications (Email, SMS, WhatsApp). It acts as a "dumb pipe" that receives pre-rendered communication payloads from business modules and routes them to the physical infrastructure gateways (e.g., Resend, Twilio, Meta).

## 2. Core Responsibilities
* **Universal Dispatch:** Consuming the generic `DispatchMessageIntegrationEvent` and routing the payload to the appropriate `IEmailService` or `IMessagingService` building block based on the requested `Channel`.
* **Tenant Replication:** Maintaining a localized, read-only replica of Tenant metadata (`TenantReplica`) to allow the messaging infrastructure to resolve tenant slugs and statuses without querying the `One` module's database.
* **HTML/Text Sanitization:** Stripping HTML tags from email bodies when routing to SMS/WhatsApp channels to ensure clean plain-text delivery.

## 3. Architectural Boundaries (What this module is NOT)
* **Not a Template Engine:** *Crucial architectural shift.* This module does **not** store message templates, subject lines, or automation rules. Templates are strictly owned by the domain modules that use them (e.g., the `Community` module owns "Payment Reminder" templates). The `Messaging` module only receives the *final rendered HTML/Text*.
* **Not a Campaign Manager:** It does not handle bulk marketing logic, A/B testing, or subscriber segmentation. It simply processes the dispatch queue.
* **Completely Context-Blind:** It does not know *why* an email is being sent. It only knows *who* to send it to and *what* the final content is.

## 4. Key Domain Aggregates & Entities
* **`TenantReplica`**: A localized cache of the `Organization` entity from the `One` module. Ensures the messaging module can operate independently even if the core identity database is under heavy load.

## 5. Integration Events
### Consumed
* **`DispatchMessageIntegrationEvent`**: The universal event published by *any* module (Community, One, Vault) when a message needs to be sent. Contains `ToEmail`, `ToPhone`, `Subject`, `HtmlBody`, and `Channel`.
* **`TenantProvisionedIntegrationEvent`** (from `One`): Creates a new `TenantReplica` when a new workspace is born.
* **`WorkspaceUpdatedIntegrationEvent`** (from `One`): Updates the `TenantReplica` if the workspace name or slug changes.

### Published
* *None.* The Messaging module is a terminal sink for communication events. It does not publish events that trigger downstream business logic.

## 6. Background Workers
* **`MessagingInboxConsumerJob`**: Processes incoming tenant replication events and universal dispatch requests transactionally.
* **`MessagingOutboxPublisherJob`**: Standard outbox dispatcher for any events the module might need to emit in the future.

## 7. Database Schema
All tables reside in the isolated `messaging` schema.
* `messaging.TenantReplicas`
* `messaging.OutboxMessages`
* `messaging.InboxMessages`

## 8. The Golden Rule of Messaging
**"Render at the Source, Dispatch at the Edge."**
If you are writing a new feature in the `Community` or `Vault` module, you must fetch the template, inject the variables (e.g., `{{customer_name}}`), and publish the *final rendered string* via `DispatchMessageIntegrationEvent`. Never attempt to pass raw template IDs or variable dictionaries to the `Messaging` module.
