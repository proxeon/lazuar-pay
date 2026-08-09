# Payments Module (The "Cashier")

## 1. Overview
The `Payments` module acts as a strict **Infrastructure Port and Gateway Orchestrator**. It is responsible for translating third-party payment provider APIs (Stripe, Billplz, FPX, Curlec) into internal system events. It handles the physical movement of money, API key management, webhook signature verification, and idempotency.

## 2. Core Responsibilities
* **Gateway Orchestration:** Generating checkout sessions and customer portal URLs via provider-specific adapters.
* **Webhook Ingestion:** Receiving raw HTTP callbacks from payment gateways, verifying cryptographic signatures, and parsing the payloads.
* **Idempotency Enforcement:** Guaranteeing that duplicate webhook retries from gateways do not result in duplicate internal events using the `PaymentWebhookLog`.
* **Fee & Tax Extraction:** Extracting exact gateway processing fees and FX rates directly from gateway payloads (or estimating them via tenant profiles) before publishing events.

## 3. Architectural Boundaries (What this module is NOT)
* **Not an Accounting Ledger:** This module does *not* calculate MRR, Net Profit, Recognized Revenue, or Tax Liabilities. It only reports the *Gross Amount* and *Gateway Fee* extracted from the provider.
* **Not a Fulfillment Engine:** It does not activate Commerce subscriptions, unlock products, or manage subscription lifecycles. It only reports that a financial transaction occurred.
* **Stateless regarding Checkouts:** It does not store pending checkout sessions in the database. Context (like `subscription_id`) is passed through gateway metadata or callback URL query strings.

## 4. Key Domain Aggregates & Entities
* **`TenantPaymentConfiguration`**: Stores encrypted API keys, webhook secrets, merchant IDs, and estimated fee profiles for each gateway type per tenant.
* **`PaymentWebhookLog`**: A strict idempotency ledger tracking `(Provider, EventId)` to block duplicate webhook processing.

## 5. Integration Events (Published)
The Payments module publishes universal financial events to the Outbox. Other modules (Commerce, Billing, Communications) subscribe to these to fulfill orders or record ledger truth.
* `GatewayPaymentCompletedIntegrationEvent`: Fired when a charge succeeds. Includes `AmountPaid`, `GatewayFee`, `TaxAmount`, `NetAmount`, `FxRate`, and `Metadata`.
* `GatewayPaymentFailedIntegrationEvent`: Fired when a checkout or charge fails.
* `GatewayRefundCompletedIntegrationEvent`: Fired when a refund is successfully processed at the gateway level.
* `GatewayRefundRequestedIntegrationEvent`: Internal event triggered when a domain module requests a refund.

## 6. Adapters (Ports & Adapters Pattern)
* **`IPaymentGatewayAdapter`**: The strict port interface.
* **`StripeGatewayAdapter`**: Integrates with Stripe.net SDK. Extracts exact fees from the `balance_transaction` object.
* **`BillplzGatewayAdapter`**: Integrates with Billplz REST API. Uses HMACSHA256 for webhook verification and relies on `TenantPaymentConfiguration` for fee estimation since Billplz webhooks lack fee data.

## 7. Database Schema
All tables reside in the isolated `payments` schema.
* `payments.TenantPaymentConfigurations`
* `payments.PaymentWebhookLogs`
* `payments.OutboxMessages`
* `payments.InboxMessages`
