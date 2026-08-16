# W1 — LP-004 analysis: real SaaS fee (not GMV take-rate)

**Program:** `plans/007-feats`  
**ID:** LP-004 — *Real SaaS fee (not GMV take-rate)*  
**Wave:** 1 (`00-implement-ids.md`; tracker row LP-004 = **P** today)  
**Date:** 2026-08-16  
**Branch:** `feat/007-waves-1-4-implement`  
**Status:** Analysis only — **do not implement from this file**  
**Related:** ADR 019 §2–3; ADR 021 high-ARPU-on-compliance (not GMV); planes in `20-sequencing-and-tracker-schema.md` P6; commercial packaging in `18-pricing-onboarding-trust.md`; refuse `LP-002` / `LP-003` / `LP-XX-001` / `LP-XX-012`.

**Feature in one sentence:** Lazuar must be able to charge a **tenant** a **flat / subscription software fee** for Hub itself (plane **S**), while guest GMV stays on **tenant BYOK** (plane **G**) at **0% take**, and prepaid credits stay a separate meter (plane **U**, `LP-005`).

**Wave 1 bar:** a stranger can be invoiced and can pay a listed Hub plan amount into **Lazuar’s** platform gateway. Checkout GMV is not taxed. We do not become Merchant of Record for tenant sales.

---

## 0. ID lock (do not confuse)

Three files reuse `LP-004`. This ticket is **only** the implement-list / tracker row.

| File | That file’s `LP-004` | This ticket? |
|------|----------------------|--------------|
| [`00-implement-ids.md`](../00-implement-ids.md) / [`00-checklist-tracker.md`](../00-checklist-tracker.md) | **SaaS fee (not take-rate on GMV)** — Wave 1, Lazuar = **P** | **Yes** |
| [`01-lazuar-feature-inventory.md`](../01-lazuar-feature-inventory.md) | Multi-tenant workspaces + switcher — **SHIPPED** | No |
| [`18-pricing-onboarding-trust.md`](../18-pricing-onboarding-trust.md) gap table | Credits consume on LHDN / WA as marketed | No — that is **`LP-005`** on the implement list |

Closest *other* row in `18-pricing-onboarding-trust.md`: **`LP-002` “Hub Pro flat SaaS SKU (none)”**. That is this work under the **implement-ids** number.

---

## 1. Verdict

| Question | Answer |
|----------|--------|
| Do we take a % of guest GMV today? | **No.** No `application_fee`, no Connect destination charge, no platform split. Tenant BYOK; money lands on the merchant rail. |
| Do we charge a flat Hub SaaS fee today? | **No.** Named in ADR 019. **No SKU, no workspace subscription, no invoice, no charge path.** |
| What *do* we charge tenants? | Optional **utility credit packs** (RM 50 / 100 / 200) via platform checkout. Checkout / M2M cashier **do not** debit the wallet. A tenant can run forever on the 50-credit starter grant + BYOK. |
| Is Paddle in this repo? | **No.** Zero `Paddle` / `paddle` hits under `apps/`. Aura Pro (RM 149 / 1,490) is **outside Hub** (`product-lines.md`). |
| Can we reuse Commerce subscriptions as “Hub Pro”? | **No.** That is the tenant’s catalog (plane **G**). `metadata.type = saas_subscription` is an Aura *Commerce* label, not Lazuar’s seat. |
| Can we reuse `InvoiceIssuedIntegrationEvent`? | **No.** Handler exists; **no publisher**. Lhdn also consumes it and would submit a **tenant** MyInvois as if the tenant sold the line. Plane collapse. |
| New table / migration? | **Yes** — one workspace SaaS subscription aggregate (+ invoice number sequence or derived id). |
| Hard paywall checkout? | **No for this ticket.** Card-at-signup is `LP-006`. Forced collect would double-bill Aura (already on Paddle) and kill PLG. |
| Tracker after a complete implement? | Flip to **Y** only if subscribe → pay flat RM → invoice PDF is demoable **and** GMV still has 0% platform take. Stay **P** if schema/config ships without a live charge. |

**Honest remaining work:** one config-driven plan, one workspace subscription row, one **platform** (system-tenant) checkout type that is **not** a credit top-up, one Lazuar-as-seller invoice, ledger skip so the payment is not booked as creator GMV. Do not add Paddle. Do not add a take-rate.

---

## 2. Feature lock

### In scope

