# Billing Module (The "Accountant")

## 1. Overview
The `Billing` module is the **Core Domain for Financial Truth**. It acts as the centralized, double-entry accounting ledger for the entire Lazuar ecosystem. While the `Payments` module handles the physical routing of API calls to gateways, the `Billing` module records the mathematical and legal reality of those transactions.

## 2. Core Responsibilities
* **Double-Entry Bookkeeping:** Recording every financial event as balanced `LedgerEntry` and `LedgerLine` records (Assets, Liabilities, Revenue, Expenses).
* **Revenue Recognition:** Amortizing upfront annual payments or event tickets into realized MRR over time via `DeferredRevenueSchedule` (**not live** until schedules are created from product periods — see §6).
* **Net revenue / net profit:** `GET /admin/billing/summary` `net_revenue` is P&L net (gross − refunds − discounts − booked gateway fees − tax). It is **not** `SUM(ASSET_CASH)` and ignores Hub/pack expense. `GET /admin/billing/net-profit` also subtracts commission.
* **Tax Liability Tracking:** Separating collected SST/VAT into liability accounts so it is never miscounted as platform profit.
* **Accounts Receivable (AR) & Payable (AP):** Tracking unpaid B2B invoices and accrued affiliate payouts.
* **LHDN e-Invoice Prep:** Maintaining customer receipt numbers, consolidation eligibility, and LHDN document UUIDs/statuses for Malaysian compliance.

## 3. Architectural Boundaries (What this module is NOT)
* **Not a Gateway Integrator:** It does not hold Stripe API keys, generate checkout URLs, or parse raw webhook JSON.
* **Not an Access Control System:** It does not know if a user has an active Commerce subscription or portal access. It only knows the financial contract.
* **No Cross-Schema Joins:** Billing does not query `commerce` / `crm` tables directly. Customer display for final receipts and proforma drafts is resolved via Commerce ports (`ICommerceDocumentLookup`).

## 3.1 Handler layer note (intentional today)

**Command and integration-event handlers currently live in `Infrastructure/`** (`Commands/`, `EventHandlers/`), not `Application/`. Application is thin (queries + LLM prompts + repository port). MediatR registers the Infrastructure assembly, so this is DI-safe.

This is a known inversion vs Commerce/Lhdn (Application-owned handlers). A full rebalance (ports + moving handlers into Application + test updates) is a **separate epic** — do not “fix” placement casually in drive-by PRs.

## 4. Key Domain Aggregates & Entities
* **`LedgerEntry`**: Aggregate root for a single financial transaction.
  * **`CustomerDocumentNumber`**: Immutable customer-facing receipt # (never overwritten by LHDN).
  * **`LhdnDocumentUuid`**: MyInvois UUID after submit/validate.
  * **`ConsolidationStatus`**: `PENDING` / `CONSOLIDATED` / `NOT_REQUIRED` / `IGNORED` (B2C monthly consolidation eligibility).
  * **`LhdnValidationStatus`**: LHDN lifecycle (`B2C_RECEIPT`, `CONSOLIDATED_PENDING`, `VALID`, `CANCELLED`, …).
  * **`TaxInvoiceId`**: Legacy dual-use field kept for back-compat; new writers prefer the fields above.
* **`LedgerLine`**: Debit/credit line with `AccountType` constants from `Modules.Billing.Domain.AccountTypes` (e.g. `ASSET_CASH`, `REVENUE_GROSS`, `EXPENSE_GATEWAY_FEE`, `LIABILITY_TAX_PAYABLE`).
* **`DeferredRevenueSchedule`**: Table/entity retained for future amortization; recognition job is **not registered** until product-period schedules are created.

## 5. Integration Events (Consumed)
The Billing module listens to the global event bus to build the ledger. It does *not* publish events that trigger side-effects; it is the terminal sink for financial data.
* **From Payments:** `GatewayPaymentCompletedIntegrationEvent`, `GatewayRefundCompletedIntegrationEvent`, `GatewayDisputeCreatedIntegrationEvent` (utility chargeback).
* **From Commerce:** `ZeroAmountCheckoutCompletedIntegrationEvent` (Records 100% coupon discounts for ROI tracking).
* **From B2B/Invoicing:** `InvoiceIssuedIntegrationEvent`, `ManualPaymentRecordedIntegrationEvent`.
* **From Affiliates:** `CommissionAccruedIntegrationEvent`.
* **From LHDN:** `LhdnDocumentValidated|Cancelled` (touch LHDN fields only; never overwrite `CustomerDocumentNumber`).

## 6. Background Workers
* **`BillingInboxConsumerJob`**: Processes incoming integration events and writes them to the ledger transactionally.
* **`BillingOutboxPublisherJob`**: Standard outbox dispatcher.
* **`B2cConsolidationJob`**: Monthly (28th MYT) consolidates prior-calendar-month `B2C_RECEIPT` / `PENDING` sales into a consolidated LHDN invoice. Idempotent per org/month (`B2C-CONS-{yyyyMM}-{orgId}`).
* **`RevenueRecognitionJob`**: **Parked / not registered (decision 00.3).** Unregistered by design until a product epic owns deferred revenue schedule creation (finance / Xero track). Entity/table may remain; **no shipping claim that recognition runs.** Re-enable in DI only with schedule writers + idempotent ledger external refs (product epic — not maintenance drive-by).

## 7. Database Schema
All tables reside in the isolated `billing` schema.
* `billing.LedgerEntries`
* `billing.LedgerLines`
* `billing.DeferredRevenueSchedules`
* `billing.OutboxMessages`
* `billing.InboxMessages`

## 8. The Golden Rule of Financial Flow
**The `Payments` module is a dumb pipe. `Commerce` manages subscriptions/checkout access state. The `Billing` module manages Truth.**

- **Payments** = gateway adapters + webhook ingress only (no MRR math).
- **Commerce** = products, subscriptions, dunning, offline payments, portal links (access/lifecycle).
- **Billing** = double-entry ledger, tax liability, LHDN document linkage, credits wallet.

Never calculate MRR, net cash, or tax payable by querying gateway tables or Commerce payment-log rows alone. Always query `billing.LedgerLines` / `IBillingQueryService` so gateway fees, discounts, refunds, and tax are balanced.

> Historical note: ADR 014/020 era copy referred to Community/Vault as access owners. Those modules were removed (ADR 022); Pure CaaS MVP hides LHDN-heavy UI (ADR 023) but Billing still records truth for every cleared payment.

## 9. Document download surfaces (contract honesty · R23)

| Route | Product OpenAPI? | Behavior |
|-------|------------------|----------|
| `GET /admin/billing/ledger/{id}/document` | **Yes** — `DocumentDownloadUrlDto` | OrgAdmin; JSON `{ url }` R2 presign |
| `GET /public/billing/{tenantSlug}/documents/draft/{sessionId}?sig&exp` | **Yes** — PDF `bytes` | HMAC draft proforma (checkout `draft_pdf_url`) |
| `GET /public/billing/{tenantSlug}/documents/{ledgerEntryId}?sig&exp` | **No** — allowlisted | HMAC email `document_link`; **302** redirect to R2 |

Final signed PDF is intentionally **not** in TypeSpec: consumers are human email links only (`DocumentPublishedIntegrationEventHandler`), success is a redirect (not streamable PDF), and claiming `bytes` would be dishonest. See `docs/contracts/openapi-vs-minimal-api.md` and `packages/api-spec/honesty-allowlist.yaml`. Promote to TSP only if product needs a typed client / Scalar path (model 302, do not claim PDF body).
