# W1-LP-004 — done

Hub can charge a workspace a **flat `hub_starter` SaaS fee** on **platform** keys (`type=platform_saas_fee`). Guest GMV stays tenant BYOK at **0%** take. Utility credits stay a separate meter. Subscribe is explicit (no paywall, no auto-invoice, no Aura/Paddle move).

`Saas:Plan:AmountMyr` is **0** in repo config. Checkout is **400** until an operator sets a positive MYR amount. Tracker LP-004 Lazuar stays **P** — the charge path is real; a live listed price is not.

## Files changed

### Config + domain

- `apps/lazuar-api/src/Lazuar.Api/appsettings.json` — `Saas.Plan` `hub_starter` / MYR / `mo`, `AmountMyr` 0; `Saas.Seller` Lazuar, SST 0 + reason.
- `Modules/Billing/Infrastructure/Services/SaasOptions.cs` — **new.** Bound in Billing DI.
- `Modules/Billing/Domain/Aggregates/WorkspaceSaasSubscription.cs` — **new.** `UNPAID` / `ACTIVE` / `PAST_DUE` / `CANCELED`. `ActivateFromPayment` extends from `max(now, CurrentPeriodEnd)`.
- `Modules/Billing/Domain/SaasPlanInterval.cs` — **new.** `mo` / `yr` only.
- `Modules/Billing/Domain/AccountTypes.cs` — `LedgerReferenceTypes.SystemSaasFee`.
- `Modules/Payments/Contracts/PlatformCheckoutTypes.cs` — **new.** `utility_credit_topup`, `platform_saas_fee`, system org id.

### Charge path

- `POST /admin/billing/saas/checkout` + `GET /admin/billing/saas` (`AdminSaasEndpoints.cs`). Amount from config only. Upsert `UNPAID` if missing. System org rejected.
- `CreateSaasCheckoutCommandHandler` → `GenerateSystemCheckoutSessionQuery` with `type=platform_saas_fee` and paying `tenant_id`. First active **system** gateway (not hardcoded Billplz).
- `GenerateSystemCheckoutSessionQueryHandler` — **requires** `metadata.type`. Does not default to credits. Picks first active system config when `GatewayName` is omitted.
- `PlatformSaasFeeHandler` — activate, `SYSTEM_SAAS_FEE` expense/cash on the **paying** org, **0 credits**, Lazuar invoice command. Never publishes `InvoiceIssuedIntegrationEvent`.
- `GatewayPaymentCompletedHandler` — skip `platform_saas_fee` the same way as utility top-up (no `REVENUE_GROSS` on `system`).
- `ChargebackClawbackHandler` — SaaS dispute → `PAST_DUE`, no credit clawback, no GMV reverse.

### Rails

- `StripeGatewayAdapter` — do not overwrite paying `tenant_id`; stamp `platform_tenant_id` when the adapter tenant is the system org. Checkout session options have no Connect `ApplicationFeeAmount` / `TransferData`.
- `BillplzGatewayAdapter` parse — `reference_2=platform_saas_fee` maps `reference_1` → `tenant_id`.

### Invoice

- `GenerateAndStorePlatformSaasInvoiceCommand` — seller = `Saas:Seller` (Lazuar), buyer = workspace + admin email. Number `SAAS-{yyyy}-*` on the **system** org. PDF at `vault/{payingTenantId}/documents/{ledgerEntryId}.pdf`. SST 0 + reason. Heading is **Invoice / payment receipt** unless seller TIN is set. No MyInvois / no `InvoiceIssued`.
- `InvoiceDocumentModel` / `BaseInvoiceDocument` — optional zero SST line + notes; skip empty TIN.

### Spec + ops

- `packages/api-spec/modules/billing/{models,routes}.tsp` — `SaasPlanDto`, `WorkspaceSaasSubscriptionDto`, checkout request/response; `GET/POST /admin/billing/saas`. `task gen` run.
- `apps/lazuar-ops` — sidebar **Plan & billing** → `/workspace/billing`. Hub plan card + **Utility credits** card. No take-rate copy.

### Migration

- `20260816120000_AddWorkspaceSaasSubscriptions` — `billing.WorkspaceSaasSubscriptions` (unique `OrganizationId`).

### Tests