- One Hub plan SKU (flat MYR, monthly **or** yearly — pick one in config; not both, not seats).
- Workspace subscription record (status + paid-through).
- Invoice (Lazuar = seller, workspace = buyer) + charge via **existing system checkout**.
- Ops surface on the already-routed `/workspace/billing` page + a sidebar link.
- Ledger / webhook typing so plane **S** cannot land as plane **G**.

### Explicit non-goals (other IDs / refuse)

| ID / trap | Why it is not this ticket |
|-----------|---------------------------|
| **LP-005** | Prepaid utility credits honesty (LHDN 3+1, WA dark, sidebar). Keep packs; do not “fix” the wallet here. |
| **LP-006** | Public signup + pricing page. No marketing site in this repo. |
| **LP-002 / LP-003** (implement refuse) | Become MoR / licensed acquirer / hold settlement. |
| **LP-054 / 057–060 / 063** | Trial, pause, plan change, proration, seats, multi-price. Wave 3. |
| **LP-010** in `18-pricing` | SST 8% on *our* invoice. Reserve a **0%** SST line + reason string; do not invent registration. |
| **`saas_subscription` Commerce metadata** | Aura distinguisher on **tenant** checkouts (`CommerceCheckoutMetadata.TypeSaas`). Do not reuse. |
| **Paddle Billing in Pay** | Aura System A. No SDK here. A later commercial ADR if we ever want MoR *for Hub seats*. |
| **Per-checkout credit** | `18-pricing` LP-005 **Never** — take-rate in disguise. |
| **Stripe Connect `application_fee`** | `18-pricing` LP-009 **Never**. Grep of `apps/` is already empty. |
| **Hard-gate** `/checkouts` / portal | Breaks PLG and Aura Connect. Banner only. |
| **Auto-invoice every workspace on register** | Double-bills `external_product=aura` (they already pay Paddle). Subscribe is **explicit**. |

### Standing locks (do not contradict)

- ADR 019: BYOK, not MoR. *“The core checkout software is sold as a flat SaaS fee.”* Credits meter LHDN / WhatsApp, not GMV.
- `00-evaluation.md`: “We must not race HitPay on MDR — we do not take MDR.”
- `19-refuse-list-and-adjacents.md`: plane **A** (tenant → Lazuar) must never be funded by taking plane **B** (buyer → merchant).
- `20-sequencing` P6: “Do not put ‘Pro on Billplz’ here” means **do not bill Hub Pro through the tenant’s BYOK keys** (plane G). Credit top-up already uses the **system** tenant’s keys. That collector is the honest reuse. It is **not** “Pro on the merchant’s Billplz.”

---

## 3. Money planes (write them on every new type)

| Plane | Who → whom | Collector in this repo today | LP-004? |
|-------|------------|------------------------------|---------|
| **G — merchant GMV** | Buyer → tenant | Tenant `TenantPaymentConfiguration` (Billplz / Stripe / CHIP / Razorpay) | Protect **0%**. Never add a cut. |
| **U — utility credits** | Tenant → Lazuar | System org `00000000-0000-0000-0000-000000000001` + `metadata.type = utility_credit_topup` | Leave alone (except: do not default new checkouts to this type). |
| **S — Hub SaaS fee** | Tenant → Lazuar | **Missing.** Aura’s analogue is **Paddle in the Aura repo**, not here. | **Build this.** New type `platform_saas_fee`. |
| **A1 — Aura Pro** | Salon → Aura | Paddle, **not Hub** | Do not move. Do not auto-charge Hub Pro on `external_product=aura` workspaces. |

Mixing S into G is trap `LP-XX-012` (“Pro plan billed through tenant Billplz”). Mixing S into U is “buy credits, accidentally get a month of Hub” (or the reverse).

---

## 4. What exists (read, not assumed)

### 4.1 No Hub plan catalogue

`Organization` (`/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Domain/Organization.cs`) has name, slug, `IsActive`, optional `ExternalProduct` / `ExternalOrgId`. **No plan, period, or paid-through.**

`TenantAppEntitlement` (`.../One/Domain/TenantAppEntitlement.cs`) is a **module flag** (`OPS`, `BILLING`, `PAYMENTS`, `CRM`, `LHDN`), not a paid SKU. Public register grants all five for free:

`/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Application/Commands/RegisterPublicUserCommand.cs` (`CoreModules`).

`CreateWorkspaceCommand` grants whatever `ProvisionApps` the caller sends. Integrator provision (`ProvisionAuraWorkspaceCommandHandler.Tenant.cs`) grants **PAYMENTS** only.

