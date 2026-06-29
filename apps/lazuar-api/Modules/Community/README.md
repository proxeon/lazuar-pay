# Community Module (Downstream Access Fulfillment)

## 1. Overview
The `Community` module acts as a strict, downstream **Access Fulfillment Wrapper** within the Lazuar ecosystem. It is completely decoupled from payment processing, ledger accounting, and subscription lifecycle management. It listens to system-wide transaction events and manages membership access levels for active spaces.

## 2. Core Responsibilities
*   **Space Registration:** Defining private virtual environments (`CommunitySpace`) linked to specific Commerce Products (pricing tiers).
*   **Membership Management:** Maintaining the active roster of members (`CommunityMember`) inside private spaces.
*   **Self-Service Delivery:** Generating dynamic, post-purchase links to private resources (such as Telegram join links or Zoom links) inside the buyer dashboard.

## 3. Architectural Boundaries (What this module is NOT)
*   **No Financial Logic:** This module does not manage payments, record invoices, process refunds, or compile double-entry accounting ledgers. Centralized financial operations live in `Billing` and `Commerce`.
*   **No Direct DB Joins:** To maintain strict database isolation, the module never joins its tables to schemas outside of the private `community` schema. Customer profile details are resolved dynamically at the application boundary via `ICrmQueryService`.

## 4. Key Domain Aggregates & Entities
*   **`CommunitySpace`**: The aggregate root mapping private group parameters (Telegram, Zoom) to the Commerce product identifiers that unlock them.
*   **`CommunityMember`**: Junction entity tracking individual CRM profiles and their access status (`ACTIVE`, `SUSPENDED`, `CANCELLED`).

## 5. Integration Events (Consumed)
The module listens to the following events to fulfill access asynchronously:
*   `OrderCompletedIntegrationEvent` (from `Commerce`): Grants access for one-time purchases.
*   `SubscriptionActivatedIntegrationEvent` (from `Commerce`): Grants/restores access upon subscription activation or payment.
*   `SubscriptionSuspendedIntegrationEvent` (from `Commerce`): Suspends membership on dunning failure.
*   `SubscriptionCanceledIntegrationEvent` (from `Commerce`): Cancels access at period end.