- `PlatformSaasFeeHandlerTests` — activate, 0 credits, balanced expense/cash, idempotent tx, wrong type (incl. `saas_subscription` + utility), missing/`system` tenant_id, amount mismatch, GET UNPAID→ACTIVE, renew from period end, no `InvoiceIssued`.
- `CreateSaasCheckoutCommandHandlerTests` — `AmountMyr <= 0` throws; metadata type + paying tenant; existing ACTIVE not reset.
- `GenerateSystemCheckoutSessionQueryHandlerTests` — missing type throws (no credit default); Stripe-only first-active still creates checkout.
- `PlatformSaasInvoiceTests` — seller Lazuar, SST 0 + reason; PDF upload; no `InvoiceIssued` / `DocumentPublished`.
- `WorkspaceSaasSubscriptionTests` + ledger `AssignPlatformDocumentNumber`.
- `LedgerBalanceMatrixTests` — SaaS skip GMV; Commerce `saas_subscription` still `GATEWAY_PAYMENT` / `REVENUE_GROSS`.
- `StripeGatewayAdapterTests` — preserve `tenant_id`; no application fee / transfer.
- `BillplzGatewayAdapterTests` — SaaS `reference_2` → `tenant_id`.
- `ChargebackClawbackHandlerTests` — SaaS dispute `PAST_DUE`, credits unchanged.
- Top-up regression (`PlatformTopUpEventHandlerTests`) unchanged.

### Follow-up tests (gap fill)

Dedicated locks for analysis §7 / acceptance 1 and 6 (planes stay apart; GMV still 0% take):

- `PlatformTopUpEventHandlerTests.HandleAsync_PlatformSaasFee_DoesNotGrantCredits` — `type=platform_saas_fee` matching a pack amount does not mint credits or `SYSTEM_CREDIT_TOPUP` (existing starter wallet stays 50).
- `PlatformSaasFeeHandlerTests` happy path also re-runs the top-up handler on the same tx — still 0 credits.
- `GenerateSystemCheckoutSessionQueryHandlerTests.Handle_MissingOrBlankType_Throws_AndDoesNotDefaultToCredits` — missing / empty / whitespace `type` throws; metadata is not rewritten to `utility_credit_topup`; adapter never called.
- `StripeGatewayAdapterTests.CreateCheckoutSessionOptions_HasNoApplicationFeeOrTransfer_AndKeepsPayingTenant` — system adapter keeps paying `tenant_id` on session + PaymentIntent metadata; stamps `platform_tenant_id`.
- `StripeGatewayAdapterTests.CreateCheckoutSessionOptions_TenantGmvCheckout_HasZeroPlatformFee_AndKeepsPayingTenant` — tenant Commerce session has no Connect `ApplicationFeeAmount` / `TransferData`; paying `tenant_id` unchanged.
- `StripeGatewayAdapterTests.PaymentAdapters_DoNotSetConnectApplicationFeeOrTransfer` — Stripe / Billplz / CHIP / Razorpay sources stay free of Connect take-rate fields.
- `LedgerBalanceMatrixTests.GuestGmvPayment_StillZeroPlatformTake_DoesNotCreateSaasFeeOrCredits` — guest Commerce payment still books `GATEWAY_PAYMENT` / `REVENUE_GROSS`; GMV + SaaS + top-up handlers together create no `SYSTEM_SAAS_FEE`, no wallet, no Hub row; summary fee 0.

## Tests run

- `Lazuar.ModuleTests` filter `PlatformSaasFeeHandlerTests|CreateSaasCheckoutCommandHandlerTests|PlatformSaasInvoiceTests|WorkspaceSaasSubscriptionTests|GenerateSystemCheckoutSessionQueryHandlerTests|LedgerBalanceMatrixTests|PlatformTopUpEventHandlerTests|ChargebackClawbackHandlerTests|StripeGatewayAdapterTests|BillplzGatewayAdapterTests|LedgerEntryAndAccountTypesTests|GatewayPaymentCompletedHandlerTests` — **71 passed**
- Follow-up: same filter — **77 passed**, 0 failed, 0 skipped.
- `Modules.Billing.Tests` — **20 passed**
- `Lazuar.ArchitectureTests` — **14 passed**
- `npx tsc --noEmit -p apps/lazuar-ops/tsconfig.json` — clean

Manual subscribe → platform hosted pay → webhook activate **not run** here (`AmountMyr` is 0 until set; needs system gateway in admin).

Not committed. Not pushed.

No Paddle. No GMV take-rate. No paywall. No tenant MyInvois on the Hub invoice. Public price card is `LP-006`.
