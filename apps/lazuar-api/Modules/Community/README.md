# Community Module (The "Subscription & Retention Engine")

## 1. Overview
The `Community` module is the core business engine for managing recurring subscriptions, member lifecycles, automated dunning, and localized tenant configurations. It acts as the fulfillment layer that grants access to community resources (Telegram, Zoom) upon successful payment verification.

## 2. Core Responsibilities
* **Plan & Tier Management:** Defining subscription catalogs (`CommunityPlan`), including pricing, capacity limits, and fulfillment links.
* **Subscriber Lifecycle State Machine:** Managing the strict state transitions of a subscription (`PENDING` -> `ACTIVE` -> `PAST_DUE` -> `EXPIRED` / `CANCELLED` / `BANNED`).
* **Automated Dunning & Reminders:** Scheduling and dispatching automated renewal reminders via the `Messaging` module based on tenant-configured rules.
* **Coupon & Discount Management:** Generating, reserving, and redeeming promotional codes with strict idempotency and expiration rules.
* **Broadcast Campaigns:** Scheduling bulk announcements to active subscribers or specific plan cohorts.
* **Localized Template Management:** Storing tenant-specific email/WhatsApp templates for community events (Welcome, Renewal, Cancellation).
* **Public Checkout & Portals:** Generating payment sessions and securing magic-link subscriber portals for end-users to manage their billing.

## 3. Architectural Boundaries (What this module is NOT)
* **Not a Payment Gateway:** It does not hold Stripe/Billplz API keys or parse raw webhooks. It relies entirely on the `Payments` module to generate checkout URLs and publish `GatewayPaymentCompletedIntegrationEvent`.
* **Not a PII Database:** It does not store the master customer profile. It references the `CRM` module's `ClientProfileId` to resolve names and emails for templating.
* **Not a Message Dispatcher:** It renders templates and publishes `DispatchMessageIntegrationEvent`, but the physical delivery of SMS/Email is handled by the `Messaging` module.
* **No Financial Accounting:** It tracks `PaymentRecord` strictly as an *Access Grant Log* (proof of payment for the current cycle). True financial ledger accounting (MRR, Gateway Fees, Taxes) is handled by the `Billing` module.

## 4. Key Domain Aggregates & Entities
* **`CommunityPlan`**: The catalog aggregate. Holds pricing, intervals, capacity limits, and JSON-serialized features/FAQs.
* **`CommunitySubscription`**: The lifecycle aggregate. Enforces strict state transition rules and holds the navigation collection of `PaymentRecord` and `ReminderDispatchLog`.
* **`CommunityCoupon`**: Manages discount logic, usage limits, and checkout reservation locks to prevent race conditions.
* **`BroadcastCampaign`**: Aggregate for scheduling and tracking bulk message delivery jobs.
* **`CommunityReminderSchedule`**: Defines the rules for automated dunning (e.g., "Send Email 3 days before due date").
* **`MessageTemplate`**: Tenant-scoped entity storing localized copy for automated notifications.

## 5. Integration Events
### Published
* **`CommunitySubscriptionActivatedIntegrationEvent`**: Fired when a subscription enters the `ACTIVE` state. Triggers the `One` module to grant portal access and the `Messaging` module to send welcome kits.
* **`CommunitySubscriptionCancelledIntegrationEvent`**: Fired on cancellation.
* **`CommunityCheckoutInitiatedIntegrationEvent`**: Fired when a user reaches the checkout page. Triggers abandoned cart timers in `Messaging`.
* **`CommunityRenewalReminderDueIntegrationEvent`**: Fired by the lifecycle worker to trigger a dunning email/SMS.
* **`DispatchMessageIntegrationEvent`**: The universal command sent to the `Messaging` module to physically deliver rendered templates.

### Consumed
* **`GatewayPaymentCompletedIntegrationEvent`** (from `Payments`): Records the payment, activates the subscription, and extends the billing cycle.
* **`GatewayPaymentFailedIntegrationEvent`** (from `Payments`): Transitions the subscription to `PAST_DUE`.
* **`GatewayRefundCompletedIntegrationEvent`** (from `Payments`): Logs the refund and updates the subscription state.
* **`ClientProfileAnonymizedIntegrationEvent`** (from `CRM`): Instantly bans the user and cancels all active subscriptions to comply with GDPR/PDPA deletion requests.
* **`AppEntitlementGrantedIntegrationEvent`** (from `One`): Triggers JIT seeding of default `MessageTemplate` records when the Community app is first enabled for a tenant.

## 6. Background Workers
* **`CommunityLifecycleJob`**: The critical hourly cron worker. It transitions overdue subscriptions to `PAST_DUE`/`EXPIRED` based on grace periods, evaluates `ReminderSchedules`, and dispatches renewal events.
* **`BroadcastPublisherJob`**: Processes pending `BroadcastCampaign` aggregates, chunking recipients to prevent event-bus timeouts.
* **`CommunityInboxConsumerJob` / `CommunityOutboxPublisherJob`**: Standard transactional workers.

## 7. Database Schema
All tables reside in the isolated `community` schema.
* `community.Plans`
* `community.Subscriptions`
* `community.PaymentRecords`
* `community.ReminderSchedules`
* `community.ReminderDispatchLogs` (Idempotency guard against reminder storms)
* `community.Coupons`
* `community.BroadcastCampaigns`
* `community.MessageTemplates`
* `community.OutboxMessages` / `community.InboxMessages`