There is **no** `WorkspaceSaasSubscription`, no `Saas` config section, no TypeSpec plan model.

### 4.2 Billing module — ledger + wallet, not seats

Module root: `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Billing/`

| Surface | Path | What it is |
|---------|------|------------|
| Admin credits | `Infrastructure/Endpoints/AdminCreditsEndpoints.cs` | `GET /credits`, `GET /credits/packages`, `POST /credits/top-up` (min RM 50) |
| Admin ledger | `Infrastructure/Endpoints/AdminLedgerEndpoints.cs` | ledger list, document URL, summary, net-profit |
| Admin profile | `Infrastructure/Endpoints/AdminProfileEndpoints.cs` | tenant legal profile (seller-of-record for **their** buyers) |
| Public billing | `Infrastructure/Endpoints/PublicBillingEndpoints.cs` | public profile + HMAC document links |
| Wallet | `Domain/Aggregates/TenantCreditBalance.cs` | integer prepaid units |
| Tenant legal | `Domain/Aggregates/TenantBillingProfile.cs` | TIN / SST / address for **tenant→buyer** PDFs |
| Account codes | `Domain/AccountTypes.cs` | includes `EXPENSE_SOFTWARE_SUBSCRIPTION`; **no** `REVENUE_SAAS` |
| Ref types | same file | `SYSTEM_CREDIT_TOPUP` / `SYSTEM_CREDIT_CHARGEBACK`; **no** `SYSTEM_SAAS_FEE` |
| TypeSpec | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/api-spec/modules/billing/{routes,models}.tsp` | credits + ledger + profile only |

`InvoiceIssuedIntegrationEvent` (`Contracts/Events/InvoiceIssuedIntegrationEvent.cs`) is subscribed in Billing (`InvoiceIssuedHandler` books AR + deferred revenue) **and** Lhdn (`InvoiceIssuedIntegrationEventHandler` calls `SubmitTaxDocumentCommand` with **placeholder buyer TIN**). **No in-repo publisher.** Dead until someone publishes — do not publish for Hub Pro.

`ApiCreditPurchasedHandler` is **not** registered in `UseBillingSubscriptions`. Dead parallel of platform top-up. Leave it.

### 4.3 Platform charge rail (credits only)

System tenant is genesis-inserted:

`/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/Workers/SystemGenesisBootstrapperJob.cs`  
id `00000000-0000-0000-0000-000000000001`, slug `system`.

`/api/v1/platform/*` binds that tenant (`TenantSecurityMiddleware.cs`). Superadmin vault UI: `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-admin/src/modules/platform/pages/PlatformPaymentSettingsPage.tsx` → `GET/PUT /platform/payment-config` (`Payments/Infrastructure/PlatformEndpoints.cs`).

Credit purchase:

```
Ops POST /admin/billing/credits/top-up { amount_myr, return_url }
  → GenerateSystemCheckoutSessionQuery (default GatewayName = "BILLPLZ")
  → system tenant keys
  → metadata type=utility_credit_topup, tenant_id=<paying workspace>
  → PlatformTopUpEventHandler grants pack + SYSTEM_CREDIT_TOPUP
       EXPENSE_SOFTWARE_SUBSCRIPTION  +amount
       ASSET_CASH                     −amount
  → GatewayPaymentCompletedHandler returns early (does not book REVENUE_GROSS)
```

Paths:

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Contracts/Queries/GenerateSystemCheckoutSessionQuery.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Application/Queries/GenerateSystemCheckoutSessionQueryHandler.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/PlatformTopUpEventHandler.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/GatewayPaymentCompletedHandler.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/ChargebackClawbackHandler.cs` (utility only)

Config packs (`appsettings.json` `Credits`): RM 50→500, 100→1100, 200→2500; `StarterGrant` 50. Seeded by `StarterCreditSeederHandler` on `AppId == BILLING`.

**There is no `GenerateSystemCheckoutSessionQuery` test.** Top-up handler tests exist (`PlatformTopUpEventHandlerTests.cs`, `LedgerBalanceMatrixTests.cs`).

### 4.4 Webhook metadata — Billplz vs Stripe

Billplz strips body metadata. Adapter stamps `reference_2 = type`, `reference_1 = subscription_id ?? tenant_id`:

`/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/BillplzGatewayAdapter.cs` (generate ~69–103, parse ~199–210).

On parse, `reference_1` becomes `tenant_id` **only if** `reference_2 == "utility_credit_topup"`. Any new type must be added to that `if` or SaaS webhooks will look like Commerce (`subscription_id`) and the SaaS handler will no-op.

Stripe copies session metadata onto the PaymentIntent, but **overwrites** `tenant_id` with the adapter `tenantId` argument:

```30:30:apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/StripeGatewayAdapter.cs
            metadata["tenant_id"] = tenantId.ToString();
```

System checkout passes `systemId` as that argument. So a Stripe platform charge **loses the paying workspace id** unless we stop overwriting (or stamp a second key the adapter must not touch). This is already a live footgun for credit top-ups on Stripe.

`GenerateSystemCheckoutSessionQueryHandler` also **defaults missing `type` to `utility_credit_topup`**. A SaaS caller that forgets `type` would grant credits. The new endpoint must set type itself, and the handler must **not** default when the caller is SaaS (or stop defaulting entirely and require type).

Admin credits always uses default `GatewayName = "BILLPLZ"`. If admin only configured Stripe on the system tenant, top-up (and a copy-paste SaaS checkout) throws “not configured.” Smallest fix: resolve the first **active** system gateway.

### 4.5 Workspace billing UI

| Path | Routed? | Sidebar? |
|------|---------|----------|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/modules/workspace/pages/BillingSettingsPage.tsx` | Yes — `/workspace/billing` (`App.tsx`) | **No.** Workspace nav is General / Payment Gateways / Email only (`Sidebar.tsx` ~260–264). |
| `UtilityLedgerPage.tsx` | Yes — `/workspace/ledger` | **No.** |
| `BillingProfilePage.tsx` | **Commented** `[MVP-HIDE]` ADR 023 | No. |

`BillingSettingsPage` is titled “Platform Billing” and is **credits-only** (balance + three pack buttons + “Purchase Credits”). Copy still promises WhatsApp dunning (`Messaging:WhatsAppEnabled=false`). No plan, no invoice list, no “you owe Lazuar RM X.”

Dashboard net cash (`GET /admin/billing/summary`) sums `REVENUE_GROSS` / fees / tax. `EXPENSE_SOFTWARE_SUBSCRIPTION` is excluded from gross/net — **correct** (matrix test documents this). A SaaS fee booked the same way will not inflate creator GMV **if** we do not also hit `GatewayPaymentCompletedHandler`.

### 4.6 Commerce `saas_subscription` is not Hub Pro

`/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Application/CommerceCheckoutMetadata.cs`

`TypeSaas = "saas_subscription"` lets a **tenant** tag a Commerce checkout (Aura experiments). `IsCommerceSubscriptionType` treats it as **the same plane as** `commerce_subscription`. Using that string for Lazuar’s own fee would make Commerce lifecycle handlers treat a Hub invoice as a creator subscription.

### 4.7 Paddle

- **This repo:** no package, no webhook, no product id, no env var.
- **Docs:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-docs/docs/guide/product-lines.md` — “Aura Plan / Paddle … **Not Hub**.” “Never route that through Billplz BYOK on Hub.”
- **Who-does-what M6:** “SaaS seat billing (platform fee) … **Still separate** (e.g. Paddle) — not this path.” That sentence is about **not** putting seats on the **cashier** (tenant GMV). It is not a requirement to import Paddle into Pay.
- **19-refuse:** plane A collector is “Paddle MoR (Aura Plan today; **Pay’s own commercial collection later**).” This ticket **is** that “later,” implemented with the **already-built** system checkout, not a new MoR.

Bringing Paddle into `lazuar-pay` would be a new SDK, product catalog, webhook surface, and would make **Paddle** the seller of Hub. That is a commercial ADR, not Wave 1 smallest. It also does not help Malaysian SST/MyInvois on *our* invoice.

### 4.8 0% GMV is already true (protect it)

- Payments adapters have **no** `application_fee` / Connect.
- Fee estimation columns were removed (`RemoveAccountingOverrides`); webhook handler passes `estimatedFeePercentage=0` (Billplz net is wrong for **gateway** MDR — not a platform take; not this ticket).
- Checkout, M2M `POST /integrations/payments/checkouts`, and Commerce renewals do **not** call `DeductTenantCreditCommand`.
- Dual-post of utility top-up as GMV was closed: `GatewayPaymentCompletedHandler` skips `utility_credit_topup` (covered by `LedgerBalanceMatrixTests`).

---

## 5. Gaps vs “real SaaS fee”

| # | Gap | Why it matters |
|---|-----|----------------|
| G1 | No plan SKU | ADR 019 sentence is false on a sales call. Tracker **P** is this hole, not a take-rate. |
| G2 | No workspace subscription | Nothing to invoice, nothing to show “ACTIVE until …”. Entitlements are free module flags. |
| G3 | No plane-S checkout type | Only `utility_credit_topup` is a tenant→Lazuar payment. Defaulting `type` in system checkout would mix S and U. |
| G4 | No invoice from **Lazuar → tenant** | Existing QuestPDF (`GenerateAndStoreDocumentCommandHandler`) uses **tenant** profile as seller and Commerce CRM as buyer. A Hub invoice generated that way would look like the tenant billed themselves. |
| G5 | `InvoiceIssued` is the wrong event | Lhdn would file **the tenant’s** MyInvois. Supplier TIN would be wrong. |
| G6 | Ops page is credits-only and not in the sidebar | Even after an API exists, humans cannot subscribe. |
| G7 | Stripe `tenant_id` overwrite + Billplz type allow-list | Charge can succeed and never activate the workspace. |
| G8 | System checkout hardcoded `BILLPLZ` | Platform Stripe-only deploys cannot collect. |
| G9 | No SaaS dispute / refund path | Credit clawback is utility-only. Acceptable leftover if first version is “all Hub fees final” in copy; do not silently ignore disputes. |
| G10 | No SST line | Fine at **0% + written reason**. Do not print 8% without an SST ID. |
| G11 | No recurring job | First paid period can be “pay again to extend.” A daily mint-pay-link job is optional in the same ticket if it stays reminder-only (Billplz cannot off-session). |

What is **not** a gap: 0% GMV, BYOK, Paddle-for-Aura, credit packs.

---

## 6. Minimal implementation (smallest honest path)

One plan. One subscribe button. One platform checkout. One invoice PDF. One ledger skip. No Paddle. No take-rate. No paywall.

### 6.1 Config (no invented public list price)

Add `Saas` beside `Credits` in `appsettings.json`. Tests use fixture numbers. **Do not** put a RM figure in README / docs as if it were a published card (`LP-006`).

```json
"Saas": {
  "Plan": {
    "Code": "hub_starter",
    "Name": "Hub Starter",
    "AmountMyr": 0,
    "Interval": "mo",
    "Currency": "MYR"
  },
  "Seller": {
    "LegalName": "Lazuar",
    "Tin": "",
    "Address": "",
    "SstId": "",
    "SstRate": 0,
    "SstReason": "Supplier not SST-registered"
  }
}
```

`AmountMyr` is operator-set. Bind via `IOptions<SaasOptions>`. Reject checkout if `AmountMyr <= 0` (forces a conscious price before anyone can pay). Interval is `mo` or `yr` only.

### 6.2 Domain — Billing, not One, not Commerce

New aggregate `WorkspaceSaasSubscription` in `billing` schema (one row per org):

| Field | Notes |
|-------|--------|
| `OrganizationId` | unique |
| `PlanCode` | from config |
| `Status` | `UNPAID` \| `ACTIVE` \| `PAST_DUE` \| `CANCELED` |
| `CurrentPeriodStart` / `CurrentPeriodEnd` | UTC |
| `NextInvoiceAt` | = period end for reminder-only renew |
| `LastGatewayTransactionId` | idempotency aid |
| `UpdatedAt` | |

Methods: `MarkUnpaid()`, `ActivateFromPayment(now, interval)`, `MarkPastDue()`, `Cancel()`.  
`ActivateFromPayment`: `ACTIVE`, period = now + 1 month/year, `NextInvoiceAt = CurrentPeriodEnd`.

Do **not** add a `PLAN` entitlement. Do **not** add columns on `Organization`.

New constants:

- `LedgerReferenceTypes.SystemSaasFee = "SYSTEM_SAAS_FEE"`
- Metadata `type = platform_saas_fee` (string literal, one constant, shared Billing + Payments)

### 6.3 Charge path

**POST `/admin/billing/saas/checkout`** (`OrgAdmin`):

1. Load config plan; 400 if `AmountMyr <= 0`.
2. Upsert `WorkspaceSaasSubscription` to `UNPAID` if missing (do not start a period).
3. `GenerateSystemCheckoutSessionQuery` with:
   - `Amount` = config (ignore client amount)
   - `ProductName` = config name (e.g. `Hub Starter (monthly)`)
   - `Metadata`: `type=platform_saas_fee`, `tenant_id=<ctx.TenantId>`, `plan_code=hub_starter`
   - **Gateway:** first active system config (Billplz or Stripe), not a hardcoded name
4. Return `{ checkout_url }` (reuse `TopUpResponseDto` or a tiny twin).

**Handler `PlatformSaasFeeHandler`** on `GatewayPaymentCompletedIntegrationEvent`:

1. Require `type == platform_saas_fee` and parseable `tenant_id`.
2. Require non-empty `GatewayTransactionId`.
3. Idempotent: existing `SYSTEM_SAAS_FEE` + that tx id → return.
4. Amount must match config (or `>=` config if gateway fees-on-top; prefer exact). Mismatch → log + no activate.
5. `ActivateFromPayment`.
6. Ledger on **paying tenant** (same polarity as credits, **no wallet grant**):
   - `EXPENSE_SOFTWARE_SUBSCRIPTION` +amount
   - `ASSET_CASH` −amount
   - `B2B`, `MarkConsolidationNotRequired()`
7. Generate **Lazuar-as-seller** PDF (see 6.4).
8. Do **not** publish `InvoiceIssuedIntegrationEvent`.

**`GatewayPaymentCompletedHandler`:** skip `platform_saas_fee` the same way as `utility_credit_topup`. Otherwise the event’s `OrganizationId` is the **system** tenant and we would book `REVENUE_GROSS` on `system` as if guests paid Hub.

**`GenerateSystemCheckoutSessionQueryHandler`:** stop blindly defaulting `type` to `utility_credit_topup`. Require `type` from the caller.

**Billplz parse:** treat `platform_saas_fee` like `utility_credit_topup` (`reference_1` → `tenant_id`).

**Stripe generate:** do not overwrite `tenant_id` when metadata already has a different paying tenant. Keep `tenant_id` = paying workspace; system id can live as `platform_tenant_id` if something needs it.

**Chargeback (smallest):** if `type == platform_saas_fee`, do **not** claw credits. Either no-op + log, or `MarkPastDue()` on the workspace row. Do not reverse GMV. Full refund SKU is `LP-091` (ops refund of **tenant** sales), not this.

Webhook still hits `/webhooks/payments/{gateway}/{systemId}` — same as credits. Paying tenant is **only** in metadata.

### 6.4 Invoice (honest seller)

New document path (do not reuse `GenerateAndStoreDocumentCommand` as-is):

- Seller = `Saas:Seller` (Lazuar).
- Buyer = workspace name + admin email (`IOneQueryService`), not Commerce CRM.
- Lines: one “Hub Starter — {month|year} software subscription”, subtotal = plan amount, SST = 0, SST reason printed, total = subtotal.
- Invoice number: `SAAS-{yyyy}-{n}` via `GenerateNextSequenceNumberCommand` on the **system** org (Lazuar’s series, not the tenant’s `RCPT-`).
- Store under `vault/{payingTenantId}/documents/{ledgerEntryId}.pdf` so existing `GET /admin/billing/ledger/{id}/document` works.
- Filename / heading: **Tax invoice** only if we have a TIN; otherwise **Invoice / payment receipt** — do not claim MyInvois.

QuestPDF can stay; `InvoiceDocumentModel` already has company vs customer fields. The bug is the **handler** filling company from the tenant profile.

### 6.5 Recurring (keep reminder-only)

Smallest that is still a subscription:

- After pay, period is 1 mo/yr.
- Ops shows period end + **“Pay next period”** (same checkout endpoint). Second payment extends from `max(now, CurrentPeriodEnd)` so early renewals do not waste days.
- Optional same-ticket job `PlatformSaasInvoiceJob` (daily): if `NextInvoiceAt <= now` and `ACTIVE`, set `PAST_DUE` (do not mint a ghost checkout that expires). Email is `LP-151` — do not invent a new mailer here.

Do **not** call Commerce `BillingEngineJob`. Do **not** `setupFutureUsage` on system Stripe just to look like Chargebee.

### 6.6 Ops UI

- Sidebar Workspace: add **Plan & billing** → `/workspace/billing`.
- Extend `BillingSettingsPage`:
  1. **Hub plan** card: name, RM amount, interval, status, period end, Pay / Renew button.
  2. Keep the existing credits card below, relabeled **Utility credits** (not “this is how we charge for checkout”).
- `GET /admin/billing/saas` returns the workspace row + config plan (amount, name) so the UI does not hardcode RM.

No public pricing page. No register card field.

### 6.7 TypeSpec

Add to `packages/api-spec/modules/billing/`:

- `SaasPlanDto`, `WorkspaceSaasSubscriptionDto`, `CreateSaasCheckoutResponseDto`
- `GET /admin/billing/saas`
- `POST /admin/billing/saas/checkout`

Run `task gen`. Do not claim `InvoiceIssued` in OpenAPI.

### 6.8 Who we do not auto-charge

| Workspace | Default |
|-----------|---------|
| Public register | `UNPAID` until they click Pay |
| Extra workspaces (`CreateWorkspaceCommand`) | same |
| `external_product` provisioned (Aura, sample) | same — **no** auto-invoice (Aura already pays Paddle) |
| System org | never a customer |

Complimentary / grandfather is `UNPAID` + checkout still works. If a founder later wants “Aura workspaces complimentary ACTIVE,” that is a config allowlist — not a second plan.

### 6.9 File touch list (expected, not a commit)

| Area | Files |
|------|--------|
| Config + options | `appsettings.json`, new `SaasOptions`, DI bind |
| Domain | `WorkspaceSaasSubscription.cs`, `AccountTypes` / `LedgerReferenceTypes` |
| Migration | `Billing` EF migration |
| Endpoints | new `AdminSaasEndpoints.cs`, map from `Endpoints.cs` |
| Handler | `PlatformSaasFeeHandler.cs`, register subscribe |
| Skip GMV | `GatewayPaymentCompletedHandler` |
| System checkout | `GenerateSystemCheckoutSessionQueryHandler` (no silent credit default; pick active gateway) |
| Rails | `BillplzGatewayAdapter` parse; `StripeGatewayAdapter` preserve `tenant_id` |
| Invoice | new command or branch in document handler with seller override |
| TypeSpec + gen | `billing/models.tsp`, `routes.tsp` |
| Ops | `BillingSettingsPage.tsx`, `Sidebar.tsx` |
| Tests | see §7 |

Do **not** add a Paddle project, Connect fee, Commerce product named “Hub Pro,” or `DeductTenantCredit` on checkout.

---

## 7. Tests needed

Mirror existing billing module tests (`Lazuar.ModuleTests/Billing/EventHandlers/*`, `LedgerBalanceMatrixTests`).

### Must

| Test | Assert |
|------|--------|
| `PlatformSaasFeeHandler` happy path | `ACTIVE`, period advanced, **0 credits** granted, one `SYSTEM_SAAS_FEE`, balanced `EXPENSE_SOFTWARE` / `ASSET_CASH` on **paying** org |
| Idempotent redelivery | same `GatewayTransactionId` → still one ledger, period not doubled |
| Wrong / missing `type` | no-op (Commerce and `utility_credit_topup` unchanged) |
| Missing `tenant_id` | no-op, no row on system org |
| `GatewayPaymentCompletedHandler` skip | `platform_saas_fee` does **not** create `GATEWAY_PAYMENT` / `REVENUE_GROSS` (extend `LedgerBalanceMatrixTests`) |
| Top-up regression | `utility_credit_topup` still grants credits and is still skipped by the GMV handler |
| Amount ≠ config | no activate |
| `AmountMyr <= 0` | checkout endpoint 400 |
| Checkout metadata | request always has `type=platform_saas_fee` and paying `tenant_id`; handler does not inject `utility_credit_topup` |
| Stripe preserve tenant | generate checkout does not replace paying `tenant_id` with system id |
| Billplz parse | `reference_2=platform_saas_fee` + `reference_1=<guid>` → `metadata.tenant_id` |
| Invoice seller | PDF/model company name is `Saas:Seller.LegalName`, **not** workspace name; SST line 0 + reason |
| No `InvoiceIssued` | handler / endpoint never publishes that event (NSubstitute `DidNotReceive`) |
| Commerce isolation | a Commerce payment with `type=saas_subscription` still takes the GMV path, not the Hub plan path |
| GET saas | `UNPAID` for a new org; after handler, `ACTIVE` + period end |
| No `application_fee` | architecture or adapter test: Stripe session create options have no `ApplicationFeeAmount` / `TransferData` (lock the refuse) |

### Should

| Test | Assert |
|------|--------|
| Renew while `ACTIVE` | period extends from current end, not from now (or from `max(now, end)` — pick one and test it) |
| Dispute `platform_saas_fee` | credits unchanged; optional `PAST_DUE` |
| Active gateway pick | system Stripe-only → checkout still created |
| Sequence | two invoices → distinct `SAAS-{year}-*` on system org |

### Do not add for this ticket

- Playwright / Paddle sandbox.
- Hard-gate: “checkout 402 without ACTIVE plan.”
- SST 8% math.
- Off-session system Stripe.
- LHDN submit of the Hub invoice.

---

## 8. Acceptance

Demoable on a current deploy (system gateway configured in `lazuar-admin`):

1. **0% GMV still true.** A guest Commerce / M2M payment on **tenant** Billplz/Stripe does not create `SYSTEM_SAAS_FEE`, does not change Hub plan status, and still has no platform application fee.
2. **Subscribe is explicit.** Register + provision still create workspaces with **no** card and **no** Hub invoice. Aura `external_product` workspaces are not auto-billed.
3. **Pay a flat fee.** OrgAdmin opens Plan & billing, clicks Pay, pays **config** `AmountMyr` on the **platform** hosted page (Lazuar’s keys). Amount is not a % of anything the tenant sold.
4. **Activation.** After the verified webhook (not the redirect), status is `ACTIVE` and `CurrentPeriodEnd` is now + 1 mo/yr.
5. **Invoice.** Tenant can download a PDF that names **Lazuar** as seller and the **workspace** as buyer, one software line, SST 0 + reason. No MyInvois claim. No tenant LHDN submit.
6. **Planes stay apart.** Same gateway payment does not grant utility credits and does not book `REVENUE_GROSS` on the tenant (or on `system` as guest GMV).
7. **Credits still work.** Existing top-up path unchanged.
8. **BYOK unchanged.** Tenant payment-config UI and cashier still use tenant keys. System keys are only for credits + Hub plan.
9. **Not MoR for guests.** Portal TOS / checkout copy still “direct transaction with Creator.” We did not add Paddle for GMV. We did not hold settlement.
10. **Copy.** Ops page does not say “we take X% of sales.” Sidebar does not hide the only place to pay Hub.

**Not required to flip the tracker to Y:** public pricing page, SST 8%, hard paywall, dunning email for Hub, annual+monthly both, seats, Paddle.

**Keep tracker P if:** config/schema exists but checkout 500s (no system gateway / Stripe `tenant_id` still overwritten) or the only “plan” is still free credits.

---

## 9. Why this shape (and not the tempting ones)

| Temptation | Why refuse |
|------------|------------|
| Paddle in this repo | No code, new company surface, MoR for Hub seats, hostile to our own MY invoice. Aura already has Paddle. |
| Commerce product “Hub Pro” on each tenant | Plane G. Would charge the tenant’s **customers** or use tenant BYOK to pay us (`LP-XX-012`). |
| 0.2–2% application fee “until we have a plan” | HitPay/Fresha gravity. Destroys the 0% sentence. SST on a GMV take. Refuse. |
| 1 credit per checkout | Same take-rate, worse (punishes RM 50 FPX). `18-pricing` Never. |
| Publish `InvoiceIssued` | Fires Lhdn with fake buyer TIN against the **tenant**. |
| Card at signup | `LP-006`. Kills Polar-class PLG; Wave 1 still wants TTFC. |
| Hard-gate cashier | Breaks Aura Connect and the sample app until every workspace pays. |
| Full Chargebee (trials, proration, seats) | Wave 3. Not needed to stop lying about “flat SaaS + credits.” |

The commercial sentence after this ticket, if amount is set and charge works:

> **RM 0 on your sales.** You pay Billplz/Stripe their rate. You pay Lazuar a **flat Hub subscription** (invoiced) plus optional credits for LHDN / WhatsApp when those meters are real.

Until `AmountMyr` is set and a human can complete the checkout, keep saying the second sentence from `18-pricing`: checkout software is **free today**; credits are a separate, mostly-dark meter.

---

## 10. Leftovers (do not sneak into the PR)

- Public price card + SST footnote (`LP-006` / `18-pricing` LP-001 / LP-010).
- Credit 3+1 LHDN deduct, WA flag, sidebar for ledger (`LP-005`).
- System-tenant **revenue** books for Lazuar-the-company (`REVENUE_SAAS` on org `…0001`). Tenant expense is enough for Wave 1.
- Hub-plan dunning email / magic link.
- Refund policy page for Hub fees.
- Complimentary allowlist for Aura.
- Replacing Aura Paddle with this SKU.

---

**Do not implement from this file.** Implement only when a follow-up ticket on `feat/007-waves-1-4-implement` says so.
