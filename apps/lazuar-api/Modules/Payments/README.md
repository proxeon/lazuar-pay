# Payments Module (The "Cashier")

## 1. Overview
The `Payments` module is the gateway orchestrator. Live adapters are **Stripe**, **Billplz**, **CHIP**, **Razorpay**, and **Xendit**. FPX/DuitNow QR/hosted wallets/e-mandate capability flags exist on the matrix but have no generate-time readers — they are not product. It handles checkout mint, webhook verify, idempotency, and M2M integration checkouts.

## 2. Core Responsibilities
* **Gateway Orchestration:** Generating checkout sessions and customer portal URLs via provider-specific adapters.
* **Webhook Ingestion:** Receiving raw HTTP callbacks from payment gateways, verifying cryptographic signatures, and parsing the payloads.
* **Idempotency Enforcement:** Guaranteeing that duplicate webhook retries from gateways do not result in duplicate internal events using the `PaymentWebhookLog`.
* **Fee & Tax Extraction:** Extracting gateway processing fees and FX rates from the payload when the processor includes them. Stripe expand / CHIP `payment` misses stamp `gateway_fee_status=unknown` and still fulfill; `GatewayFee=0` then is not "the fee is zero". Billplz journals are gross-only (estimated fee args on `ParseWebhookAsync` are unused).

## 3. Architectural Boundaries (What this module is NOT)
* **Not an Accounting Ledger:** This module does *not* calculate MRR, Net Profit, Recognized Revenue, or Tax Liabilities. It only reports the *Gross Amount* and *Gateway Fee* extracted from the provider.
* **Not a Fulfillment Engine:** It does not activate Commerce subscriptions, unlock products, or manage subscription lifecycles. It only reports that a financial transaction occurred.
* **Not checkout-stateless:** Machine (`/integrations/payments/checkouts`) sessions are stored as `IntegrationCheckoutSessions`. Commerce hop-2 still passes `subscription_id` through gateway metadata.

## 4. Key Domain Aggregates & Entities
* **`TenantPaymentConfiguration`**: Stores encrypted API keys, webhook secrets, merchant IDs. Estimated fee profile columns are unused (handler passes 0, 0, 0).
* **`PaymentWebhookLog`**: A strict idempotency ledger tracking `(Provider, EventId)` (and tenant) to block duplicate webhook processing.
* **`IntegrationCheckoutSession`**: M2M checkout row (amount, status, TTL, outbound webhook).

## 5. Integration Events (Published)
The Payments module publishes universal financial events to the Outbox. Other modules (Commerce, Billing, Communications) subscribe to these to fulfill orders or record ledger truth.
* `GatewayPaymentCompletedIntegrationEvent`: Fired when a charge succeeds. Includes `AmountPaid`, `GatewayFee`, `TaxAmount`, `NetAmount`, `FxRate`, and `Metadata`.
* `GatewayPaymentFailedIntegrationEvent`: Fired when a checkout or charge fails.
* `GatewayRefundCompletedIntegrationEvent`: Fired when a refund is successfully processed at the gateway level (Stripe inbound + CHIP `payment.refunded`).
* `GatewayRefundRequestedIntegrationEvent`: Internal event triggered when a domain module requests a refund.

## 6. Adapters (Ports & Adapters Pattern)
* **`IPaymentGatewayAdapter`**: The strict port interface.
* **`StripeGatewayAdapter`**: Stripe.net. Copies fees from `balance_transaction` when expand succeeds; otherwise `gateway_fee_status=unknown`.
* **`BillplzGatewayAdapter`**: HMACSHA256 webhooks. Fee formula exists but production always receives 0, 0, 0.
* **`ChipCollectGatewayAdapter`**: CHIP Collect (`gate.chip-in.asia`). Fees from `payment.fee_amount` when present.
* **`RazorpayGatewayAdapter`**: Payment links. Reminder-only (`SupportsOffSession` is false).
* **`XenditGatewayAdapter`**: Invoices. Reminder-only. `xendit_payment_methods` metadata is an unused filter hook.

## 7. Database Schema
All tables reside in the isolated `payments` schema.
* `payments.TenantPaymentConfigurations`
* `payments.PaymentWebhookLogs`
* `payments.IntegrationCheckoutSessions`
* `payments.OutboxMessages`
* `payments.InboxMessages`
