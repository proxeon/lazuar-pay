# Billing Module (The "Accountant")

## 1. Overview
The `Billing` module is the **Core Domain for Financial Truth**. It acts as the centralized, double-entry accounting ledger for the entire Lazuar ecosystem. While the `Payments` module handles the physical routing of API calls to gateways, the `Billing` module records the mathematical and legal reality of those transactions.

## 2. Core Responsibilities
* **Double-Entry Bookkeeping:** Recording every financial event as balanced `LedgerEntry` and `LedgerLine` records (Assets, Liabilities, Revenue, Expenses).
* **Revenue Recognition:** Amortizing upfront annual payments or event tickets into realized MRR over time via `DeferredRevenueSchedule`.
* **Net Profit Calculation:** Deducting exact Gateway Fees (Stripe/Billplz) and Affiliate Commissions from Gross Revenue to calculate actual cash in the bank.
* **Tax Liability Tracking:** Separating collected SST/VAT into liability accounts so it is never miscounted as platform profit.
* **Accounts Receivable (AR) & Payable (AP):** Tracking unpaid B2B invoices and accrued affiliate payouts.
* **LHDN e-Invoice Prep:** Maintaining tax invoice IDs and validation statuses for Malaysian LHDN compliance.

## 3. Architectural Boundaries (What this module is NOT)
* **Not a Gateway Integrator:** It does not hold Stripe API keys, generate checkout URLs, or parse raw webhook JSON.
* **Not an Access Control System:** It does not know if a user has access to a Telegram group or a Video Vault. It only knows the financial contract.
* **No Cross-Schema Joins:** It queries its own `billing` schema. To get customer names or plan details for reporting, it relies on cross-module read models (e.g., `IBillingQueryService` using Dapper) or event payloads.

## 4. Key Domain Aggregates & Entities
* **`LedgerEntry`**: The aggregate root representing a single financial transaction (e.g., a completed payment, a refund, an issued invoice). Contains `TaxInvoiceId` and `LhdnValidationStatus`.
* **`LedgerLine`**: Child entity representing a single debit or credit line. Tracks `AccountType` (e.g., `ASSET_CASH`, `REVENUE_GROSS`, `EXPENSE_GATEWAY_FEE`, `LIABILITY_TAX_PAYABLE`), `Amount`, and `BaseCurrencyAmount` (normalized to MYR).
* **`DeferredRevenueSchedule`**: Tracks the amortization schedule for upfront payments, moving funds from `LIABILITY_DEFERRED_REVENUE` to `REVENUE_RECOGNIZED` over time.

## 5. Integration Events (Consumed)
The Billing module listens to the global event bus to build the ledger. It does *not* publish events that trigger side-effects; it is the terminal sink for financial data.
* **From Payments:** `GatewayPaymentCompletedIntegrationEvent`, `GatewayRefundCompletedIntegrationEvent`.
* **From Community:** `ZeroAmountCheckoutCompletedIntegrationEvent` (Records 100% coupon discounts for ROI tracking).
* **From B2B/Invoicing:** `InvoiceIssuedIntegrationEvent`, `ManualPaymentRecordedIntegrationEvent`.
* **From Affiliates:** `CommissionAccruedIntegrationEvent`.

## 6. Background Workers
* **`BillingInboxConsumerJob`**: Processes incoming integration events and writes them to the ledger transactionally.
* **`BillingOutboxPublisherJob`**: Standard outbox dispatcher.
* **`RevenueRecognitionJob`**: Runs periodically (e.g., hourly) to scan `DeferredRevenueSchedule` records, calculate elapsed time, and generate new `LedgerEntry` records to recognize deferred revenue.

## 7. Database Schema
All tables reside in the isolated `billing` schema.
* `billing.LedgerEntries`
* `billing.LedgerLines`
* `billing.DeferredRevenueSchedules`
* `billing.OutboxMessages`
* `billing.InboxMessages`

## 8. The Golden Rule of Financial Flow
**The `Payments` module is a dumb pipe. The `Community/Vault` modules manage Access. The `Billing` module manages Truth.** 
Never attempt to calculate MRR or Net Profit by querying the `community.PaymentRecords` or `payments` tables. Always query the `billing.LedgerLines` via the `IBillingQueryService` to ensure gateway fees, taxes, and refunds are accurately accounted for.
